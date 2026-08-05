using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 체인 라이팅 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    /// 대상: LightningBolt.mat + ShockAura.mat + ChainLightningSkill 프리팹(타이밍·배치).
    /// 씬 스코프: ChainLightningVfx 컴포넌트(흑암 변형은 root 이름 "Dark" 가드로 제외).
    /// 볼트 렌더러 이름 규약: MainBolt/ChainBolt(볼트), ShockAura(감전), MuzzleOrb(머즐).
    /// </summary>
    [CustomEditor(typeof(ChainLightningPreset))]
    public class ChainLightningPresetEditor : FlareOrbPresetEditorBase
    {
        const string CDIR = "Assets/RenderFX/HeroSkill/Lumina/ChainLightning";
        static string MatBolt    => CDIR + "/Materials/LightningBolt.mat";
        static string MatShock   => CDIR + "/Materials/ShockAura.mat";
        static string MatBg      => CDIR + "/Materials/ShockBurstBg.mat";
        static string SkillPrefab => CDIR + "/Prefabs/ChainLightningSkill.prefab";
        const string PresetPath  = CDIR + "/Presets/FX_ChainLightningPreset.asset";

        protected override string LiveKey => "JC.ChainLightningPreset.LivePreview";
        protected override string HelpText =>
            "라이브 프리뷰: 슬라이더를 움직이면 씬의 볼트·아우라·타이밍에 즉시 반영(MPB, 에셋 무변경).\n" +
            "previewProgress=긋기 진행도, previewSeed=플리커 형상 리롤(에디트 모드 정지 프레임).\n" +
            "적용: 재질 + ChainLightningSkill 프리팹에 확정 기록하고 프리뷰 오버라이드 해제.\n" +
            "캡처: 현재 재질/프리팹 값을 이 프리셋으로 역방향 읽기.";

        protected override void LivePush() => LivePushStatic((ChainLightningPreset)target);
        protected override void ApplyPreset() => Apply((ChainLightningPreset)target);
        protected override void CapturePreset() => Capture((ChainLightningPreset)target);

        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.ChainLightningPreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<ChainLightningPreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        static void FillBolt(System.Action<string, float> setF, System.Action<string, Color> setC,
                             ChainLightningPreset p)
        {
            setC("_ColorCore", p.boltCoreColor);
            setC("_ColorGlow", p.boltGlowColor);
            setF("_Emission", p.boltEmission);
            setF("_Width", p.boltWidth);
            setF("_SegCount", p.segCount);
            setF("_JitterAmp", p.jitterAmp);
            setF("_MicroJag", p.microJag);
            setF("_MicroFreq", p.microFreq);
            setF("_EndPin", p.endPin);
            setF("_CoreWidth", p.coreWidth);
            setF("_GlowWidth", p.glowWidth);
            setF("_HeadSize", p.headSize);
            setF("_HeadBoost", p.headBoost);
            setF("_BranchCount", p.branchCount);
            setF("_BranchLen", p.branchLen);
            setF("_BranchSlope", p.branchSlope);
            setF("_BranchWidthMul", p.branchWidthMul);
        }

        static void FillShock(System.Action<string, float> setF, System.Action<string, Color> setC,
                              ChainLightningPreset p)
        {
            setC("_ColorCore", p.shockCoreColor);
            setC("_ColorGlow", p.shockGlowColor);
            setF("_Emission", p.shockEmission);
            setF("_Radius", p.shockRadius);
            setF("_EllipseK", p.shockEllipseK);
            setF("_RadJitter", p.radJitter);
            setF("_ArcCount", p.arcCount);
            setF("_Density", p.arcDensity);
            setF("_ArcWidth", p.arcWidth);
            setF("_HighlightRatio", p.arcHighlightRatio);
            setF("_WobbleAmp", p.wobbleAmp);
            setF("_WobbleFreq", p.wobbleFreq);
            setF("_GlowAmt", p.glowAmt);
            setF("_CoreGlow", p.shockCoreGlow);
        }

        static void FillBg(System.Action<string, float> setF, System.Action<string, Color> setC,
                           ChainLightningPreset p)
        {
            setC("_ColorBody", p.bgBodyColor);
            setC("_ColorCenter", p.bgCenterColor);
            setF("_CenterSize", p.bgCenterSize);
            setF("_SpikeCount", p.bgSpikeCount);
            setF("_SpikeLen", p.bgSpikeLen);
            setF("_LenJitter", p.bgLenJitter);
            setF("_SpikeWidth", p.bgSpikeWidth);
            setF("_TaperSharp", p.bgTaperSharp);
            setF("_MaxAlpha", p.bgOpacity);
        }

        static void ApplyTimings(ChainLightningVfx vfx, ChainLightningPreset p)
        {
            vfx.MuzzleTime = p.muzzleTime;
            vfx.MuzzleSize = p.muzzleSize;
            vfx.MuzzleOffset = p.muzzleOffset;
            vfx.DrawTime = p.drawTime;
            vfx.HoldTime = p.holdTime;
            vfx.FadeTime = p.fadeTime;
            vfx.FlickerRate = p.flickerRate;
            vfx.ChainDelay = p.chainDelay;
            vfx.ChainDrawTime = p.chainDrawTime;
            vfx.ChainHoldTime = p.chainHoldTime;
            vfx.ShockDuration = p.shockDuration;
            vfx.ShockFadeTime = p.shockFadeTime;
            vfx.ShockSize = p.shockSize;
            vfx.TargetHeight = p.targetHeight;
        }

        static void LivePushStatic(ChainLightningPreset p)
        {
            foreach (var vfx in Object.FindObjectsByType<ChainLightningVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(vfx)) continue;   // 흑암 변형은 일반판 프리셋 스코프 밖
                ApplyTimings(vfx, p);
                foreach (var mr in vfx.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    string n = mr.gameObject.name;
                    if (n.Contains("Bolt")) FillBolt((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    else if (n.Contains("Bg")) FillBg((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    else FillShock((k, f) => mpb.SetFloat(k, f), (k, c) => mpb.SetColor(k, c), p);
                    if (n.Contains("Muzzle"))
                    {
                        mpb.SetFloat("_CoreGlow", p.muzzleCoreGlow);
                        if (!Application.isPlaying)
                            mr.transform.localScale = new Vector3(p.muzzleSize, p.muzzleSize, 1f);
                    }
                    if (n.Contains("ShockAura") && !Application.isPlaying)
                        mr.transform.localScale = new Vector3(p.shockSize, p.shockSize, 1f);
                    if (!Application.isPlaying)
                    {
                        mpb.SetFloat("_Progress", p.previewProgress);
                        mpb.SetFloat("_Seed", p.previewSeed);
                        mpb.SetFloat("_Opacity", 1f);
                    }
                    mr.SetPropertyBlock(mpb);
                }
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Apply(ChainLightningPreset p)
        {
            var matB = Load<Material>(MatBolt);
            FillBolt((k, f) => matB.SetFloat(k, f), (k, c) => matB.SetColor(k, c), p);
            matB.renderQueue = 3006;
            EditorUtility.SetDirty(matB);
            var matS = Load<Material>(MatShock);
            FillShock((k, f) => matS.SetFloat(k, f), (k, c) => matS.SetColor(k, c), p);
            matS.renderQueue = 3005;
            EditorUtility.SetDirty(matS);
            var matBg = Load<Material>(MatBg);
            if (matBg != null)
            {
                FillBg((k, f) => matBg.SetFloat(k, f), (k, c) => matBg.SetColor(k, c), p);
                matBg.renderQueue = 3004;
                EditorUtility.SetDirty(matBg);
            }
            AssetDatabase.SaveAssets();

            var full = PrefabUtility.LoadPrefabContents(SkillPrefab);
            try
            {
                var vfx = full.GetComponent<ChainLightningVfx>();
                if (vfx != null)
                {
                    var so = new SerializedObject(vfx);
                    so.FindProperty("muzzleTime").floatValue = p.muzzleTime;
                    so.FindProperty("muzzleSize").floatValue = p.muzzleSize;
                    so.FindProperty("muzzleOffset").vector3Value = p.muzzleOffset;
                    so.FindProperty("drawTime").floatValue = p.drawTime;
                    so.FindProperty("holdTime").floatValue = p.holdTime;
                    so.FindProperty("fadeTime").floatValue = p.fadeTime;
                    so.FindProperty("flickerRate").floatValue = p.flickerRate;
                    so.FindProperty("chainDelay").floatValue = p.chainDelay;
                    so.FindProperty("chainDrawTime").floatValue = p.chainDrawTime;
                    so.FindProperty("chainHoldTime").floatValue = p.chainHoldTime;
                    so.FindProperty("shockDuration").floatValue = p.shockDuration;
                    so.FindProperty("shockFadeTime").floatValue = p.shockFadeTime;
                    so.FindProperty("shockSize").floatValue = p.shockSize;
                    so.FindProperty("targetHeight").floatValue = p.targetHeight;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var muzzle = full.transform.Find("MuzzleOrb");
                    if (muzzle != null) muzzle.localScale = new Vector3(p.muzzleSize, p.muzzleSize, 1f);
                    var shock = full.transform.Find("ShockAura");
                    if (shock != null) shock.localScale = new Vector3(p.shockSize, p.shockSize, 1f);
                }
                PrefabUtility.SaveAsPrefabAsset(full, SkillPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(full); }

            foreach (var vfx in Object.FindObjectsByType<ChainLightningVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (InDarkVariant(vfx)) continue;
                foreach (var mr in vfx.GetComponentsInChildren<MeshRenderer>(true))
                    mr.SetPropertyBlock(null);
            }
            Debug.Log("[ChainLightningPreset] 재질·프리팹에 적용 완료 (라이브 오버라이드 해제)");
        }

        public static void Capture(ChainLightningPreset p)
        {
            Undo.RecordObject(p, "Capture Chain Lightning");

            var matB = Load<Material>(MatBolt);
            p.boltCoreColor = matB.GetColor("_ColorCore");
            p.boltGlowColor = matB.GetColor("_ColorGlow");
            p.boltEmission = matB.GetFloat("_Emission");
            p.boltWidth = matB.GetFloat("_Width");
            p.segCount = Mathf.RoundToInt(matB.GetFloat("_SegCount"));
            p.jitterAmp = matB.GetFloat("_JitterAmp");
            p.microJag = matB.GetFloat("_MicroJag");
            p.microFreq = matB.GetFloat("_MicroFreq");
            p.endPin = matB.GetFloat("_EndPin");
            p.coreWidth = matB.GetFloat("_CoreWidth");
            p.glowWidth = matB.GetFloat("_GlowWidth");
            p.headSize = matB.GetFloat("_HeadSize");
            p.headBoost = matB.GetFloat("_HeadBoost");
            p.branchCount = Mathf.RoundToInt(matB.GetFloat("_BranchCount"));
            p.branchLen = matB.GetFloat("_BranchLen");
            p.branchSlope = matB.GetFloat("_BranchSlope");
            p.branchWidthMul = matB.GetFloat("_BranchWidthMul");

            var matS = Load<Material>(MatShock);
            p.shockCoreColor = matS.GetColor("_ColorCore");
            p.shockGlowColor = matS.GetColor("_ColorGlow");
            p.shockEmission = matS.GetFloat("_Emission");
            p.shockRadius = matS.GetFloat("_Radius");
            p.shockEllipseK = matS.GetFloat("_EllipseK");
            p.radJitter = matS.GetFloat("_RadJitter");
            p.arcCount = Mathf.RoundToInt(matS.GetFloat("_ArcCount"));
            p.arcDensity = matS.GetFloat("_Density");
            p.arcWidth = matS.GetFloat("_ArcWidth");
            p.arcHighlightRatio = matS.GetFloat("_HighlightRatio");
            p.wobbleAmp = matS.GetFloat("_WobbleAmp");
            p.wobbleFreq = matS.GetFloat("_WobbleFreq");
            p.glowAmt = matS.GetFloat("_GlowAmt");
            p.shockCoreGlow = matS.GetFloat("_CoreGlow");

            var matBg = Load<Material>(MatBg);
            if (matBg != null)
            {
                p.bgBodyColor = matBg.GetColor("_ColorBody");
                p.bgCenterColor = matBg.GetColor("_ColorCenter");
                p.bgCenterSize = matBg.GetFloat("_CenterSize");
                p.bgSpikeCount = Mathf.RoundToInt(matBg.GetFloat("_SpikeCount"));
                p.bgSpikeLen = matBg.GetFloat("_SpikeLen");
                p.bgLenJitter = matBg.GetFloat("_LenJitter");
                p.bgSpikeWidth = matBg.GetFloat("_SpikeWidth");
                p.bgTaperSharp = matBg.GetFloat("_TaperSharp");
                p.bgOpacity = matBg.GetFloat("_MaxAlpha");
            }

            var full = Load<GameObject>(SkillPrefab);
            var vfx = full != null ? full.GetComponent<ChainLightningVfx>() : null;
            if (vfx != null)
            {
                p.muzzleTime = vfx.MuzzleTime;
                p.muzzleSize = vfx.MuzzleSize;
                p.muzzleOffset = vfx.MuzzleOffset;
                p.drawTime = vfx.DrawTime;
                p.holdTime = vfx.HoldTime;
                p.fadeTime = vfx.FadeTime;
                p.flickerRate = vfx.FlickerRate;
                p.chainDelay = vfx.ChainDelay;
                p.chainDrawTime = vfx.ChainDrawTime;
                p.chainHoldTime = vfx.ChainHoldTime;
                p.shockDuration = vfx.ShockDuration;
                p.shockFadeTime = vfx.ShockFadeTime;
                p.shockSize = vfx.ShockSize;
                p.targetHeight = vfx.TargetHeight;
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[ChainLightningPreset] 현재값 캡처 완료");
        }
    }
}
