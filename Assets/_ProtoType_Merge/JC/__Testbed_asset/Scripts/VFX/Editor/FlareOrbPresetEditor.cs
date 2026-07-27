using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// F0(필드 방식) 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    /// F0 전담: 코어 재질 + 림 스프라이트 + 광원 + 필드 오라 재질(전/후면).
    /// 렌더 큐(겹침 구성): F1후면 2995 < F0후면 2996 < 림 2997 < 코어 3000 < F0전면 3001 < F1전면 3002.
    /// </summary>
    [CustomEditor(typeof(FlareOrbPreset))]
    public class FlareOrbPresetEditor : FlareOrbPresetEditorBase
    {
        static string MatCore     => DIR + "/FlareOrbCore.mat";
        static string MatRim      => DIR + "/FlareCoreRim.mat";
        static string MatAura     => DIR + "/FlareAuraTongue.mat";
        static string MatAuraBack => DIR + "/FlareAuraTongueBack.mat";
        static string OrbPrefab   => DIR + "/FlareBombOrb.prefab";
        const string PresetPath   = DIR + "/F0_FlareOrbPreset.asset";

        protected override string LiveKey => "JC.FlareOrbPreset.LivePreview";
        protected override string HelpText =>
            "라이브 프리뷰: 슬라이더를 움직이면 씬의 F0 오브(FlareBombOrb)에 즉시 반영(MPB, 에셋 무변경). 플레이 중에도 동작.\n" +
            "적용: 이 값을 코어/림/오라 재질 + FlareBombOrb 프리팹(셸 형태·광원)에 확정 기록하고 프리뷰 오버라이드를 해제.\n" +
            "캡처: 현재 재질/프리팹 값을 이 프리셋으로 역방향 읽기.";

        protected override void LivePush() => LivePushStatic((FlareOrbPreset)target);
        protected override void ApplyPreset() => Apply((FlareOrbPreset)target);
        protected override void CapturePreset() => Capture((FlareOrbPreset)target);

        // 플레이모드 진입 시 씬 리로드로 MPB(라이브 프리뷰)가 초기화되므로 자동 재푸시
        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.FlareOrbPreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<FlareOrbPreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        // ---------- 재질 값 채우기 ----------

        static void FillCoreMpb(MaterialPropertyBlock mpb, FlareOrbPreset p)
        {
            mpb.SetColor("_ColorCore", p.coreColor);
            mpb.SetColor("_ColorMid", p.coreMidColor);
            mpb.SetColor("_ColorRim", p.coreRimColor);
            mpb.SetFloat("_EmissionStrength", p.coreEmission);
            mpb.SetFloat("_BaseAlpha", p.coreBaseAlpha);
            mpb.SetFloat("_RimPower", p.coreRimPower);
            mpb.SetFloat("_RimStrength", 0f);   // 림은 CoreRim 스프라이트 담당
            mpb.SetFloat("_NoiseScale", p.coreNoiseScale);
            mpb.SetFloat("_NoiseSpeed", p.coreNoiseSpeed);
            mpb.SetFloat("_SwirlSpeed", p.coreSwirlSpeed);
        }

        static void FillRimMpb(MaterialPropertyBlock mpb, FlareOrbPreset p)
        {
            mpb.SetColor("_Color", p.coreRimColor);
            mpb.SetFloat("_Intensity", p.coreRimStrength);
            mpb.SetFloat("_RingRadius", p.rimRadius);
            mpb.SetFloat("_RingWidth", p.rimWidth);
            mpb.SetFloat("_HaloStrength", p.rimHalo);
        }

        static void FillAuraMpb(MaterialPropertyBlock mpb, FlareOrbPreset p, bool back)
        {
            mpb.SetColor("_ColorTongue", p.tongueColor);
            mpb.SetColor("_ColorHighlight", p.tongueHighlightColor);
            mpb.SetFloat("_Emission", back ? p.tongueEmission * p.backEmissionMul : p.tongueEmission);
            mpb.SetFloat("_Opacity", back ? p.backOpacity : 1f);
            mpb.SetFloat("_NoiseScale", p.patternScale);
            mpb.SetFloat("_VStretch", p.patternVStretch);
            mpb.SetFloat("_Detail", p.patternDetail);
            mpb.SetFloat("_RidgeMix", p.ridgeMix);
            mpb.SetFloat("_TaperSharp", p.taperSharp);
            mpb.SetFloat("_BreakScale", p.breakScale);
            mpb.SetFloat("_BreakAmount", p.breakAmount);
            mpb.SetFloat("_Threshold", p.patternThreshold);
            mpb.SetFloat("_EdgeSoftRatio", p.edgeSoftRatio);
            mpb.SetFloat("_HighlightShift", p.highlightShift);
            mpb.SetFloat("_FlowSpeed", p.flowSpeed);
            mpb.SetFloat("_Waver", p.waver);
            mpb.SetFloat("_WaverSpeed", p.waverSpeed);
            mpb.SetFloat("_Shear", p.spiralShear);
            mpb.SetFloat("_SCurveAmount", p.sCurveAmount);
            mpb.SetFloat("_SCurveFreq", p.sCurveFreq);
            mpb.SetFloat("_SCurveFollow", p.sCurveFollow);
            mpb.SetFloat("_SCurveUpperRatio", p.sCurveUpperRatio);
            mpb.SetFloat("_TipErodeStart", p.tipErodeStart);
            mpb.SetFloat("_TipErodeStrength", p.tipErodeStrength);
            mpb.SetFloat("_SpeckAmount", p.speckAmount);
            mpb.SetFloat("_SpeckScale", p.speckScale);
        }

        // ---------- 라이브 프리뷰 ----------

        static void LivePushStatic(FlareOrbPreset p)
        {
            foreach (var shell in Object.FindObjectsByType<FlareOrbShell>(FindObjectsSortMode.None))
            {
                if (IsAltShell(shell)) continue;   // Alt 계열은 F1 프리셋 관할
                if (InDarkVariant(shell)) continue;   // 흑염 변형은 크림판 프리셋 스코프 밖
                bool isBack = shell.gameObject.name.Contains("Back");
                ApplyShellToInstance(shell, p, isBack);

                var mr = shell.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    FillAuraMpb(mpb, p, isBack);
                    mr.SetPropertyBlock(mpb);
                }

                if (isBack) continue;   // 이하 공용 자식 처리는 전면 셸 기준 1회만
                var root = shell.transform.parent;
                if (root == null) continue;

                var coreT = root.Find("Core_Sphere");
                if (coreT != null)
                {
                    var cmr = coreT.GetComponent<MeshRenderer>();
                    if (cmr != null)
                    {
                        var mpb = new MaterialPropertyBlock();
                        cmr.GetPropertyBlock(mpb);
                        FillCoreMpb(mpb, p);
                        cmr.SetPropertyBlock(mpb);
                    }
                }

                var rimT = root.Find("CoreRim");
                if (rimT != null)
                {
                    var rmr = rimT.GetComponent<MeshRenderer>();
                    if (rmr != null)
                    {
                        var mpb = new MaterialPropertyBlock();
                        rmr.GetPropertyBlock(mpb);
                        FillRimMpb(mpb, p);
                        rmr.SetPropertyBlock(mpb);
                    }
                }

                var lightT = root.Find("FireLight");
                if (lightT != null)
                {
                    var light = lightT.GetComponent<Light>();
                    if (light != null) { light.color = p.lightColor; light.range = p.lightRange; }
                    var flicker = lightT.GetComponent<FireLightFlicker>();
                    if (flicker != null)
                    {
                        flicker.baseIntensity = p.lightIntensity;
                        flicker.intensityAmplitude = p.lightFlickerAmplitude;
                    }
                }
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        // ---------- 확정 적용 / 캡처 ----------

        public static void Apply(FlareOrbPreset p)
        {
            var core = Load<Material>(MatCore);
            core.SetColor("_ColorCore", p.coreColor);
            core.SetColor("_ColorMid", p.coreMidColor);
            core.SetColor("_ColorRim", p.coreRimColor);
            core.SetFloat("_EmissionStrength", p.coreEmission);
            core.SetFloat("_BaseAlpha", p.coreBaseAlpha);
            core.SetFloat("_RimPower", p.coreRimPower);
            core.SetFloat("_RimStrength", 0f);   // 림은 CoreRim 스프라이트 담당
            core.SetFloat("_NoiseScale", p.coreNoiseScale);
            core.SetFloat("_NoiseSpeed", p.coreNoiseSpeed);
            core.SetFloat("_SwirlSpeed", p.coreSwirlSpeed);
            core.renderQueue = 3000;
            EditorUtility.SetDirty(core);

            var rim = Load<Material>(MatRim);
            if (rim != null)
            {
                rim.SetColor("_Color", p.coreRimColor);
                rim.SetFloat("_Intensity", p.coreRimStrength);
                rim.SetFloat("_RingRadius", p.rimRadius);
                rim.SetFloat("_RingWidth", p.rimWidth);
                rim.SetFloat("_HaloStrength", p.rimHalo);
                rim.renderQueue = 2997;
                EditorUtility.SetDirty(rim);
            }

            var aura = Load<Material>(MatAura);
            aura.SetColor("_ColorTongue", p.tongueColor);
            aura.SetColor("_ColorHighlight", p.tongueHighlightColor);
            aura.SetFloat("_Emission", p.tongueEmission);
            aura.SetFloat("_Opacity", 1f);
            aura.SetFloat("_NoiseScale", p.patternScale);
            aura.SetFloat("_VStretch", p.patternVStretch);
            aura.SetFloat("_Detail", p.patternDetail);
            aura.SetFloat("_RidgeMix", p.ridgeMix);
            aura.SetFloat("_TaperSharp", p.taperSharp);
            aura.SetFloat("_BreakScale", p.breakScale);
            aura.SetFloat("_BreakAmount", p.breakAmount);
            aura.SetFloat("_Threshold", p.patternThreshold);
            aura.SetFloat("_EdgeSoftRatio", p.edgeSoftRatio);
            aura.SetFloat("_HighlightShift", p.highlightShift);
            aura.SetFloat("_FlowSpeed", p.flowSpeed);
            aura.SetFloat("_Waver", p.waver);
            aura.SetFloat("_WaverSpeed", p.waverSpeed);
            aura.SetFloat("_Shear", p.spiralShear);
            aura.SetFloat("_SCurveAmount", p.sCurveAmount);
            aura.SetFloat("_SCurveFreq", p.sCurveFreq);
            aura.SetFloat("_SCurveFollow", p.sCurveFollow);
            aura.SetFloat("_SCurveUpperRatio", p.sCurveUpperRatio);
            aura.SetFloat("_TipErodeStart", p.tipErodeStart);
            aura.SetFloat("_TipErodeStrength", p.tipErodeStrength);
            aura.SetFloat("_SpeckAmount", p.speckAmount);
            aura.SetFloat("_SpeckScale", p.speckScale);
            aura.renderQueue = 3001;
            EditorUtility.SetDirty(aura);

            var back = Load<Material>(MatAuraBack);
            if (back != null)
            {
                back.CopyPropertiesFromMaterial(aura);
                back.SetFloat("_Emission", p.tongueEmission * p.backEmissionMul);
                back.SetFloat("_Opacity", p.backOpacity);
                back.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
                back.renderQueue = 2996;
                EditorUtility.SetDirty(back);
            }
            AssetDatabase.SaveAssets();

            var orb = PrefabUtility.LoadPrefabContents(OrbPrefab);
            try
            {
                ApplyShellsToPrefab(orb, p);

                var lightT = orb.transform.Find("FireLight");
                if (lightT != null)
                {
                    var light = lightT.GetComponent<Light>();
                    light.color = p.lightColor;
                    light.range = p.lightRange;
                    var flicker = lightT.GetComponent<FireLightFlicker>();
                    if (flicker != null)
                    {
                        flicker.baseIntensity = p.lightIntensity;
                        flicker.intensityAmplitude = p.lightFlickerAmplitude;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(orb, OrbPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(orb); }

            ClearShellOverrides(s => !IsAltShell(s), true);
            Debug.Log("[FlareOrbPreset] 프리팹에 적용 완료 (라이브 오버라이드 해제)");
        }

        public static void Capture(FlareOrbPreset p)
        {
            Undo.RecordObject(p, "Capture Flare Orb F0");

            var core = Load<Material>(MatCore);
            p.coreColor = core.GetColor("_ColorCore");
            p.coreMidColor = core.GetColor("_ColorMid");
            p.coreEmission = core.GetFloat("_EmissionStrength");
            p.coreBaseAlpha = core.GetFloat("_BaseAlpha");
            p.coreRimPower = core.GetFloat("_RimPower");
            p.coreNoiseScale = core.GetFloat("_NoiseScale");
            p.coreNoiseSpeed = core.GetFloat("_NoiseSpeed");
            p.coreSwirlSpeed = core.GetFloat("_SwirlSpeed");

            var rim = Load<Material>(MatRim);
            if (rim != null)
            {
                p.coreRimColor = rim.GetColor("_Color");
                p.coreRimStrength = rim.GetFloat("_Intensity");
                p.rimRadius = rim.GetFloat("_RingRadius");
                p.rimWidth = rim.GetFloat("_RingWidth");
                p.rimHalo = rim.GetFloat("_HaloStrength");
            }

            var aura = Load<Material>(MatAura);
            p.tongueColor = aura.GetColor("_ColorTongue");
            p.tongueHighlightColor = aura.GetColor("_ColorHighlight");
            p.tongueEmission = aura.GetFloat("_Emission");
            p.patternScale = aura.GetFloat("_NoiseScale");
            p.patternVStretch = aura.GetFloat("_VStretch");
            p.patternDetail = aura.GetFloat("_Detail");
            p.ridgeMix = aura.GetFloat("_RidgeMix");
            p.taperSharp = aura.GetFloat("_TaperSharp");
            p.breakScale = aura.GetFloat("_BreakScale");
            p.breakAmount = aura.GetFloat("_BreakAmount");
            p.patternThreshold = aura.GetFloat("_Threshold");
            p.edgeSoftRatio = aura.GetFloat("_EdgeSoftRatio");
            p.highlightShift = aura.GetFloat("_HighlightShift");
            p.flowSpeed = aura.GetFloat("_FlowSpeed");
            p.waver = aura.GetFloat("_Waver");
            p.waverSpeed = aura.GetFloat("_WaverSpeed");
            p.spiralShear = aura.GetFloat("_Shear");
            p.sCurveAmount = aura.GetFloat("_SCurveAmount");
            p.sCurveFreq = aura.GetFloat("_SCurveFreq");
            p.sCurveFollow = aura.GetFloat("_SCurveFollow");
            p.sCurveUpperRatio = aura.GetFloat("_SCurveUpperRatio");
            p.tipErodeStart = aura.GetFloat("_TipErodeStart");
            p.tipErodeStrength = aura.GetFloat("_TipErodeStrength");
            p.speckAmount = aura.GetFloat("_SpeckAmount");
            p.speckScale = aura.GetFloat("_SpeckScale");

            var backMat = Load<Material>(MatAuraBack);
            if (backMat != null)
            {
                p.backOpacity = backMat.GetFloat("_Opacity");
                float frontEm = Mathf.Max(aura.GetFloat("_Emission"), 0.001f);
                p.backEmissionMul = backMat.GetFloat("_Emission") / frontEm;
            }

            var orb = Load<GameObject>(OrbPrefab);
            CaptureShellsFromPrefab(orb, p);

            var lightT = orb.transform.Find("FireLight");
            if (lightT != null)
            {
                var light = lightT.GetComponent<Light>();
                p.lightColor = light.color;
                p.lightRange = light.range;
                var flicker = lightT.GetComponent<FireLightFlicker>();
                if (flicker != null)
                {
                    p.lightIntensity = flicker.baseIntensity;
                    p.lightFlickerAmplitude = flicker.intensityAmplitude;
                }
            }

            EditorUtility.SetDirty(p);
            Debug.Log("[FlareOrbPreset] 현재값 캡처 완료");
        }
    }
}
