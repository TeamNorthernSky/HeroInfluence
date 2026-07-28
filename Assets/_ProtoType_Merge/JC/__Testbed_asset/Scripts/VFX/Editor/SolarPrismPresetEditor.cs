using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 솔라 프리즘 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    /// 대상: PrismCrystal.mat + StarFlare.mat + GlowDisc.mat + SolarPrismSkill 프리팹(배치·모션·판정).
    /// 씬 스코프: SolarPrismVfx 컴포넌트(흑암 변형은 root 이름 "Dark" 가드로 제외).
    /// 렌더러 이름 규약: Crystal(크리스탈)/Flare_*(플레어)/GroundGlow(바닥광)/Embers(입자).
    /// </summary>
    [CustomEditor(typeof(SolarPrismPreset))]
    public class SolarPrismPresetEditor : FlareOrbPresetEditorBase
    {
        const string SDIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/L_SolarPrism";
        static string MatCrystal => SDIR + "/PrismCrystal.mat";
        static string MatFlare   => SDIR + "/StarFlare.mat";
        static string MatGround  => SDIR + "/GlowDisc.mat";
        static string MatBurst   => SDIR + "/PrismBurst.mat";
        static string MatRing    => SDIR + "/PrismImpactRing.mat";
        static string SkillPrefab => SDIR + "/SolarPrismSkill.prefab";
        const string PresetPath  = SDIR + "/FX_SolarPrismPreset.asset";

        protected override string LiveKey => "JC.SolarPrismPreset.LivePreview";
        protected override string HelpText =>
            "라이브 프리뷰: 슬라이더를 움직이면 씬의 크리스탈·플레어·바닥광·배치에 즉시 반영(MPB, 에셋 무변경).\n" +
            "플레어 점멸·배치는 플레이 중 차지(J키)에서 확인.\n" +
            "적용: 재질 + SolarPrismSkill 프리팹에 확정 기록하고 프리뷰 오버라이드 해제.\n" +
            "캡처: 현재 재질/프리팹 값을 이 프리셋으로 역방향 읽기.";

        protected override void LivePush() => LivePushStatic((SolarPrismPreset)target);
        protected override void ApplyPreset() => Apply((SolarPrismPreset)target);
        protected override void CapturePreset() => Capture((SolarPrismPreset)target);

        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.SolarPrismPreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<SolarPrismPreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        static void FillCrystal(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_ColorBody", p.bodyColor);
            setC("_ColorRim", p.rimColor);
            setF("_Emission", p.crystalEmission);
            setF("_BodyAlpha", p.bodyAlpha);
            setF("_RimAlpha", p.rimAlpha);
            setF("_FresnelPow", p.fresnelPow);
            setF("_FacetAmount", p.facetAmount);
        }

        static void FillFlare(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_Color", p.flareColor);
            setF("_Emission", p.flareEmission);
            setF("_SpikeNarrow", p.spikeNarrow);
            setF("_SpikeFall", p.spikeFall);
            setF("_DiagRatio", p.diagRatio);
            setF("_CoreSize", p.flareCoreSize);
        }

        static void FillGround(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_Color", p.groundColor);
            setF("_Emission", p.groundEmission);
            setF("_Falloff", p.groundFalloff);
        }

        static void FillImpactBurst(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_ColorHot", p.impactHotColor);
            setC("_ColorGold", p.impactGlowColor);
            setF("_Emission", p.impactEmission);
        }

        static void FillImpactRing(System.Action<string, float> setF, System.Action<string, Color> setC, SolarPrismPreset p)
        {
            setC("_ColorRing", p.impactRingColor);
        }

        static void ApplyVfx(SolarPrismVfx vfx, SolarPrismPreset p)
        {
            vfx.UnitCount = p.unitCount;
            vfx.Spacing = p.spacing;
            vfx.ForwardOffset = p.forwardOffset;
            vfx.HoverHeight = p.hoverHeight;
            vfx.PrismScale = p.prismScale;
            vfx.HoverAmp = p.hoverAmp;
            vfx.HoverFreq = p.hoverFreq;
            vfx.SpinSpeed = p.spinSpeed;
            vfx.SpinJitter = p.spinJitter;
            vfx.SummonTime = p.summonTime;
            vfx.SummonStagger = p.summonStagger;
            vfx.BeltRadius = p.beltRadius;
            vfx.BeltLocalY = p.beltLocalY;
            vfx.BeltAngleOffset = p.beltAngleOffset;
            vfx.FlareThreshold = p.flareThreshold;
            vfx.FlareSharp = p.flareSharp;
            vfx.FlareSize = p.flareSize;
            vfx.GroundSize = p.groundSize;
            vfx.GroundBase = p.groundBase;
            vfx.GroundPulse = p.groundPulse;
            vfx.LaunchSpeed = p.launchSpeed;
            vfx.LaunchStagger = p.launchStagger;
            vfx.LaunchSpinMul = p.launchSpinMul;
            vfx.LaunchArc = p.launchArc;
            vfx.TargetHeight = p.targetHeight;
            vfx.AimBlend = p.aimBlend;
            foreach (var tr in vfx.GetComponentsInChildren<TrailRenderer>(true))
            {
                tr.time = p.launchTrailTime;
                tr.widthMultiplier = p.launchTrailWidth;
            }
            foreach (var im in vfx.GetComponentsInChildren<FlareBombImpact>(true))
            {
                im.BurstDuration = p.impactBurstDuration;
                im.RingDuration = p.impactRingDuration;
                im.RingGroundOffset = p.targetHeight;
                var burst = im.transform.Find("ImpactBurst");
                if (burst != null) burst.localScale = new Vector3(p.impactSize, p.impactSize, 1f);
                var ring = im.transform.Find("ImpactRing");
                if (ring != null) ring.localScale = new Vector3(p.impactRingSize, p.impactRingSize, 1f);
            }
        }

        static void ApplyEmbers(SolarPrismVfx vfx, SolarPrismPreset p)
        {
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var em = ps.emission;
                em.rateOverTime = p.emberRate;
                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(p.emberSize * 0.6f, p.emberSize * 1.3f);
            }
        }

        static void LivePushStatic(SolarPrismPreset p)
        {
            foreach (var vfx in Object.FindObjectsByType<SolarPrismVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(vfx)) continue;
                ApplyVfx(vfx, p);
                ApplyEmbers(vfx, p);
                foreach (var mr in vfx.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    string n = mr.gameObject.name;
                    if (n.Contains("Crystal")) FillCrystal((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    else if (n.Contains("ImpactBurst")) FillImpactBurst((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    else if (n.Contains("ImpactRing")) FillImpactRing((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    else if (n.Contains("Flare")) FillFlare((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    else if (n.Contains("Ground")) FillGround((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    else continue;
                    mr.SetPropertyBlock(mpb);
                }
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Apply(SolarPrismPreset p)
        {
            var mc = Load<Material>(MatCrystal);
            FillCrystal((k, f) => mc.SetFloat(k, f), (k, c) => mc.SetColor(k, c), p);
            mc.renderQueue = 3001;
            EditorUtility.SetDirty(mc);
            var mf = Load<Material>(MatFlare);
            FillFlare((k, f) => mf.SetFloat(k, f), (k, c) => mf.SetColor(k, c), p);
            mf.renderQueue = 3007;
            EditorUtility.SetDirty(mf);
            var mg = Load<Material>(MatGround);
            FillGround((k, f) => mg.SetFloat(k, f), (k, c) => mg.SetColor(k, c), p);
            mg.renderQueue = 2999;
            EditorUtility.SetDirty(mg);
            var mb = Load<Material>(MatBurst);
            if (mb != null)
            {
                FillImpactBurst((k, f) => mb.SetFloat(k, f), (k, c) => mb.SetColor(k, c), p);
                mb.renderQueue = 3005;
                EditorUtility.SetDirty(mb);
            }
            var mr2 = Load<Material>(MatRing);
            if (mr2 != null)
            {
                FillImpactRing((k, f) => mr2.SetFloat(k, f), (k, c) => mr2.SetColor(k, c), p);
                mr2.renderQueue = 3004;
                EditorUtility.SetDirty(mr2);
            }
            AssetDatabase.SaveAssets();

            var full = PrefabUtility.LoadPrefabContents(SkillPrefab);
            try
            {
                var vfx = full.GetComponent<SolarPrismVfx>();
                if (vfx != null)
                {
                    var so = new SerializedObject(vfx);
                    so.FindProperty("unitCount").intValue = p.unitCount;
                    so.FindProperty("spacing").floatValue = p.spacing;
                    so.FindProperty("forwardOffset").floatValue = p.forwardOffset;
                    so.FindProperty("hoverHeight").floatValue = p.hoverHeight;
                    so.FindProperty("prismScale").floatValue = p.prismScale;
                    so.FindProperty("hoverAmp").floatValue = p.hoverAmp;
                    so.FindProperty("hoverFreq").floatValue = p.hoverFreq;
                    so.FindProperty("spinSpeed").floatValue = p.spinSpeed;
                    so.FindProperty("spinJitter").floatValue = p.spinJitter;
                    so.FindProperty("summonTime").floatValue = p.summonTime;
                    so.FindProperty("summonStagger").floatValue = p.summonStagger;
                    so.FindProperty("beltRadius").floatValue = p.beltRadius;
                    so.FindProperty("beltLocalY").floatValue = p.beltLocalY;
                    so.FindProperty("beltAngleOffset").floatValue = p.beltAngleOffset;
                    so.FindProperty("flareThreshold").floatValue = p.flareThreshold;
                    so.FindProperty("flareSharp").floatValue = p.flareSharp;
                    so.FindProperty("flareSize").floatValue = p.flareSize;
                    so.FindProperty("groundSize").floatValue = p.groundSize;
                    so.FindProperty("groundBase").floatValue = p.groundBase;
                    so.FindProperty("groundPulse").floatValue = p.groundPulse;
                    so.FindProperty("launchSpeed").floatValue = p.launchSpeed;
                    so.FindProperty("launchStagger").floatValue = p.launchStagger;
                    so.FindProperty("launchSpinMul").floatValue = p.launchSpinMul;
                    so.FindProperty("launchArc").floatValue = p.launchArc;
                    so.FindProperty("targetHeight").floatValue = p.targetHeight;
                    so.FindProperty("aimBlend").floatValue = p.aimBlend;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    ApplyEmbers(vfx, p);
                    foreach (var tr in vfx.GetComponentsInChildren<TrailRenderer>(true))
                    {
                        tr.time = p.launchTrailTime;
                        tr.widthMultiplier = p.launchTrailWidth;
                    }
                    foreach (var im in vfx.GetComponentsInChildren<FlareBombImpact>(true))
                    {
                        var soI = new SerializedObject(im);
                        soI.FindProperty("burstDuration").floatValue = p.impactBurstDuration;
                        soI.FindProperty("ringDuration").floatValue = p.impactRingDuration;
                        soI.FindProperty("ringGroundOffset").floatValue = p.targetHeight;
                        soI.ApplyModifiedPropertiesWithoutUndo();
                        var burst = im.transform.Find("ImpactBurst");
                        if (burst != null) burst.localScale = new Vector3(p.impactSize, p.impactSize, 1f);
                        var ring = im.transform.Find("ImpactRing");
                        if (ring != null) ring.localScale = new Vector3(p.impactRingSize, p.impactRingSize, 1f);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(full, SkillPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(full); }

            foreach (var vfx in Object.FindObjectsByType<SolarPrismVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(vfx)) continue;
                foreach (var mr in vfx.GetComponentsInChildren<MeshRenderer>(true))
                    mr.SetPropertyBlock(null);
            }
            Debug.Log("[SolarPrismPreset] 재질·프리팹에 적용 완료 (라이브 오버라이드 해제)");
        }

        public static void Capture(SolarPrismPreset p)
        {
            Undo.RecordObject(p, "Capture Solar Prism");

            var mc = Load<Material>(MatCrystal);
            p.bodyColor = mc.GetColor("_ColorBody");
            p.rimColor = mc.GetColor("_ColorRim");
            p.crystalEmission = mc.GetFloat("_Emission");
            p.bodyAlpha = mc.GetFloat("_BodyAlpha");
            p.rimAlpha = mc.GetFloat("_RimAlpha");
            p.fresnelPow = mc.GetFloat("_FresnelPow");
            p.facetAmount = mc.GetFloat("_FacetAmount");

            var mf = Load<Material>(MatFlare);
            p.flareColor = mf.GetColor("_Color");
            p.flareEmission = mf.GetFloat("_Emission");
            p.spikeNarrow = mf.GetFloat("_SpikeNarrow");
            p.spikeFall = mf.GetFloat("_SpikeFall");
            p.diagRatio = mf.GetFloat("_DiagRatio");
            p.flareCoreSize = mf.GetFloat("_CoreSize");

            var mg = Load<Material>(MatGround);
            p.groundColor = mg.GetColor("_Color");
            p.groundEmission = mg.GetFloat("_Emission");
            p.groundFalloff = mg.GetFloat("_Falloff");

            var full = Load<GameObject>(SkillPrefab);
            var vfx = full != null ? full.GetComponent<SolarPrismVfx>() : null;
            if (vfx != null)
            {
                p.unitCount = vfx.UnitCount;
                p.spacing = vfx.Spacing;
                p.forwardOffset = vfx.ForwardOffset;
                p.hoverHeight = vfx.HoverHeight;
                p.prismScale = vfx.PrismScale;
                p.hoverAmp = vfx.HoverAmp;
                p.hoverFreq = vfx.HoverFreq;
                p.spinSpeed = vfx.SpinSpeed;
                p.spinJitter = vfx.SpinJitter;
                p.summonTime = vfx.SummonTime;
                p.summonStagger = vfx.SummonStagger;
                p.beltRadius = vfx.BeltRadius;
                p.beltLocalY = vfx.BeltLocalY;
                p.beltAngleOffset = vfx.BeltAngleOffset;
                p.flareThreshold = vfx.FlareThreshold;
                p.flareSharp = vfx.FlareSharp;
                p.flareSize = vfx.FlareSize;
                p.groundSize = vfx.GroundSize;
                p.groundBase = vfx.GroundBase;
                p.groundPulse = vfx.GroundPulse;
                p.launchSpeed = vfx.LaunchSpeed;
                p.launchStagger = vfx.LaunchStagger;
                p.launchSpinMul = vfx.LaunchSpinMul;
                p.launchArc = vfx.LaunchArc;
                p.targetHeight = vfx.TargetHeight;
                p.aimBlend = vfx.AimBlend;
                var trc = vfx.GetComponentInChildren<TrailRenderer>(true);
                if (trc != null) { p.launchTrailTime = trc.time; p.launchTrailWidth = trc.widthMultiplier; }
                var im = vfx.GetComponentInChildren<FlareBombImpact>(true);
                if (im != null)
                {
                    p.impactBurstDuration = im.BurstDuration;
                    p.impactRingDuration = im.RingDuration;
                    var burst = im.transform.Find("ImpactBurst");
                    if (burst != null) p.impactSize = burst.localScale.x;
                    var ring = im.transform.Find("ImpactRing");
                    if (ring != null) p.impactRingSize = ring.localScale.x;
                }
                var mb = Load<Material>(MatBurst);
                if (mb != null)
                {
                    p.impactHotColor = mb.GetColor("_ColorHot");
                    p.impactGlowColor = mb.GetColor("_ColorGold");
                    p.impactEmission = mb.GetFloat("_Emission");
                }
                var mr2 = Load<Material>(MatRing);
                if (mr2 != null) p.impactRingColor = mr2.GetColor("_ColorRing");
                var ps = vfx.GetComponentInChildren<ParticleSystem>(true);
                if (ps != null)
                {
                    p.emberRate = ps.emission.rateOverTime.constant;
                    p.emberSize = ps.main.startSize.constantMax / 1.3f;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[SolarPrismPreset] 현재값 캡처 완료");
        }
    }
}
