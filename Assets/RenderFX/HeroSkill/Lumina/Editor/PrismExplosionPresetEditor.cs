using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 프리즘 익스플로전 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    /// 대상: 전용 재질 5종(PrismCrystalX/StarFlareX/GlowDiscX/PrismBurstX/PrismImpactRingX) +
    /// PrismExplosionSkill 프리팹(배치·모션·상승조준·발사·폭발).
    /// 씬 스코프: PrismExplosionVfx 컴포넌트(Dark 가드 상속).
    /// 렌더러 이름 규약: Crystal/Flare_*/GroundGlow/ImpactBurst*/ImpactRing.
    /// </summary>
    [CustomEditor(typeof(PrismExplosionPreset))]
    public class PrismExplosionPresetEditor : FlareOrbPresetEditorBase
    {
        const string XDIR = "Assets/RenderFX/HeroSkill/Lumina/PrismExplosion";
        static string MatCrystal => XDIR + "/Materials/PrismCrystalX.mat";
        static string MatFlare   => XDIR + "/Materials/StarFlareX.mat";
        static string MatGround  => XDIR + "/Materials/GlowDiscX.mat";
        static string MatBurst   => XDIR + "/Materials/PrismBurstX.mat";
        static string MatRing    => XDIR + "/Materials/PrismImpactRingX.mat";
        static string SkillPrefab => XDIR + "/Prefabs/PrismExplosionSkill.prefab";
        const string PresetPath  = XDIR + "/Presets/FX_PrismExplosionPreset.asset";

        protected override string LiveKey => "JC.PrismExplosionPreset.LivePreview";
        protected override string HelpText =>
            "라이브 프리뷰: 슬라이더를 움직이면 씬의 수정·플레어·바닥광·폭발·시퀀스 값에 즉시 반영(MPB, 에셋 무변경).\n" +
            "플로우 확인은 플레이 중 K(차지) → K(상승·조준 → 비행 → 진형 중앙 대폭발).\n" +
            "적용: 전용 재질 5종 + PrismExplosionSkill 프리팹에 확정 기록하고 프리뷰 오버라이드 해제.\n" +
            "캡처: 현재 재질/프리팹 값을 이 프리셋으로 역방향 읽기.";

        protected override void LivePush() => LivePushStatic((PrismExplosionPreset)target);
        protected override void ApplyPreset() => Apply((PrismExplosionPreset)target);
        protected override void CapturePreset() => Capture((PrismExplosionPreset)target);

        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.PrismExplosionPreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<PrismExplosionPreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        static void FillCrystal(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_ColorBody", p.bodyColor);
            setC("_ColorRim", p.rimColor);
            setF("_Emission", p.crystalEmission);
            setF("_BodyAlpha", p.bodyAlpha);
            setF("_RimAlpha", p.rimAlpha);
            setF("_FresnelPow", p.fresnelPow);
            setF("_FacetAmount", p.facetAmount);
        }

        static void FillFlare(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_Color", p.flareColor);
            setF("_Emission", p.flareEmission);
        }

        static void FillGround(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_Color", p.groundColor);
            setF("_Emission", p.groundEmission);
        }

        static void FillImpactBurst(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_ColorHot", p.impactHotColor);
            setC("_ColorGold", p.impactGlowColor);
            setF("_Emission", p.impactEmission);
        }

        static void FillImpactRing(System.Action<string, float> setF, System.Action<string, Color> setC, PrismExplosionPreset p)
        {
            setC("_ColorRing", p.impactRingColor);
        }

        static void ApplyVfx(PrismExplosionVfx vfx, PrismExplosionPreset p)
        {
            vfx.ForwardOffset = p.forwardOffset;
            vfx.HoverHeight = p.hoverHeight;
            vfx.PrismScale = p.prismScale;
            vfx.HoverAmp = p.hoverAmp;
            vfx.HoverFreq = p.hoverFreq;
            vfx.SpinSpeed = p.spinSpeed;
            vfx.SummonTime = p.summonTime;
            vfx.FlareThreshold = p.flareThreshold;
            vfx.FlareSharp = p.flareSharp;
            vfx.FlareSize = p.flareSize;
            vfx.GroundSize = p.groundSize;
            vfx.GroundBase = p.groundBase;
            vfx.GroundPulse = p.groundPulse;
            vfx.RiseTime = p.riseTime;
            vfx.RiseHeight = p.riseHeight;
            vfx.TremorAmp = p.tremorAmp;
            vfx.TremorFreq = p.tremorFreq;
            vfx.AimSpinMul = p.aimSpinMul;
            vfx.LaunchSpeed = p.launchSpeed;
            vfx.LaunchSpinMul = p.launchSpinMul;
            vfx.LaunchArc = p.launchArc;
            vfx.TargetHeight = p.targetHeight;
            vfx.SpiralRadius = p.spiralRadius;
            vfx.SpiralRate = p.spiralRate;
            vfx.SmokeLinger = p.smokeLinger;

            var mainTrail = vfx.transform.Find("PrismUnit/FlightTrail");
            if (mainTrail != null)
            {
                var tr = mainTrail.GetComponent<TrailRenderer>();
                if (tr != null) { tr.time = p.trailTime; tr.widthMultiplier = p.trailWidth; }
            }
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!ps.gameObject.name.Contains("Embers")) continue;
                var em = ps.emission;
                em.rateOverTime = p.emberRate;
                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(p.emberSize * 0.6f, p.emberSize * 1.3f);
            }
            foreach (var im in vfx.GetComponentsInChildren<FlareBombImpact>(true))
            {
                im.BurstDuration = p.impactBurstDuration;
                im.RingDuration = p.impactRingDuration;
                im.RingGroundOffset = p.targetHeight;
                var bh = im.transform.Find("ImpactBurstH");
                if (bh != null) bh.localScale = new Vector3(p.impactSizeH, p.impactSizeH * 0.6f, 1f);
                var bv = im.transform.Find("ImpactBurstV");
                if (bv != null) bv.localScale = new Vector3(p.impactSizeV * 0.5f, p.impactSizeV, 1f);
                var ring = im.transform.Find("ImpactRing");
                if (ring != null) ring.localScale = new Vector3(p.impactRingSize, p.impactRingSize, 1f);
            }
        }

        static void LivePushStatic(PrismExplosionPreset p)
        {
            foreach (var vfx in Object.FindObjectsByType<PrismExplosionVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(vfx)) continue;
                ApplyVfx(vfx, p);
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

        public static void Apply(PrismExplosionPreset p)
        {
            void Bake(string path, System.Action<Material> fill, int queue)
            {
                var m = Load<Material>(path);
                if (m == null) return;
                fill(m);
                m.renderQueue = queue;
                EditorUtility.SetDirty(m);
            }
            Bake(MatCrystal, m => FillCrystal((k, f) => m.SetFloat(k, f), (k, c) => m.SetColor(k, c), p), 3001);
            Bake(MatFlare, m => FillFlare((k, f) => m.SetFloat(k, f), (k, c) => m.SetColor(k, c), p), 3007);
            Bake(MatGround, m => FillGround((k, f) => m.SetFloat(k, f), (k, c) => m.SetColor(k, c), p), 2999);
            Bake(MatBurst, m => FillImpactBurst((k, f) => m.SetFloat(k, f), (k, c) => m.SetColor(k, c), p), 3005);
            Bake(MatRing, m => FillImpactRing((k, f) => m.SetFloat(k, f), (k, c) => m.SetColor(k, c), p), 3004);
            AssetDatabase.SaveAssets();

            var full = PrefabUtility.LoadPrefabContents(SkillPrefab);
            try
            {
                var vfx = full.GetComponent<PrismExplosionVfx>();
                if (vfx != null) ApplyVfx(vfx, p);
                PrefabUtility.SaveAsPrefabAsset(full, SkillPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(full); }

            foreach (var vfx in Object.FindObjectsByType<PrismExplosionVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(vfx)) continue;
                foreach (var mr in vfx.GetComponentsInChildren<MeshRenderer>(true))
                    mr.SetPropertyBlock(null);
            }
            Debug.Log("[PrismExplosionPreset] 재질·프리팹에 적용 완료 (라이브 오버라이드 해제)");
        }

        public static void Capture(PrismExplosionPreset p)
        {
            Undo.RecordObject(p, "Capture Prism Explosion");

            var mc = Load<Material>(MatCrystal);
            if (mc != null)
            {
                p.bodyColor = mc.GetColor("_ColorBody");
                p.rimColor = mc.GetColor("_ColorRim");
                p.crystalEmission = mc.GetFloat("_Emission");
                p.bodyAlpha = mc.GetFloat("_BodyAlpha");
                p.rimAlpha = mc.GetFloat("_RimAlpha");
                p.fresnelPow = mc.GetFloat("_FresnelPow");
                p.facetAmount = mc.GetFloat("_FacetAmount");
            }
            var mf = Load<Material>(MatFlare);
            if (mf != null) { p.flareColor = mf.GetColor("_Color"); p.flareEmission = mf.GetFloat("_Emission"); }
            var mg = Load<Material>(MatGround);
            if (mg != null) { p.groundColor = mg.GetColor("_Color"); p.groundEmission = mg.GetFloat("_Emission"); }
            var mb = Load<Material>(MatBurst);
            if (mb != null)
            {
                p.impactHotColor = mb.GetColor("_ColorHot");
                p.impactGlowColor = mb.GetColor("_ColorGold");
                p.impactEmission = mb.GetFloat("_Emission");
            }
            var mr2 = Load<Material>(MatRing);
            if (mr2 != null) p.impactRingColor = mr2.GetColor("_ColorRing");

            var full = Load<GameObject>(SkillPrefab);
            var vfx = full != null ? full.GetComponent<PrismExplosionVfx>() : null;
            if (vfx != null)
            {
                p.forwardOffset = vfx.ForwardOffset;
                p.hoverHeight = vfx.HoverHeight;
                p.prismScale = vfx.PrismScale;
                p.hoverAmp = vfx.HoverAmp;
                p.hoverFreq = vfx.HoverFreq;
                p.spinSpeed = vfx.SpinSpeed;
                p.summonTime = vfx.SummonTime;
                p.flareThreshold = vfx.FlareThreshold;
                p.flareSharp = vfx.FlareSharp;
                p.flareSize = vfx.FlareSize;
                p.groundSize = vfx.GroundSize;
                p.groundBase = vfx.GroundBase;
                p.groundPulse = vfx.GroundPulse;
                p.riseTime = vfx.RiseTime;
                p.riseHeight = vfx.RiseHeight;
                p.tremorAmp = vfx.TremorAmp;
                p.tremorFreq = vfx.TremorFreq;
                p.aimSpinMul = vfx.AimSpinMul;
                p.launchSpeed = vfx.LaunchSpeed;
                p.launchSpinMul = vfx.LaunchSpinMul;
                p.launchArc = vfx.LaunchArc;
                p.targetHeight = vfx.TargetHeight;
                p.spiralRadius = vfx.SpiralRadius;
                p.spiralRate = vfx.SpiralRate;
                p.smokeLinger = vfx.SmokeLinger;
                var im = vfx.GetComponentInChildren<FlareBombImpact>(true);
                if (im != null)
                {
                    p.impactBurstDuration = im.BurstDuration;
                    p.impactRingDuration = im.RingDuration;
                    var bh = im.transform.Find("ImpactBurstH");
                    if (bh != null) p.impactSizeH = bh.localScale.x;
                    var bv = im.transform.Find("ImpactBurstV");
                    if (bv != null) p.impactSizeV = bv.localScale.y;
                    var ring = im.transform.Find("ImpactRing");
                    if (ring != null) p.impactRingSize = ring.localScale.x;
                }
                var tr = vfx.transform.Find("PrismUnit/FlightTrail");
                var trc = tr != null ? tr.GetComponent<TrailRenderer>() : null;
                if (trc != null) { p.trailTime = trc.time; p.trailWidth = trc.widthMultiplier; }
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!ps.gameObject.name.Contains("Embers")) continue;
                    p.emberRate = ps.emission.rateOverTime.constant;
                    p.emberSize = ps.main.startSize.constantMax / 1.3f;
                    break;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[PrismExplosionPreset] 현재값 캡처 완료");
        }
    }
}
