using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// JC 원본 PlayFogManager를 KJ_Work로 복제한 버전.
/// 아직 DH 기준 보정은 하지 않고, JC 방식 로직을 최대한 그대로 유지한다.
/// </summary>
public class KJ_PlayFogManager : MonoBehaviour
{
    [Header("Fog Toggle")]
    [Tooltip("전체 안개 On/Off. Off일 때 RT_Current를 완전히 1로 채워 모든 영역이 시야 상태가 됨.")]
    [SerializeField] private bool fogEnabled = true;

    [Header("Layer Restore Delays (탐색 종료 후 복원 시작까지 초)")]
    [Tooltip("최하단 레이어(평지)의 복원 시작 대기 시간")]
    [SerializeField, Min(0f)] private float lowLayerDelay = 5f;
    [Tooltip("중간 레이어의 복원 시작 대기 시간")]
    [SerializeField, Min(0f)] private float midLayerDelay = 5.5f;
    [Tooltip("최상단 레이어(높은 지형)의 복원 시작 대기 시간")]
    [SerializeField, Min(0f)] private float highLayerDelay = 6f;

    [Header("Restore Duration (복원 시작~완료 소요 시간 초, 모든 레이어 공통)")]
    [SerializeField, Min(0.01f)] private float restoreDuration = 0.5f;

    [Header("FogHidable Clip Threshold")]
    [Tooltip("Low 레이어 visibility가 이 값 이하일 때 FogHidable 오브젝트를 stencil clip (완전 Fogged 판정)")]
    [SerializeField, Range(0f, 1f)] private float fogHidableLowThreshold = 0.05f;

    [Header("Mask Rendering")]
    [Tooltip("시야 경계의 smoothstep 부드러움 너비 (월드 유닛)")]
    [SerializeField, Min(0f)] private float maskSmoothEdge = 0.5f;

    private KJ_PlayGridManager grid;
    private RenderTexture rtCurrent;
    private RenderTexture rtExplored;
    private RenderTexture rtExploredTemp;

    // --- 턴제 Decay 추가 데이터 ---
    private int[] gridVisitTurns;
    private Color[] exploredPixels;
    private Texture2D exploredTexCPU;
    private int currentTurn = 0;
    private int maxDecayTurns = 3;
    // ----------------------------

    private Material maskMaterial;
    private Material decayMaterial;
    private CommandBuffer cmd;

    public struct VisibilitySource
    {
        public Vector2 worldPos;
        public float radius;
    }

    private System.Collections.Generic.List<VisibilitySource> visibilitySources = new System.Collections.Generic.List<VisibilitySource>();
    private bool hasVisibilityData => visibilitySources.Count > 0;

    private static readonly int CurrentTexGlobalId = Shader.PropertyToID("_VisibilityCurrentTex");
    private static readonly int ExploredTexGlobalId = Shader.PropertyToID("_VisibilityExploredTex");

    private static readonly int PlayerWorldPosId = Shader.PropertyToID("_PlayerWorldPos");
    private static readonly int SightRadiusId = Shader.PropertyToID("_SightRadius");
    private static readonly int SmoothEdgeId = Shader.PropertyToID("_SmoothEdge");

    private static readonly int CurrentTexInputId = Shader.PropertyToID("_CurrentTex");
    private static readonly int ExploredTexInputId = Shader.PropertyToID("_ExploredTex");
    private static readonly int RestoreDelaysId = Shader.PropertyToID("_RestoreDelays");
    private static readonly int RestoreDurationId = Shader.PropertyToID("_RestoreDuration");
    private static readonly int FogDeltaTimeId = Shader.PropertyToID("_FogDeltaTime");
    private static readonly int FogHidableLowThresholdId = Shader.PropertyToID("_FogHidableLowThreshold");

    public bool FogEnabled
    {
        get => fogEnabled;
        set => fogEnabled = value;
    }

