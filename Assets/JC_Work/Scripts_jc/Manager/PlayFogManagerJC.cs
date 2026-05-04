using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 전장의 안개 매니저. GameManager 하위에 배치.
///
/// GPU 기반 RT 2장 운용 방식:
///   - RT_Current  (R8)      : 매 프레임 FogMaskJC 셰이더로 플레이어 시야 원형 마스크 렌더
///   - RT_Explored (RGBA32)  : FogDecayJC 셰이더로 매 프레임 감쇠 + current 병합.
///                             RGB 3채널에 레이어별 복원 진행도 저장 (R=low 5s, G=mid 7s, B=high 9s)
///
/// 게임 로직용 grid.visibility(GridCell)는 별도로 유지.
/// </summary>
public class PlayFogManagerJC : MonoBehaviour
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

    // === 런타임 상태 ===
    private PlayGridManagerJC grid;
    private RenderTexture rtCurrent;
    private RenderTexture rtExplored;
    private RenderTexture rtExploredTemp;

    private Material maskMaterial;
    private Material decayMaterial;
    private CommandBuffer cmd;

    private Vector2 playerWorldPos;
    private float sightRadiusWorld;
    private bool hasPlayerPos;

    // === Shader property IDs ===
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
        grid = FindFirstObjectByType<PlayGridManagerJC>();
        if (grid == null)
        {
            Debug.LogError("[PlayFogManagerJC] PlayGridManagerJC를 찾을 수 없습니다");
            return;
        }

        int w = grid.Width;
        int h = grid.Height;

        if (w <= 0 || h <= 0)
        {
            Debug.LogError($"[PlayFogManagerJC] 잘못된 그리드 크기: {w}x{h}. 초기화 중단.");
            return;
        }

        // RT_Current: R8, 현재 시야 마스크 (0/1만 저장하므로 8bit로 충분)
        rtCurrent = CreateRT(w, h, RenderTextureFormat.R8, "FogRT_Current");

        // RT_Explored 2장 (ping-pong): ARGBFloat
        // 정밀도 무한, sRGB 변환 없음 → 선형 감쇠 계산이 정확히 누적됨
        // ARGB32(8bit)로 하면 sRGB/Linear 해석 불일치로 1.0 근처에서 감쇠가 멈추는 현상 발생
        rtExplored = CreateRT(w, h, RenderTextureFormat.ARGBFloat, "FogRT_Explored");
        rtExploredTemp = CreateRT(w, h, RenderTextureFormat.ARGBFloat, "FogRT_ExploredTemp");

        // 초기 클리어 (모두 black = 완전 안개)
        ClearRT(rtCurrent, Color.clear);
        ClearRT(rtExplored, Color.clear);
        ClearRT(rtExploredTemp, Color.clear);

        // Material 준비 (셰이더 자동 Find)
        var maskShader = Shader.Find("Custom/JC/FogMask");
        var decayShader = Shader.Find("Custom/JC/FogDecay");
        if (maskShader == null || decayShader == null)
        {
            Debug.LogError("[PlayFogManagerJC] FogMask/FogDecay 셰이더를 찾을 수 없습니다. Assets_jc 폴더의 셰이더 확인 필요.");
            return;
        }
        maskMaterial = new Material(maskShader) { hideFlags = HideFlags.DontSave };
        decayMaterial = new Material(decayShader) { hideFlags = HideFlags.DontSave };
        maskMaterial.SetFloat(SmoothEdgeId, maskSmoothEdge);

        // 글로벌 셰이더 텍스처 세팅
        Shader.SetGlobalTexture(CurrentTexGlobalId, rtCurrent);
        Shader.SetGlobalTexture(ExploredTexGlobalId, rtExplored);

        cmd = new CommandBuffer { name = "PlayFogManagerJC" };

        Debug.Log($"[PlayFogManagerJC] 초기화 완료 ({w}x{h}, RT 2장 GPU 방식, 복원 delay {lowLayerDelay}/{midLayerDelay}/{highLayerDelay}s + duration {restoreDuration}s)");
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
        if (maskMaterial != null) DestroyImmediate(maskMaterial);
        if (decayMaterial != null) DestroyImmediate(decayMaterial);
        cmd?.Release();
    }

    /// <summary>
    /// 플레이어 위치/시야 반경 갱신.
    /// PlayerControllerJC에서 이동 시 호출.
    /// 그리드 데이터(게임 로직용)도 함께 갱신한다.
    /// </summary>
    public void UpdatePlayerVisibility(Vector2Int playerGridPos, int sightRadiusCells)
    {
        if (grid == null) return;

        // 월드 좌표로 변환 (셀 중심)
        var worldPos = PlayGridManagerJC.GridToWorld(playerGridPos);
        playerWorldPos = new Vector2(worldPos.x, worldPos.z);
        sightRadiusWorld = sightRadiusCells * PlayGridManagerJC.CellSize;
        hasPlayerPos = true;

        // 게임 로직용 그리드 셀 visibility도 갱신 (discrete)
        grid.RevealArea(playerGridPos, sightRadiusCells);
    }

    private void LateUpdate()
    {
        if (grid == null || rtCurrent == null) return;
        if (maskMaterial == null || decayMaterial == null) return;

        cmd.Clear();

        // === 안개 Off 모드: RT_Current를 완전 1로 채우고 종료 ===
        if (!fogEnabled)
        {
            cmd.SetRenderTarget(rtCurrent);
            cmd.ClearRenderTarget(false, true, Color.white);
            Graphics.ExecuteCommandBuffer(cmd);
            return;
        }

        // === 1) RT_Current 갱신 (원형 시야 마스크) ===
        if (hasPlayerPos)
        {
            maskMaterial.SetVector(PlayerWorldPosId, new Vector4(playerWorldPos.x, playerWorldPos.y, 0, 0));
            maskMaterial.SetFloat(SightRadiusId, sightRadiusWorld);
            maskMaterial.SetFloat(SmoothEdgeId, maskSmoothEdge);
            // source는 blit용 placeholder (material에서 _MainTex 미사용)
            cmd.Blit(Texture2D.blackTexture, rtCurrent, maskMaterial);
        }
        else
        {
            // 플레이어 위치 없음 → 완전 안개
            cmd.SetRenderTarget(rtCurrent);
            cmd.ClearRenderTarget(false, true, Color.clear);
        }

        // === 2) RT_Explored 갱신 (대기 후 감쇠 + current 병합, ping-pong) ===
        // FogDecayJC는 A채널에 누적된 경과시간(elapsed)을 사용해 레이어별로
        // "delay 초만큼 유지 → duration 초에 걸쳐 선형 복원" 동작을 수행한다.
        float dt = Time.deltaTime;

        cmd.SetGlobalVector(RestoreDelaysId, new Vector4(lowLayerDelay, midLayerDelay, highLayerDelay, 0f));
        cmd.SetGlobalFloat(RestoreDurationId, restoreDuration);
        cmd.SetGlobalFloat(FogHidableLowThresholdId, fogHidableLowThreshold);
        cmd.SetGlobalFloat(FogDeltaTimeId, dt);
        cmd.SetGlobalTexture(ExploredTexInputId, rtExplored);
        cmd.SetGlobalTexture(CurrentTexInputId, rtCurrent);

        // source는 placeholder (셰이더에서 _MainTex 미사용)
        cmd.Blit(Texture2D.blackTexture, rtExploredTemp, decayMaterial);

        Graphics.ExecuteCommandBuffer(cmd);

        // Ping-pong swap
        var swap = rtExplored;
        rtExplored = rtExploredTemp;
        rtExploredTemp = swap;

        // 글로벌 텍스처 재설정 (rtExplored 레퍼런스가 swap됐으므로)
        Shader.SetGlobalTexture(ExploredTexGlobalId, rtExplored);
    }

    /// <summary>
    /// 외부에서 안개 토글. Inspector의 fogEnabled와 연동.
    /// </summary>
    public void SetFogEnabled(bool enabled)
    {
        fogEnabled = enabled;
    }
}
