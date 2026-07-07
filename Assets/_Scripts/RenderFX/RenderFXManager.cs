using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 신설 260706] 렌더링 연출 전역 매니저. 1차 담당 = fog 렌더 게이트 + fog 프리셋 허브, 향후 전투 이펙트로 확장.
/// - Shift+F: fog 렌더 토글 치트 (BattleCheatController 패턴 — 디버그 패널과 무관하게 상시 작동)
/// - 씬 로드/언로드 시 FogRenderManager 존재 여부를 스캔해 FogRenderGate.SceneHasFog 갱신
///   → 탐사씬에서만 fog 패스가 발동하고, 로비/전투/타이틀에서는 자동 차단된다.
/// - FogPreset 적용: 셰이더 값은 fogMaterial에, 가시성/refog 값은 씬의 DH 매니저 2종에 런타임 반영.
///   머티리얼은 Awake에서 백업 후 OnDestroy(플레이 종료)에 복원 — DH 소유 .mat 에셋을 오염시키지 않는다.
///   튜닝 결과의 영구 저장소는 FogPreset SO 에셋 (플레이 중 편집해도 보존됨).
/// - 부트스트랩: Resources/RenderFX/RenderFXManager.prefab을 BeforeSceneLoad에 생성 (GameManager 패턴).
/// </summary>
[DisallowMultipleComponent]
public class RenderFXManager : MonoBehaviour
{
    private const string PrefabResourcePath = "RenderFX/RenderFXManager";

    public static RenderFXManager Instance { get; private set; }

    [Header("Fog Toggle")]
    [Tooltip("시작 시 fog 렌더 기본 상태. 플레이 중 인스펙터에서 바꿔도 즉시 반영된다.")]
    [SerializeField] private bool fogEnabledByDefault = true;
    [SerializeField] private bool logToggle = true;

    [Header("Fog Preset")]
    [Tooltip("적용할 프리셋. 플레이 중 이 SO를 인스펙터에서 편집하면 즉시 반영된다.")]
    [SerializeField] private FogPreset activePreset;
    [Tooltip("fog 합성 머티리얼 (DHFogOfWarMaterial). 셰이더 값 적용 + 백업/복원 대상.")]
    [SerializeField] private Material fogMaterial;
    [SerializeField] private bool applyPresetOnSceneLoad = true;

    private Material fogMaterialBackup;

    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int FogColorMidId = Shader.PropertyToID("_FogColorMid");
    private static readonly int FogColorHighId = Shader.PropertyToID("_FogColorHigh");
    private static readonly int FogDensityLowId = Shader.PropertyToID("_FogDensityLow");
    private static readonly int FogDensityMidId = Shader.PropertyToID("_FogDensityMid");
    private static readonly int FogDensityHighId = Shader.PropertyToID("_FogDensityHigh");
    private static readonly int NoiseScale1Id = Shader.PropertyToID("_NoiseScale1");
    private static readonly int NoiseScale2Id = Shader.PropertyToID("_NoiseScale2");
    private static readonly int NoiseScale3Id = Shader.PropertyToID("_NoiseScale3");
    private static readonly int FlowSpeed1Id = Shader.PropertyToID("_FlowSpeed1");
    private static readonly int FlowSpeed2Id = Shader.PropertyToID("_FlowSpeed2");
    private static readonly int FlowSpeed3Id = Shader.PropertyToID("_FlowSpeed3");
    private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int NoiseContrastId = Shader.PropertyToID("_NoiseContrast");
    private static readonly int HeightTransitionId = Shader.PropertyToID("_HeightTransition");
    private static readonly int FogCeilingYId = Shader.PropertyToID("_FogCeilingY");   // 구 DH 셰이더 폴백용
    private static readonly int FogLowTopYId = Shader.PropertyToID("_FogLowTopY");
    private static readonly int FogHighStartYId = Shader.PropertyToID("_FogHighStartY");
    private static readonly int BrightnessLowId = Shader.PropertyToID("_BrightnessLow");
    private static readonly int BrightnessMidId = Shader.PropertyToID("_BrightnessMid");
    private static readonly int BrightnessHighId = Shader.PropertyToID("_BrightnessHigh");
    private static readonly int CloudContrastId = Shader.PropertyToID("_CloudContrast");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int EdgeWidthWorldId = Shader.PropertyToID("_EdgeWidthWorld");
    private static readonly int EdgeNoiseStrengthId = Shader.PropertyToID("_EdgeNoiseStrength");
    private static readonly int EdgeNoiseScaleId = Shader.PropertyToID("_EdgeNoiseScale");
    private static readonly int EdgeNoiseSpeedId = Shader.PropertyToID("_EdgeNoiseSpeed");
    private static readonly int EdgeFadeWidthId = Shader.PropertyToID("_EdgeFadeWidth");
    private static readonly int EdgeLayerSpreadWorldId = Shader.PropertyToID("_EdgeLayerSpreadWorld");
    private static readonly int SightBoostWorldId = Shader.PropertyToID("_SightBoostWorld");
    private static readonly int SheetOpacityId = Shader.PropertyToID("_SheetOpacity");
    private static readonly int SheetHeightYId = Shader.PropertyToID("_SheetHeightY");
    private static readonly int SheetColorId = Shader.PropertyToID("_SheetColor");
    private static readonly int SheetEdgeShiftWorldId = Shader.PropertyToID("_SheetEdgeShiftWorld");
    private static readonly int SheetFadeWidthWorldId = Shader.PropertyToID("_SheetFadeWidthWorld");
    private static readonly int SheetGroundAlignId = Shader.PropertyToID("_SheetGroundAlign");
    private static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");
    private const string DebugModeKeyword = "_DEBUGMODE_ON";

