using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 신설 260706 / 260714 상태별 2레이어] 렌더링 연출 전역 매니저. 1차 담당 = fog 렌더 게이트 + fog 프리셋 허브.
/// - Shift+F: fog 렌더 토글 치트 (BattleCheatController 패턴 — 디버그 패널과 무관하게 상시 작동)
/// - 씬 로드/언로드 시 FogRenderManager 존재 여부를 스캔해 FogRenderGate.SceneHasFog 갱신
///   → 탐사씬에서만 fog 패스가 발동하고, 로비/전투/타이틀에서는 자동 차단된다.
/// - FogPreset 적용: unexplored/fogged 두 레이어(FogLayerSettings)를 각각 무접두/_Fg* 셰이더
///   프로퍼티군에 반영. 시야 반경(sightRadiusCells>0)은 DH PartyFogRevealer.revealRadius에
///   주입해 탐사 로직과 렌더 경계가 단일 소스로 동반된다(260806 B안).
///   머티리얼은 Awake에서 백업 후 OnDestroy(플레이 종료)에 복원 — 커밋 에셋 무오염.
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
    [Tooltip("fog 합성 머티리얼 (FogOfWarHI). 셰이더 값 적용 + 백업/복원 대상.")]
    [SerializeField] private Material fogMaterial;
    [SerializeField] private bool applyPresetOnSceneLoad = true;

    private Material fogMaterialBackup;

    /// <summary>레이어 1벌(무접두=Unexplored / "Fg"=Fogged)의 셰이더 프로퍼티 ID 묶음.</summary>
    private readonly struct LayerIds
    {
        public readonly int FogColor, FogColorMid, FogColorHigh;
        public readonly int DensityLow, DensityMid, DensityHigh;
        public readonly int Noise1, Noise2, Noise3, Flow1, Flow2, Flow3, Distortion, Contrast;
        public readonly int HeightTransition, LowTopY, HighStartY;
        public readonly int BrightLow, BrightMid, BrightHigh, CloudContrast, CloudCoverage, CloudDensityEffect;
        public readonly int SheetOpacity, SheetHeightY, SheetColor, SheetEdgeShift, SheetFadeWidth, SheetGroundAlign;

        public LayerIds(string prefix)
        {
            FogColor = Shader.PropertyToID("_" + prefix + "FogColor");
            FogColorMid = Shader.PropertyToID("_" + prefix + "FogColorMid");
            FogColorHigh = Shader.PropertyToID("_" + prefix + "FogColorHigh");
            DensityLow = Shader.PropertyToID("_" + prefix + "FogDensityLow");
            DensityMid = Shader.PropertyToID("_" + prefix + "FogDensityMid");
            DensityHigh = Shader.PropertyToID("_" + prefix + "FogDensityHigh");
            Noise1 = Shader.PropertyToID("_" + prefix + "NoiseScale1");
            Noise2 = Shader.PropertyToID("_" + prefix + "NoiseScale2");
            Noise3 = Shader.PropertyToID("_" + prefix + "NoiseScale3");
            Flow1 = Shader.PropertyToID("_" + prefix + "FlowSpeed1");
            Flow2 = Shader.PropertyToID("_" + prefix + "FlowSpeed2");
            Flow3 = Shader.PropertyToID("_" + prefix + "FlowSpeed3");
            Distortion = Shader.PropertyToID("_" + prefix + "DistortionStrength");
            Contrast = Shader.PropertyToID("_" + prefix + "NoiseContrast");
            HeightTransition = Shader.PropertyToID("_" + prefix + "HeightTransition");
            LowTopY = Shader.PropertyToID("_" + prefix + "FogLowTopY");
            HighStartY = Shader.PropertyToID("_" + prefix + "FogHighStartY");
            BrightLow = Shader.PropertyToID("_" + prefix + "BrightnessLow");
            BrightMid = Shader.PropertyToID("_" + prefix + "BrightnessMid");
            BrightHigh = Shader.PropertyToID("_" + prefix + "BrightnessHigh");
            CloudContrast = Shader.PropertyToID("_" + prefix + "CloudContrast");
            CloudCoverage = Shader.PropertyToID("_" + prefix + "CloudCoverage");
            CloudDensityEffect = Shader.PropertyToID("_" + prefix + "CloudDensityEffect");
            SheetOpacity = Shader.PropertyToID("_" + prefix + "SheetOpacity");
            SheetHeightY = Shader.PropertyToID("_" + prefix + "SheetHeightY");
            SheetColor = Shader.PropertyToID("_" + prefix + "SheetColor");
            SheetEdgeShift = Shader.PropertyToID("_" + prefix + "SheetEdgeShiftWorld");
            SheetFadeWidth = Shader.PropertyToID("_" + prefix + "SheetFadeWidthWorld");
            SheetGroundAlign = Shader.PropertyToID("_" + prefix + "SheetGroundAlign");
        }
    }

    private static readonly LayerIds UeIds = new LayerIds("");
    private static readonly LayerIds FgIds = new LayerIds("Fg");

    private static readonly int FogCeilingYId = Shader.PropertyToID("_FogCeilingY");   // 구 DH 셰이더 폴백용
    private static readonly int EdgeWidthWorldId = Shader.PropertyToID("_EdgeWidthWorld");
    private static readonly int EdgeNoiseStrengthId = Shader.PropertyToID("_EdgeNoiseStrength");
    private static readonly int EdgeNoiseScaleId = Shader.PropertyToID("_EdgeNoiseScale");
    private static readonly int EdgeNoiseSpeedId = Shader.PropertyToID("_EdgeNoiseSpeed");
    private static readonly int EdgeFadeWidthId = Shader.PropertyToID("_EdgeFadeWidth");
    private static readonly int EdgeLayerSpreadWorldId = Shader.PropertyToID("_EdgeLayerSpreadWorld");
    private static readonly int StateBlendWidthWorldId = Shader.PropertyToID("_StateBlendWidthWorld");
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

        distanceField.Tick(Time.deltaTime);
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
        ApplySightRadius(preset, resetOnChange: false);
        ApplyToMaterial(preset);
        ApplyToDistanceField(preset);
    }

    /// <summary>
    /// 시야 반경 단일 소스 주입(260806 B안) — 프리셋 sightRadiusCells(>0)를 DH
    /// PartyFogRevealer.revealRadius에 반영한다. 탐사 로직(이동 가능 범위)과
    /// 안개 경계가 같은 값에서 파생되어 항상 일치한다.
    /// resetOnChange=true(프리셋 라이브 튜닝 경로)면 안개를 리셋 후 재구성해
    /// 반경 축소도 즉시 순수하게 보인다. 씬 로드 주입 경로는 false —
    /// 세이브에서 복원된 탐사 이력을 지우면 안 되므로 가산 리빌만 한다.
    /// </summary>
    private void ApplySightRadius(FogPreset p, bool resetOnChange)
    {
        if (p.shared.sightRadiusCells <= 0) return;

        var revealer = FindFirstObjectByType<PartyFogRevealer>();
        if (revealer == null) return;

        if (revealer.RevealRadius == p.shared.sightRadiusCells) return;

        revealer.SetRevealRadius(p.shared.sightRadiusCells);

        if (!Application.isPlaying) return;

        if (resetOnChange)
            ResetFogAndReveal();
        else
            revealer.RevealAllCurrentPartyPositions();
    }

    /// <summary>
    /// [튜닝 보조] 안개 상태 전체를 지우고 현재 상태(파티 위치·점령 거점·HeroUnion) 기준으로
    /// 재구성한다 — 프리셋 인스펙터에서 시야 반경을 바꾸면 자동 호출(라이브 튜닝 경로 한정).
    /// ⚠️ 탐사 이동 이력이 소실되고 그 상태가 세이브에도 반영된다 — 튜닝 세션 전용.
    /// 재리빌 순서는 DHFogProgressApplier.RevealCurrentContext와 동일(구역 진입 안내 분기 포함).
    /// </summary>
    private void ResetFogAndReveal()
    {
        if (!Application.isPlaying) return;

        var fogGrid = FindFirstObjectByType<FogGridManager>();
        if (fogGrid == null) return;

        fogGrid.ClearFogData();

        if (ZoneEntryGuidanceController.IsActiveOrStoredActive)
        {
            ZoneEntryGuidanceController.ApplyStoredGuidanceIfNeeded()?.RevealAllowedPathCells();
        }
        else
        {
            FindFirstObjectByType<PartyFogRevealer>()?.RevealAllCurrentPartyPositions();
            FindFirstObjectByType<OutpostFogRevealer>()?.RevealAllClaimedOutposts();
            FindFirstObjectByType<HeroUnionFogRevealer>()?.RevealAllHeroUnions();
        }

        // 동기 즉시 반영(Snap) — 프레임 콜백을 기다리지 않고, 걷힘 연출도 생략한다.
        // 시야 리셋은 맵 전역이 바뀌는 튜닝 조작이라 연출을 걸면 대규모 스윕이 된다.
        distanceField.SnapToTarget();
    }

    /// <summary>
    /// 파티 시야반경(월드) — PartyFogRevealer.RevealRadius 조회, 부재 시 기본 4셀.
    /// sheetRangeMultiplier의 (계수−1)×반경 환산 기반.
    /// </summary>
    private float GetSightRadiusWorld()
    {
        float radiusCells = 4f;
        var revealer = FindFirstObjectByType<PartyFogRevealer>();
        if (revealer != null)
            radiusCells = revealer.RevealRadius;

        float cellSize = Shader.GetGlobalFloat(FogCellSizeId);
        if (cellSize <= 0.0001f) cellSize = 1f;
        return radiusCells * cellSize;
    }

    /// <summary>걷힘 속도 슬라이더(0~10) 1당 월드 속도 — 5 ≈ 종전 3월드유닛/초 매핑.</summary>
    private const float RevealSpeedUnitWorld = 0.6f;

    /// <summary>경계 라운딩·층별 걷힘 속도(월드)를 셀 단위로 환산해 SDF 빌더에 전달. 셀 크기는 FogRenderManager가 푸시한 전역값 사용.</summary>
    private void ApplyToDistanceField(FogPreset p)
    {
        float cellSize = Shader.GetGlobalFloat(FogCellSizeId);
        if (cellSize <= 0.0001f) cellSize = 1f;
        distanceField.SetRounding(p.shared.edgeRoundingWorld / cellSize);
        distanceField.SetRevealSpeeds(
            p.shared.revealSpeedUnexplored * RevealSpeedUnitWorld / cellSize,
            p.shared.revealSpeedFogged * RevealSpeedUnitWorld / cellSize,
            p.shared.revealSpeedSheet * RevealSpeedUnitWorld / cellSize);
    }

    private void ApplyToMaterial(FogPreset p)
    {
        if (fogMaterial == null) return;

        ApplyLayerToMaterial(p.unexplored, UeIds, legacyFallback: true);
        if (fogMaterial.HasProperty(FgIds.DensityLow))
            ApplyLayerToMaterial(p.fogged, FgIds, legacyFallback: false);

        // 셰이더 세대별 전용 프로퍼티 — 미보유 머티리얼에 유령 프로퍼티가 쌓이지 않게 가드
        if (fogMaterial.HasProperty(EdgeWidthWorldId))
        {
            fogMaterial.SetFloat(EdgeWidthWorldId, p.shared.edgeWidthWorld);
            fogMaterial.SetFloat(EdgeNoiseStrengthId, p.shared.edgeNoiseStrength);
            fogMaterial.SetFloat(EdgeNoiseScaleId, p.shared.edgeNoiseScale);
            fogMaterial.SetFloat(EdgeNoiseSpeedId, p.shared.edgeNoiseSpeed);
            fogMaterial.SetFloat(EdgeFadeWidthId, p.shared.edgeFadeWidth);
            fogMaterial.SetFloat(EdgeLayerSpreadWorldId, p.shared.edgeLayerSpreadWorld);
            if (fogMaterial.HasProperty(StateBlendWidthWorldId))
                fogMaterial.SetFloat(StateBlendWidthWorldId, p.shared.stateBlendWidthWorld);
        }

        fogMaterial.SetFloat(DebugModeId, p.shared.debugMode ? 1f : 0f);
        if (p.shared.debugMode) fogMaterial.EnableKeyword(DebugModeKeyword);
        else fogMaterial.DisableKeyword(DebugModeKeyword);
    }

    /// <summary>레이어 1벌을 해당 프로퍼티군에 반영. legacyFallback=구 DH 셰이더(_FogCeilingY 등) 호환은 base군만.</summary>
    private void ApplyLayerToMaterial(FogPreset.FogLayerSettings s, in LayerIds ids, bool legacyFallback)
    {
        fogMaterial.SetColor(ids.FogColor, s.fogColor);
        if (fogMaterial.HasProperty(ids.FogColorMid))
        {
            fogMaterial.SetColor(ids.FogColorMid, s.fogColorMid);
            fogMaterial.SetColor(ids.FogColorHigh, s.fogColorHigh);
        }
        fogMaterial.SetFloat(ids.DensityLow, s.fogDensityLow);
        fogMaterial.SetFloat(ids.DensityMid, s.fogDensityMid);
        fogMaterial.SetFloat(ids.DensityHigh, s.fogDensityHigh);
        fogMaterial.SetFloat(ids.Noise1, s.noiseScaleBase);
        fogMaterial.SetFloat(ids.Noise2, s.noiseScaleDetail);
        fogMaterial.SetFloat(ids.Noise3, s.noiseScaleDistortion);
        fogMaterial.SetFloat(ids.Flow1, s.flowSpeedBase);
        fogMaterial.SetFloat(ids.Flow2, s.flowSpeedDetail);
        fogMaterial.SetFloat(ids.Flow3, s.flowSpeedDistortion);
        fogMaterial.SetFloat(ids.Distortion, s.distortionStrength);
        fogMaterial.SetFloat(ids.Contrast, s.noiseContrast);
        fogMaterial.SetFloat(ids.HeightTransition, s.heightTransition);
        if (fogMaterial.HasProperty(ids.LowTopY))
        {
            fogMaterial.SetFloat(ids.LowTopY, s.fogLowTopY);
            fogMaterial.SetFloat(ids.HighStartY, s.fogHighStartY);
        }
        else if (legacyFallback && fogMaterial.HasProperty(FogCeilingYId))
        {
            // 구 DH 셰이더 폴백: 단일 ceiling에 Low 상단 경계를 매핑
            fogMaterial.SetFloat(FogCeilingYId, s.fogLowTopY);
        }
        fogMaterial.SetFloat(ids.BrightLow, s.brightnessLow);
        fogMaterial.SetFloat(ids.BrightMid, s.brightnessMid);
        fogMaterial.SetFloat(ids.BrightHigh, s.brightnessHigh);
        fogMaterial.SetFloat(ids.CloudContrast, s.cloudContrast);
        if (fogMaterial.HasProperty(ids.CloudCoverage))
        {
            fogMaterial.SetFloat(ids.CloudCoverage, s.cloudCoverage);
            fogMaterial.SetFloat(ids.CloudDensityEffect, s.cloudDensityEffect);
        }
        if (fogMaterial.HasProperty(ids.SheetOpacity))
        {
            fogMaterial.SetFloat(ids.SheetOpacity, s.sheetOpacity);
            fogMaterial.SetFloat(ids.SheetHeightY, s.sheetHeightY);
            fogMaterial.SetColor(ids.SheetColor, s.sheetColor);
            fogMaterial.SetFloat(ids.SheetEdgeShift,
                Mathf.Approximately(s.sheetRangeMultiplier, 1f) ? 0f : (s.sheetRangeMultiplier - 1f) * GetSightRadiusWorld());
            fogMaterial.SetFloat(ids.SheetFadeWidth, s.sheetFadeWidthWorld);
            fogMaterial.SetFloat(ids.SheetGroundAlign, s.sheetGroundAlign);
        }
    }

    // DH 씬 컴포넌트로의 리플렉션 푸시는 260714 전량 은퇴, 비-SDF 폴백 경로는 260806 T2로 철거.
    // refog는 게임 규칙(데이터)이라 DH 씬 값을 존중한다.

    private void HandlePresetChanged(FogPreset changed)
    {
        if (!Application.isPlaying) return;
        if (changed != activePreset) return;

        ApplySightRadius(changed, resetOnChange: true);
        ApplyToMaterial(changed);
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

    /// <summary>플레이 종료 시 커밋 대상 .mat을 원상 복구 — 에셋 무오염 보장.</summary>
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
        CaptureLayerFromMaterial(p.unexplored, UeIds);
        if (fogMaterial.HasProperty(FgIds.DensityLow))
            CaptureLayerFromMaterial(p.fogged, FgIds);

        if (fogMaterial.HasProperty(EdgeWidthWorldId))
        {
            p.shared.edgeWidthWorld = fogMaterial.GetFloat(EdgeWidthWorldId);
            p.shared.edgeNoiseStrength = fogMaterial.GetFloat(EdgeNoiseStrengthId);
            p.shared.edgeNoiseScale = fogMaterial.GetFloat(EdgeNoiseScaleId);
            p.shared.edgeNoiseSpeed = fogMaterial.GetFloat(EdgeNoiseSpeedId);
            p.shared.edgeFadeWidth = fogMaterial.GetFloat(EdgeFadeWidthId);
            p.shared.edgeLayerSpreadWorld = fogMaterial.GetFloat(EdgeLayerSpreadWorldId);
            if (fogMaterial.HasProperty(StateBlendWidthWorldId))
                p.shared.stateBlendWidthWorld = fogMaterial.GetFloat(StateBlendWidthWorldId);
        }
        p.shared.debugMode = fogMaterial.GetFloat(DebugModeId) > 0.5f;

        UnityEditor.EditorUtility.SetDirty(p);
        Debug.Log($"[RenderFXManager] 머티리얼 값 캡처 완료 → {p.name}");
    }

    /// <summary>레이어 1벌 역캡처. sheetRangeMultiplier는 시야반경 환산값이라 역산 생략(sight와 동일 정책).</summary>
    private void CaptureLayerFromMaterial(FogPreset.FogLayerSettings s, in LayerIds ids)
    {
        s.fogColor = fogMaterial.GetColor(ids.FogColor);
        if (fogMaterial.HasProperty(ids.FogColorMid))
        {
            s.fogColorMid = fogMaterial.GetColor(ids.FogColorMid);
            s.fogColorHigh = fogMaterial.GetColor(ids.FogColorHigh);
        }
        s.fogDensityLow = fogMaterial.GetFloat(ids.DensityLow);
        s.fogDensityMid = fogMaterial.GetFloat(ids.DensityMid);
        s.fogDensityHigh = fogMaterial.GetFloat(ids.DensityHigh);
        s.noiseScaleBase = fogMaterial.GetFloat(ids.Noise1);
        s.noiseScaleDetail = fogMaterial.GetFloat(ids.Noise2);
        s.noiseScaleDistortion = fogMaterial.GetFloat(ids.Noise3);
        s.flowSpeedBase = fogMaterial.GetFloat(ids.Flow1);
        s.flowSpeedDetail = fogMaterial.GetFloat(ids.Flow2);
        s.flowSpeedDistortion = fogMaterial.GetFloat(ids.Flow3);
        s.distortionStrength = fogMaterial.GetFloat(ids.Distortion);
        s.noiseContrast = fogMaterial.GetFloat(ids.Contrast);
        s.heightTransition = fogMaterial.GetFloat(ids.HeightTransition);
        if (fogMaterial.HasProperty(ids.LowTopY))
        {
            s.fogLowTopY = fogMaterial.GetFloat(ids.LowTopY);
            s.fogHighStartY = fogMaterial.GetFloat(ids.HighStartY);
        }
        s.brightnessLow = fogMaterial.GetFloat(ids.BrightLow);
        s.brightnessMid = fogMaterial.GetFloat(ids.BrightMid);
        s.brightnessHigh = fogMaterial.GetFloat(ids.BrightHigh);
        s.cloudContrast = fogMaterial.GetFloat(ids.CloudContrast);
        if (fogMaterial.HasProperty(ids.CloudCoverage))
        {
            s.cloudCoverage = fogMaterial.GetFloat(ids.CloudCoverage);
            s.cloudDensityEffect = fogMaterial.GetFloat(ids.CloudDensityEffect);
        }
        if (fogMaterial.HasProperty(ids.SheetOpacity))
        {
            s.sheetOpacity = fogMaterial.GetFloat(ids.SheetOpacity);
            s.sheetHeightY = fogMaterial.GetFloat(ids.SheetHeightY);
            s.sheetColor = fogMaterial.GetColor(ids.SheetColor);
            s.sheetFadeWidthWorld = fogMaterial.GetFloat(ids.SheetFadeWidth);
            s.sheetGroundAlign = fogMaterial.GetFloat(ids.SheetGroundAlign);
        }
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
