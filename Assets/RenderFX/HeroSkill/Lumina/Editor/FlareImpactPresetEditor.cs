using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 발사·탄착 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    /// 대상: FlareImpactBurst.mat + FlareImpactRing.mat + FlareBombSkill 프리팹
    /// (FlareBombImpact 타이밍·쿼드 크기 + FlareBombVfx 비행 파라미터).
    /// 씬 스코프: FlareBombImpact/FlareBombVfx 컴포넌트로 탐색(F0/F1/F2와 무간섭).
    /// </summary>
    [CustomEditor(typeof(FlareImpactPreset))]
    public class FlareImpactPresetEditor : FlareOrbPresetEditorBase
    {
        static string MatBurst    => DIR + "/FlareImpactBurst.mat";
        static string MatRing     => DIR + "/FlareImpactRing.mat";
        static string SkillPrefab => DIR + "/FlareBombSkill.prefab";
        const string PresetPath   = DIR + "/FX_FlareImpactPreset.asset";

        protected override string LiveKey => "JC.FlareImpactPreset.LivePreview";
        protected override string HelpText =>
            "라이브 프리뷰: 슬라이더를 움직이면 씬의 탄착 버스트·링·비행 파라미터에 즉시 반영(MPB, 에셋 무변경).\n" +
            "previewProgress로 원샷 진행도를 정지 프레임 스크럽(에디트 모드, 임팩트 렌더러가 켜져 있어야 보임).\n" +
            "적용: 재질 + FlareBombSkill 프리팹(타이밍·크기·비행)에 확정 기록하고 프리뷰 오버라이드 해제.\n" +
            "캡처: 현재 재질/프리팹 값을 이 프리셋으로 역방향 읽기.";

        protected override void LivePush() => LivePushStatic((FlareImpactPreset)target);
        protected override void ApplyPreset() => Apply((FlareImpactPreset)target);
        protected override void CapturePreset() => Capture((FlareImpactPreset)target);

        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.FlareImpactPreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<FlareImpactPreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        static void FillBurst(System.Action<string, float> setF, System.Action<string, Color> setC,
                              FlareImpactPreset p)
        {
            setC("_ColorHot", p.hotColor);
            setC("_ColorGold", p.goldColor);
            setF("_Emission", p.burstEmission);
            setF("_HotCore", p.hotCore);
            setF("_GrowFrac", p.growFrac);
            setF("_FadeStart", p.fadeStart);
            setF("_SpikeCount", p.spikeCount);
            setF("_SpikeLen", p.spikeLen);
            setF("_LenJitter", p.lenJitter);
            setF("_SpikeWidth", p.spikeWidth);
            setF("_WidthJitter", p.widthJitter);
            setF("_TaperSharp", p.taperSharp);
            setF("_SubCount", p.subCount);
            setF("_SubLen", p.subLen);
            setF("_SubWidth", p.subWidth);
            setF("_PosJitter", p.posJitter);
            setF("_Seed", p.seed);
            setF("_FlashSize", p.flashSize);
            setF("_FlashIntensity", p.flashIntensity);
            setF("_FlashFade", p.flashFade);
        }

        static void FillRing(System.Action<string, float> setF, System.Action<string, Color> setC,
                             FlareImpactPreset p)
        {
            setC("_ColorRing", p.ringColor);
            setF("_Emission", p.ringEmission);
            setF("_RingMaxR", p.ringMaxR);
            setF("_RingWidth", p.ringWidth);
            setF("_InnerGlow", p.innerGlow);
            setF("_FadeStart", p.ringFadeStart);
        }

        static void ApplyFlightFx(Transform vfxRoot, FlareImpactPreset p)
        {
            var trail = vfxRoot.GetComponentInChildren<TrailRenderer>(true);
            if (trail != null)
            {
                trail.time = p.trailTime;
                trail.widthMultiplier = p.trailWidth;
            }
            var ember = vfxRoot.GetComponentInChildren<ParticleSystem>(true);
            if (ember != null)
            {
                var em = ember.emission;
                em.rateOverDistance = p.emberRate;
                var main = ember.main;
                main.startSize = new ParticleSystem.MinMaxCurve(p.emberSize * 0.6f, p.emberSize * 1.3f);
                var noise = ember.noise;
                noise.strength = p.emberNoise;
            }
        }

        static void ApplySizes(Transform impactRoot, FlareImpactPreset p)
        {
            var g = impactRoot.Find("BurstGround");
            if (g != null) g.localScale = new Vector3(p.groundWidth, p.groundHeight, 1f);
            var s = impactRoot.Find("BurstSpike");
            if (s != null) s.localScale = new Vector3(p.spikeWidth2, p.spikeHeight, 1f);
            var r = impactRoot.Find("GroundRing");
            if (r != null) r.localScale = new Vector3(p.ringSize, p.ringSize, 1f);
        }

        static void LivePushStatic(FlareImpactPreset p)
        {
            foreach (var impact in Object.FindObjectsByType<FlareBombImpact>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(impact)) continue;   // 흑염 변형은 크림판 프리셋 스코프 밖
                ApplySizes(impact.transform, p);
                impact.BurstDuration = p.burstDuration;
                impact.RingDuration = p.ringDuration;
                impact.RingGroundOffset = p.ringGroundOffset;
                foreach (Transform child in impact.transform)
                {
                    var mr = child.GetComponent<MeshRenderer>();
                    if (mr == null) continue;
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    bool isRing = child.name.Contains("Ring");
                    if (isRing) FillRing((n, f) => mpb.SetFloat(n, f), (n, c) => mpb.SetColor(n, c), p);
                    else FillBurst((n, f) => mpb.SetFloat(n, f), (n, c) => mpb.SetColor(n, c), p);
                    if (!Application.isPlaying) mpb.SetFloat("_Progress", p.previewProgress);
                    mr.SetPropertyBlock(mpb);
                }
            }
            foreach (var vfx in Object.FindObjectsByType<FlareBombVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(vfx)) continue;   // 흑염 변형은 크림판 프리셋 스코프 밖
                vfx.OrbScale = p.orbScale;
                vfx.SummonOffset = p.summonOffset;
                vfx.ChargeDuration = p.chargeDuration;
                vfx.ChargeGrowTime = p.chargeGrowTime;
                vfx.Speed = p.speed;
                vfx.ArcHeight = p.arcHeight;
                vfx.FlightScale = p.flightScale;
                vfx.TargetHeight = p.targetHeight;
                vfx.LingerTime = p.lingerTime;
                ApplyFlightFx(vfx.transform, p);
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Apply(FlareImpactPreset p)
        {
            var matB = Load<Material>(MatBurst);
            FillBurst((n, f) => matB.SetFloat(n, f), (n, c) => matB.SetColor(n, c), p);
            matB.renderQueue = 3005;   // 최상단
            EditorUtility.SetDirty(matB);
            var matR = Load<Material>(MatRing);
            FillRing((n, f) => matR.SetFloat(n, f), (n, c) => matR.SetColor(n, c), p);
            matR.renderQueue = 3004;
            EditorUtility.SetDirty(matR);
            AssetDatabase.SaveAssets();

            var full = PrefabUtility.LoadPrefabContents(SkillPrefab);
            try
            {
                var impact = full.GetComponentInChildren<FlareBombImpact>(true);
                if (impact != null)
                {
                    ApplySizes(impact.transform, p);
                    var so = new SerializedObject(impact);
                    so.FindProperty("burstDuration").floatValue = p.burstDuration;
                    so.FindProperty("ringDuration").floatValue = p.ringDuration;
                    so.FindProperty("ringGroundOffset").floatValue = p.ringGroundOffset;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var vfx = full.GetComponentInChildren<FlareBombVfx>(true);
                if (vfx != null)
                {
                    var so = new SerializedObject(vfx);
                    so.FindProperty("orbScale").floatValue = p.orbScale;
                    so.FindProperty("summonOffset").vector3Value = p.summonOffset;
                    so.FindProperty("chargeDuration").floatValue = p.chargeDuration;
                    so.FindProperty("chargeGrowTime").floatValue = p.chargeGrowTime;
                    so.FindProperty("speed").floatValue = p.speed;
                    so.FindProperty("arcHeight").floatValue = p.arcHeight;
                    so.FindProperty("flightScale").floatValue = p.flightScale;
                    so.FindProperty("targetHeight").floatValue = p.targetHeight;
                    so.FindProperty("lingerTime").floatValue = p.lingerTime;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    ApplyFlightFx(vfx.transform, p);
                }
                PrefabUtility.SaveAsPrefabAsset(full, SkillPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(full); }

            foreach (var impact in Object.FindObjectsByType<FlareBombImpact>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (Transform child in impact.transform)
                {
                    var mr = child.GetComponent<MeshRenderer>();
                    if (mr != null) mr.SetPropertyBlock(null);
                }
            Debug.Log("[FlareImpactPreset] 재질·프리팹에 적용 완료 (라이브 오버라이드 해제)");
        }

        public static void Capture(FlareImpactPreset p)
        {
            Undo.RecordObject(p, "Capture Flare Impact");

            var matB = Load<Material>(MatBurst);
            p.hotColor = matB.GetColor("_ColorHot");
            p.goldColor = matB.GetColor("_ColorGold");
            p.burstEmission = matB.GetFloat("_Emission");
            p.hotCore = matB.GetFloat("_HotCore");
            p.growFrac = matB.GetFloat("_GrowFrac");
            p.fadeStart = matB.GetFloat("_FadeStart");
            p.spikeCount = Mathf.RoundToInt(matB.GetFloat("_SpikeCount"));
            p.spikeLen = matB.GetFloat("_SpikeLen");
            p.lenJitter = matB.GetFloat("_LenJitter");
            p.spikeWidth = matB.GetFloat("_SpikeWidth");
            p.widthJitter = matB.GetFloat("_WidthJitter");
            p.taperSharp = matB.GetFloat("_TaperSharp");
            p.subCount = Mathf.RoundToInt(matB.GetFloat("_SubCount"));
            p.subLen = matB.GetFloat("_SubLen");
            p.subWidth = matB.GetFloat("_SubWidth");
            p.posJitter = matB.GetFloat("_PosJitter");
            p.seed = matB.GetFloat("_Seed");
            p.flashSize = matB.GetFloat("_FlashSize");
            p.flashIntensity = matB.GetFloat("_FlashIntensity");
            p.flashFade = matB.GetFloat("_FlashFade");

            var matR = Load<Material>(MatRing);
            p.ringColor = matR.GetColor("_ColorRing");
            p.ringEmission = matR.GetFloat("_Emission");
            p.ringMaxR = matR.GetFloat("_RingMaxR");
            p.ringWidth = matR.GetFloat("_RingWidth");
            p.innerGlow = matR.GetFloat("_InnerGlow");
            p.ringFadeStart = matR.GetFloat("_FadeStart");

            var full = Load<GameObject>(SkillPrefab);
            var impact = full != null ? full.GetComponentInChildren<FlareBombImpact>(true) : null;
            if (impact != null)
            {
                var g = impact.transform.Find("BurstGround");
                if (g != null) { p.groundWidth = g.localScale.x; p.groundHeight = g.localScale.y; }
                var s = impact.transform.Find("BurstSpike");
                if (s != null) { p.spikeWidth2 = s.localScale.x; p.spikeHeight = s.localScale.y; }
                var r = impact.transform.Find("GroundRing");
                if (r != null) p.ringSize = r.localScale.x;
                p.burstDuration = impact.BurstDuration;
                p.ringDuration = impact.RingDuration;
                p.ringGroundOffset = impact.RingGroundOffset;
            }
            var vfx = full != null ? full.GetComponentInChildren<FlareBombVfx>(true) : null;
            if (vfx != null)
            {
                p.orbScale = vfx.OrbScale;
                p.summonOffset = vfx.SummonOffset;
                p.chargeDuration = vfx.ChargeDuration;
                p.chargeGrowTime = vfx.ChargeGrowTime;
                p.speed = vfx.Speed;
                p.arcHeight = vfx.ArcHeight;
                p.flightScale = vfx.FlightScale;
                p.targetHeight = vfx.TargetHeight;
                p.lingerTime = vfx.LingerTime;
                var trail = vfx.GetComponentInChildren<TrailRenderer>(true);
                if (trail != null) { p.trailTime = trail.time; p.trailWidth = trail.widthMultiplier; }
                var ember = vfx.GetComponentInChildren<ParticleSystem>(true);
                if (ember != null)
                {
                    p.emberRate = ember.emission.rateOverDistance.constant;
                    p.emberSize = ember.main.startSize.constantMax / 1.3f;
                    p.emberNoise = ember.noise.strength.constant;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[FlareImpactPreset] 현재값 캡처 완료");
        }
    }
}