    public void Initialize()
    {
        grid = KJ_FOWManager.Instance?.Grid;
        if (grid == null)
        {
            Debug.LogError("[KJ_PlayFogManager] PlayGridManager를 찾을 수 없습니다");
            return;
        }

        int w = grid.Width;
        int h = grid.Height;

        if (w <= 0 || h <= 0)
        {
            Debug.LogError($"[KJ_PlayFogManager] 잘못된 그리드 크기: {w}x{h}. 초기화 중단.");
            return;
        }

        // 턴제 데이터 초기화
        gridVisitTurns = new int[w * h];
        exploredPixels = new Color[w * h];
        for (int i = 0; i < gridVisitTurns.Length; i++)
        {
            gridVisitTurns[i] = -999; // 초기값 (방문 안 함)
            exploredPixels[i] = Color.clear;
        }
        // JC 셰이더가 RGB(가시성) 및 A(탐색여부)를 모두 사용하므로 RGBA32 필요
        exploredTexCPU = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        rtCurrent = CreateRT(w, h, RenderTextureFormat.R8, "KJ_FogRT_Current");
        rtExplored = CreateRT(w, h, RenderTextureFormat.ARGBFloat, "KJ_FogRT_Explored");
        rtExploredTemp = CreateRT(w, h, RenderTextureFormat.ARGBFloat, "KJ_FogRT_ExploredTemp");

        ClearRT(rtCurrent, Color.clear);
        ClearRT(rtExplored, Color.clear);
        ClearRT(rtExploredTemp, Color.clear);

        var maskShader = Shader.Find("Custom/KJ/FogMaskJC");
        var decayShader = Shader.Find("Custom/KJ/FogDecayJC");
        if (maskShader == null || decayShader == null)
        {
            Debug.LogError("[KJ_PlayFogManager] FogMask/FogDecay 셰이더를 찾을 수 없습니다.");
            return;
        }

        maskMaterial = new Material(maskShader) { hideFlags = HideFlags.DontSave };
        decayMaterial = new Material(decayShader) { hideFlags = HideFlags.DontSave };
        maskMaterial.SetFloat(SmoothEdgeId, maskSmoothEdge);

        Shader.SetGlobalTexture(CurrentTexGlobalId, rtCurrent);
        Shader.SetGlobalTexture(ExploredTexGlobalId, rtExplored);

        cmd = new CommandBuffer { name = "KJ_PlayFogManager" };
        Debug.Log($"[KJ_PlayFogManager] 초기화 완료 ({w}x{h})");
    }