    private static readonly int FogZoneTexId = Shader.PropertyToID("_FogZoneTex");
    private static readonly int FogZoneTexBoundId = Shader.PropertyToID("_FogZoneTexBound");
    private static readonly int FogCellSizeId = Shader.PropertyToID("_FogCellSize");
    private Texture2D fogZoneTexture;

    // 가시성 경계 SDF — 씬에 FogGridManager가 있는 동안만 활성 (FogOfWarHI 등고선 경계의 데이터원)
    private readonly FogDistanceField distanceField = new FogDistanceField();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        Debug.LogWarning($"[RenderFXManager] Resources/{PrefabResourcePath}.prefab 미발견 — 기본값으로 동적 생성합니다.");
        new GameObject("RenderFXManager").AddComponent<RenderFXManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameObject.name = "RenderFXManager";
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        FogRenderGate.UserEnabled = fogEnabledByDefault;

        if (fogMaterial != null)
            BackupFogMaterial();

        FogPreset.Changed += HandlePresetChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        RefreshSceneFogPresence();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;
        FogPreset.Changed -= HandlePresetChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        FogRenderGate.SceneHasFog = false;

        distanceField.Detach();
        ClearFogZones();
        RestoreFogMaterial();
    }

    private void Update()
    {
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shiftHeld && Input.GetKeyDown(KeyCode.F))
            ToggleFog();

        distanceField.RebuildIfDirty();
    }

    public void ToggleFog()
    {
        FogRenderGate.UserEnabled = !FogRenderGate.UserEnabled;
        if (logToggle)
            Debug.Log($"[RenderFXManager] fog 렌더 토글: {(FogRenderGate.UserEnabled ? "ON" : "OFF")}");
    }

    // ================= 프리셋 적용 =================

    /// <summary>프리셋을 활성화하고 머티리얼 + 씬 매니저에 즉시 적용.</summary>
    public void ApplyPreset(FogPreset preset)
    {
        if (preset == null) return;

        activePreset = preset;
        ApplyToMaterial(preset);
        ApplyToSceneManagers(preset);
        ApplyToDistanceField(preset);
    }

    /// <summary>
    /// 파티 시야반경(월드) — PartyFogRevealer.revealRadius 리플렉션 조회(DH 무수정 seam), 부재 시 기본 4셀.
    /// sightRangeMultiplier/sheetRangeMultiplier의 (계수−1)×반경 환산 공용 기반.
    /// </summary>
    private float GetSightRadiusWorld()
    {
        float radiusCells = 4f;
        var revealer = FindFirstObjectByType<PartyFogRevealer>();
        if (revealer != null)
        {
            var field = typeof(PartyFogRevealer).GetField("revealRadius", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) radiusCells = (int)field.GetValue(revealer);
        }

        float cellSize = Shader.GetGlobalFloat(FogCellSizeId);
        if (cellSize <= 0.0001f) cellSize = 1f;
        return radiusCells * cellSize;
    }

    private float ComputeSightBoostWorld(FogPreset p)
    {
        if (Mathf.Approximately(p.sightRangeMultiplier, 1f)) return 0f;
        return (p.sightRangeMultiplier - 1f) * GetSightRadiusWorld();
    }

    /// <summary>경계 라운딩(월드)을 셀 단위로 환산해 SDF 빌더에 전달. 셀 크기는 FogRenderManager가 푸시한 전역값 사용.</summary>
    private void ApplyToDistanceField(FogPreset p)
    {
        float cellSize = Shader.GetGlobalFloat(FogCellSizeId);
        if (cellSize <= 0.0001f) cellSize = 1f;
        distanceField.SetRounding(p.edgeRoundingWorld / cellSize);
    }

    private void ApplyToMaterial(FogPreset p)
    {
        if (fogMaterial == null) return;

        fogMaterial.SetColor(FogColorId, p.fogColor);
        if (fogMaterial.HasProperty(FogColorMidId))
        {
            fogMaterial.SetColor(FogColorMidId, p.fogColorMid);
            fogMaterial.SetColor(FogColorHighId, p.fogColorHigh);
        }
        fogMaterial.SetFloat(FogDensityLowId, p.fogDensityLow);
        fogMaterial.SetFloat(FogDensityMidId, p.fogDensityMid);
        fogMaterial.SetFloat(FogDensityHighId, p.fogDensityHigh);
        fogMaterial.SetFloat(NoiseScale1Id, p.noiseScaleBase);
        fogMaterial.SetFloat(NoiseScale2Id, p.noiseScaleDetail);
        fogMaterial.SetFloat(NoiseScale3Id, p.noiseScaleDistortion);
        fogMaterial.SetFloat(FlowSpeed1Id, p.flowSpeedBase);
        fogMaterial.SetFloat(FlowSpeed2Id, p.flowSpeedDetail);
        fogMaterial.SetFloat(FlowSpeed3Id, p.flowSpeedDistortion);
        fogMaterial.SetFloat(DistortionStrengthId, p.distortionStrength);
        fogMaterial.SetFloat(NoiseContrastId, p.noiseContrast);
        fogMaterial.SetFloat(HeightTransitionId, p.heightTransition);
        if (fogMaterial.HasProperty(FogLowTopYId))
        {
            fogMaterial.SetFloat(FogLowTopYId, p.fogLowTopY);
            fogMaterial.SetFloat(FogHighStartYId, p.fogHighStartY);
        }
        else if (fogMaterial.HasProperty(FogCeilingYId))
        {
            // 구 DH 셰이더 폴백: 단일 ceiling에 Low 상단 경계를 매핑
            fogMaterial.SetFloat(FogCeilingYId, p.fogLowTopY);
        }
        fogMaterial.SetFloat(BrightnessLowId, p.brightnessLow);
        fogMaterial.SetFloat(BrightnessMidId, p.brightnessMid);
        fogMaterial.SetFloat(BrightnessHighId, p.brightnessHigh);
        fogMaterial.SetFloat(CloudContrastId, p.cloudContrast);
        fogMaterial.SetFloat(EdgeSoftnessId, p.fallbackEdgeSoftness);
        // 셰이더 세대별 전용 프로퍼티 — 미보유 머티리얼에 유령 프로퍼티가 쌓이지 않게 가드
        if (fogMaterial.HasProperty(EdgeWidthWorldId))
        {
            fogMaterial.SetFloat(EdgeWidthWorldId, p.edgeWidthWorld);
            fogMaterial.SetFloat(EdgeNoiseStrengthId, p.edgeNoiseStrength);
            fogMaterial.SetFloat(EdgeNoiseScaleId, p.edgeNoiseScale);
            fogMaterial.SetFloat(EdgeNoiseSpeedId, p.edgeNoiseSpeed);
            fogMaterial.SetFloat(EdgeFadeWidthId, p.edgeFadeWidth);
            fogMaterial.SetFloat(EdgeLayerSpreadWorldId, p.edgeLayerSpreadWorld);
            fogMaterial.SetFloat(SightBoostWorldId, ComputeSightBoostWorld(p));
        }
        if (fogMaterial.HasProperty(SheetOpacityId))
        {
            fogMaterial.SetFloat(SheetOpacityId, p.sheetOpacity);
            fogMaterial.SetFloat(SheetHeightYId, p.sheetHeightY);
            fogMaterial.SetColor(SheetColorId, p.sheetColor);
            fogMaterial.SetFloat(SheetEdgeShiftWorldId,
                Mathf.Approximately(p.sheetRangeMultiplier, 1f) ? 0f : (p.sheetRangeMultiplier - 1f) * GetSightRadiusWorld());
            fogMaterial.SetFloat(SheetFadeWidthWorldId, p.sheetFadeWidthWorld);
            fogMaterial.SetFloat(SheetGroundAlignId, p.sheetGroundAlign);
        }

        fogMaterial.SetFloat(DebugModeId, p.debugMode ? 1f : 0f);
        if (p.debugMode) fogMaterial.EnableKeyword(DebugModeKeyword);
        else fogMaterial.DisableKeyword(DebugModeKeyword);
    }

    /// <summary>
    /// 씬의 DH 매니저 2종에 가시성/refog 값을 반영. 필드가 private라 리플렉션 사용
    /// (DH 파일 무수정 seam — 씬 오브젝트 값이라 플레이 종료 시 자동 원복).
    /// </summary>
    private void ApplyToSceneManagers(FogPreset p)
    {
        var render = FindFirstObjectByType<FogRenderManager>();
        if (render != null)
        {
            bool ok = SetPrivateField(render, "unexploredValue", p.unexploredValue)
                    & SetPrivateField(render, "foggedValue", p.foggedValue)
                    & SetPrivateField(render, "visibleValue", p.visibleValue);
            if (ok)
                render.MarkDirty();
            else
                Debug.LogWarning("[RenderFXManager] FogRenderManager 필드명 불일치 — DH 코드 변경 여부 확인 필요");
        }

        var grid = FindFirstObjectByType<FogGridManager>();
        if (grid != null)
        {
            bool ok = SetPrivateField(grid, "enableRefogByDay", p.enableRefogByDay)
                    & SetPrivateField(grid, "refogDelayDays", Mathf.Max(1, p.refogDelayDays));
            if (!ok)
                Debug.LogWarning("[RenderFXManager] FogGridManager 필드명 불일치 — DH 코드 변경 여부 확인 필요");
        }
    }

    private static bool SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return false;
        field.SetValue(target, value);
        return true;
    }

    private void HandlePresetChanged(FogPreset changed)
    {
        if (!Application.isPlaying) return;
        if (changed != activePreset) return;

        ApplyToMaterial(changed);
        ApplyToSceneManagers(changed);
        ApplyToDistanceField(changed);
    }

    // ================= 영역 지정 안개 제어 (FogZone) =================

    /// <summary>
    /// 그리드 영역별 안개 제어를 적용. 씬의 FogRenderManager 그리드 크기로 zone 텍스처를 구워
    /// 전역 _FogZoneTex로 푸시한다 (FogOfWarHI 셰이더 소비). 지정 영역 밖은 기본(밀도 1, 전 레이어 활성).
    /// 씬 전환 시 자동 해제되므로 씬별로 재설정해야 한다.
    /// </summary>
    public bool SetFogZones(System.Collections.Generic.IReadOnlyList<FogZone> zones)
    {
        var render = FindFirstObjectByType<FogRenderManager>();
        if (render == null)
        {
            Debug.LogWarning("[RenderFXManager] SetFogZones 실패 — 씬에 FogRenderManager 없음 (탐사씬 아님)");
            return false;
        }

        Vector2Int size = render.GridMax - render.GridMin + Vector2Int.one;
        if (size.x < 1 || size.y < 1) return false;

        if (fogZoneTexture == null || fogZoneTexture.width != size.x || fogZoneTexture.height != size.y)
        {
            if (fogZoneTexture != null) Destroy(fogZoneTexture);
            fogZoneTexture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, true)
            {
                name = "FogZoneTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        var pixels = new Color[size.x * size.y];
        var defaultPixel = new Color(1f, 1f, 1f, 1f);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = defaultPixel;

        if (zones != null)
        {
            for (int z = 0; z < zones.Count; z++)
            {
                var zone = zones[z];
                var c = new Color(
                    Mathf.Clamp01(zone.densityMultiplier),
                    zone.lowEnabled ? 1f : 0f,
                    zone.midEnabled ? 1f : 0f,
                    zone.highEnabled ? 1f : 0f);

                int xMin = Mathf.Max(zone.cells.xMin, 0);
                int yMin = Mathf.Max(zone.cells.yMin, 0);
                int xMax = Mathf.Min(zone.cells.xMax, size.x);
                int yMax = Mathf.Min(zone.cells.yMax, size.y);
                for (int y = yMin; y < yMax; y++)
                    for (int x = xMin; x < xMax; x++)
                        pixels[y * size.x + x] = c;
            }
        }

        fogZoneTexture.SetPixels(pixels);
        fogZoneTexture.Apply(false, false);
        Shader.SetGlobalTexture(FogZoneTexId, fogZoneTexture);
        Shader.SetGlobalFloat(FogZoneTexBoundId, 1f);
        return true;
    }

    /// <summary>영역 지정 안개 제어 해제 — 셰이더가 기본값(전 영역 밀도 1)으로 복귀.</summary>
    public void ClearFogZones()
    {
        Shader.SetGlobalFloat(FogZoneTexBoundId, 0f);
        if (fogZoneTexture != null)
        {
            Destroy(fogZoneTexture);
            fogZoneTexture = null;
        }
    }

    // ================= 머티리얼 백업/복원 =================

    private void BackupFogMaterial()
    {
        fogMaterialBackup = new Material(fogMaterial);
    }

    /// <summary>플레이 종료 시 DH 소유 .mat을 원상 복구 — 커밋 에셋 무오염 보장.</summary>
    private void RestoreFogMaterial()
    {
        if (fogMaterial == null || fogMaterialBackup == null) return;

        fogMaterial.CopyPropertiesFromMaterial(fogMaterialBackup);
        fogMaterial.shaderKeywords = fogMaterialBackup.shaderKeywords;
        Destroy(fogMaterialBackup);
        fogMaterialBackup = null;
    }

    // ================= 씬 게이트 =================

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshSceneFogPresence();
    private void HandleSceneUnloaded(Scene scene) => RefreshSceneFogPresence();

    private void RefreshSceneFogPresence()
    {
        FogRenderGate.SceneHasFog = FindFirstObjectByType<FogRenderManager>() != null;

        // zone은 씬 종속 데이터 — 씬 구성이 바뀌면 해제 (필요 씬에서 재설정)
        ClearFogZones();

        // 경계 SDF: fog 씬에서만 활성. Attach가 기존 구독 해제 후 재구독 + 초기 빌드 예약
        distanceField.Attach(FogRenderGate.SceneHasFog ? FindFirstObjectByType<FogGridManager>() : null);

        if (FogRenderGate.SceneHasFog && applyPresetOnSceneLoad)
            ApplyPreset(activePreset);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 플레이 중 인스펙터 토글 즉시 반영용. 에딧 모드에서는 static을 건드리지 않는다.
        if (Application.isPlaying && Instance == this)
            FogRenderGate.UserEnabled = fogEnabledByDefault;
    }

    [ContextMenu("머티리얼 현재값 → 프리셋 캡처")]
    private void CaptureMaterialToPreset()
    {
        if (fogMaterial == null || activePreset == null)
        {
            Debug.LogWarning("[RenderFXManager] 캡처 실패 — fogMaterial/activePreset 결선 확인");
            return;
        }

        var p = activePreset;
        p.fogColor = fogMaterial.GetColor(FogColorId);
        if (fogMaterial.HasProperty(FogColorMidId))
        {
            p.fogColorMid = fogMaterial.GetColor(FogColorMidId);
            p.fogColorHigh = fogMaterial.GetColor(FogColorHighId);
        }
        p.fogDensityLow = fogMaterial.GetFloat(FogDensityLowId);
        p.fogDensityMid = fogMaterial.GetFloat(FogDensityMidId);
        p.fogDensityHigh = fogMaterial.GetFloat(FogDensityHighId);
        p.noiseScaleBase = fogMaterial.GetFloat(NoiseScale1Id);
        p.noiseScaleDetail = fogMaterial.GetFloat(NoiseScale2Id);
        p.noiseScaleDistortion = fogMaterial.GetFloat(NoiseScale3Id);
        p.flowSpeedBase = fogMaterial.GetFloat(FlowSpeed1Id);
        p.flowSpeedDetail = fogMaterial.GetFloat(FlowSpeed2Id);
        p.flowSpeedDistortion = fogMaterial.GetFloat(FlowSpeed3Id);
        p.distortionStrength = fogMaterial.GetFloat(DistortionStrengthId);
        p.noiseContrast = fogMaterial.GetFloat(NoiseContrastId);
        p.heightTransition = fogMaterial.GetFloat(HeightTransitionId);
        if (fogMaterial.HasProperty(FogLowTopYId))
        {
            p.fogLowTopY = fogMaterial.GetFloat(FogLowTopYId);
            p.fogHighStartY = fogMaterial.GetFloat(FogHighStartYId);
        }
        p.brightnessLow = fogMaterial.GetFloat(BrightnessLowId);
        p.brightnessMid = fogMaterial.GetFloat(BrightnessMidId);
        p.brightnessHigh = fogMaterial.GetFloat(BrightnessHighId);
        p.cloudContrast = fogMaterial.GetFloat(CloudContrastId);
        p.fallbackEdgeSoftness = fogMaterial.GetFloat(EdgeSoftnessId);
        if (fogMaterial.HasProperty(EdgeWidthWorldId))
        {
            p.edgeWidthWorld = fogMaterial.GetFloat(EdgeWidthWorldId);
            p.edgeNoiseStrength = fogMaterial.GetFloat(EdgeNoiseStrengthId);
            p.edgeNoiseScale = fogMaterial.GetFloat(EdgeNoiseScaleId);
            p.edgeNoiseSpeed = fogMaterial.GetFloat(EdgeNoiseSpeedId);
            p.edgeFadeWidth = fogMaterial.GetFloat(EdgeFadeWidthId);
            p.edgeLayerSpreadWorld = fogMaterial.GetFloat(EdgeLayerSpreadWorldId);
        }
        if (fogMaterial.HasProperty(SheetOpacityId))
        {
            p.sheetOpacity = fogMaterial.GetFloat(SheetOpacityId);
            p.sheetHeightY = fogMaterial.GetFloat(SheetHeightYId);
            p.sheetColor = fogMaterial.GetColor(SheetColorId);
            p.sheetFadeWidthWorld = fogMaterial.GetFloat(SheetFadeWidthWorldId);
            p.sheetGroundAlign = fogMaterial.GetFloat(SheetGroundAlignId);
            // sheetRangeMultiplier는 시야반경 환산값이라 역산 생략 (sightRangeMultiplier와 동일 정책)
        }
        p.debugMode = fogMaterial.GetFloat(DebugModeId) > 0.5f;

        UnityEditor.EditorUtility.SetDirty(p);
        Debug.Log($"[RenderFXManager] 머티리얼 값 캡처 완료 → {p.name}");
    }

    [ContextMenu("프리셋 → 머티리얼 굽기 (에딧 모드 전용, 에셋 영구 반영)")]
    private void BakePresetToMaterial()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[RenderFXManager] 굽기는 에딧 모드에서만 — 플레이 중 값은 종료 시 복원됩니다.");
            return;
        }
        if (fogMaterial == null || activePreset == null)
        {
            Debug.LogWarning("[RenderFXManager] 굽기 실패 — fogMaterial/activePreset 결선 확인");
            return;
        }

        ApplyToMaterial(activePreset);
        UnityEditor.EditorUtility.SetDirty(fogMaterial);
        Debug.Log($"[RenderFXManager] 프리셋 '{activePreset.name}' → {fogMaterial.name} 굽기 완료 (커밋 대상 변경 발생)");
    }
#endif
}