    private static RenderTexture CreateRT(int w, int h, RenderTextureFormat fmt, string name)
    {
        var rt = new RenderTexture(w, h, 0, fmt, RenderTextureReadWrite.Linear)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
            enableRandomWrite = false,
        };
        rt.Create();
        return rt;
    }

    private static void ClearRT(RenderTexture rt, Color color)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(false, true, color);
        RenderTexture.active = prev;
    }

    private void OnDestroy()
    {
        if (rtCurrent != null) { rtCurrent.Release(); DestroyImmediate(rtCurrent); }
        if (rtExplored != null) { rtExplored.Release(); DestroyImmediate(rtExplored); }
        if (rtExploredTemp != null) { rtExploredTemp.Release(); DestroyImmediate(rtExploredTemp); }
        if (exploredTexCPU != null) DestroyImmediate(exploredTexCPU);
        if (maskMaterial != null) DestroyImmediate(maskMaterial);
        if (decayMaterial != null) DestroyImmediate(decayMaterial);
        cmd?.Release();
    }

    public void UpdatePlayerVisibility(Vector2Int playerGridPos, int sightRadiusCells)
    {
        AddVisibilitySource(playerGridPos, sightRadiusCells, true);
    }

    /// <summary>
    /// 가시성 소스를 추가합니다. 
    /// recordHistory가 true이면 gridVisitTurns에 기록되어 턴제 소멸 대상이 되며,
    /// false이면 실시간 rtCurrent에만 표시되어 소스가 사라지거나 비활성화되면 즉각 안개가 복구됩니다.
    /// </summary>
    public void AddVisibilitySource(Vector2Int gridPos, int radiusCells, bool recordHistory)
    {
        if (grid == null) return;

        var worldPos = KJ_PlayGridManager.GridToWorld(gridPos);
        visibilitySources.Add(new VisibilitySource 
        { 
            worldPos = new Vector2(worldPos.x, worldPos.z), 
            radius = radiusCells * KJ_PlayGridManager.CellSize 
        });

        if (recordHistory)
        {
            UpdateGridVisit(gridPos, radiusCells);
            grid.RevealArea(gridPos, radiusCells);
        }
    }

    public void ResetVisibilitySources()
    {
        visibilitySources.Clear();
    }

    private void UpdateGridVisit(Vector2Int center, int radius)
    {
        int w = grid.Width;
        int h = grid.Height;

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                if (x < 0 || x >= w || y < 0 || y >= h) continue;
                gridVisitTurns[y * w + x] = currentTurn;
            }
        }
        RefreshExploredTexture();
    }

    public void AdvanceTurn(int turn, int decayLimit)
    {
        Debug.Log($"[KJ_PlayFogManager] AdvanceTurn 호출: Turn {currentTurn} -> {turn}, DecayLimit: {decayLimit}");
        currentTurn = turn;
        maxDecayTurns = decayLimit;
        RefreshExploredTexture();
    }

    private void RefreshExploredTexture()
    {
        if (grid == null || exploredPixels == null) return;

        int w = grid.Width;
        int h = grid.Height;
        int exploredCount = 0;

        for (int i = 0; i < gridVisitTurns.Length; i++)
        {
            int age = currentTurn - gridVisitTurns[i];
            if (gridVisitTurns[i] >= 0 && age < maxDecayTurns)
            {
                exploredPixels[i] = Color.white; 
                exploredCount++;
            }
            else
            {
                exploredPixels[i] = Color.clear;
            }
        }

        exploredTexCPU.SetPixels(exploredPixels);
        exploredTexCPU.Apply();
        Graphics.Blit(exploredTexCPU, rtExplored);
    }

    private void LateUpdate()
    {
        if (grid == null || rtCurrent == null) return;
        if (maskMaterial == null || decayMaterial == null) return;

        cmd.Clear();

        if (!fogEnabled)
        {
            cmd.SetRenderTarget(rtCurrent);
            cmd.ClearRenderTarget(false, true, Color.white);
            Graphics.ExecuteCommandBuffer(cmd);
            return;
        }

        // rtCurrent를 비우고 시작 (이후 Loop에서 가산 블렌딩으로 채움)
        cmd.SetRenderTarget(rtCurrent);
        cmd.ClearRenderTarget(false, true, Color.clear);

        if (hasVisibilityData)
        {
            maskMaterial.SetFloat(SmoothEdgeId, maskSmoothEdge);
            foreach (var source in visibilitySources)
            {
                maskMaterial.SetVector(PlayerWorldPosId, new Vector4(source.worldPos.x, source.worldPos.y, 0, 0));
                maskMaterial.SetFloat(SightRadiusId, source.radius);
                cmd.Blit(Texture2D.blackTexture, rtCurrent, maskMaterial);
            }
        }

        cmd.SetGlobalVector(RestoreDelaysId, new Vector4(lowLayerDelay, midLayerDelay, highLayerDelay, 0f));
        cmd.SetGlobalFloat(RestoreDurationId, restoreDuration);
        cmd.SetGlobalFloat(FogHidableLowThresholdId, fogHidableLowThreshold);
        cmd.SetGlobalFloat(FogDeltaTimeId, 0f); // 실시간 감쇠 중단
        cmd.SetGlobalTexture(ExploredTexInputId, rtExplored);
        cmd.SetGlobalTexture(CurrentTexInputId, rtCurrent);

        Graphics.ExecuteCommandBuffer(cmd);

        Shader.SetGlobalTexture(ExploredTexGlobalId, rtExplored);
    }

    public void SetFogEnabled(bool enabled)
    {
        fogEnabled = enabled;
    }
}
