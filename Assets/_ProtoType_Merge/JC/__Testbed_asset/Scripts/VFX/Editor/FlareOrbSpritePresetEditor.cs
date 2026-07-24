using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// F2(2D 스프라이트 방식) 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    /// 대상: FlareAuraSprite.mat + FlareBombOrbFull 프리팹의 FlareOrbSprite2D 자식(worldSize).
    /// 씬 스코프: FlareOrbSpriteAura 마커 컴포넌트로 탐색(F0/F1과 무간섭).
    /// </summary>
    [CustomEditor(typeof(FlareOrbSpritePreset))]
    public class FlareOrbSpritePresetEditor : FlareOrbPresetEditorBase
    {
        static string MatSprite  => DIR + "/FlareAuraSprite.mat";
        static string FullPrefab => DIR + "/FlareBombOrbFull.prefab";
        const string PresetPath  = DIR + "/F2_FlareOrbSpritePreset.asset";

        protected override string LiveKey => "JC.FlareOrbSpritePreset.LivePreview";
        protected override string HelpText =>
            "라이브 프리뷰: 슬라이더를 움직이면 씬의 F2 스프라이트에 즉시 반영(MPB, 에셋 무변경). 플레이 중에도 동작.\n" +
            "적용: 이 값을 스프라이트 재질 + FlareBombOrbFull 프리팹(worldSize)에 확정 기록하고 프리뷰 오버라이드를 해제.\n" +
            "캡처: 현재 재질/프리팹 값을 이 프리셋으로 역방향 읽기.\n" +
            "F1↔F2 전환은 FlareBombOrbFull 루트의 FlareOrbLayerToggle에서.";

        protected override void LivePush() => LivePushStatic((FlareOrbSpritePreset)target);
        protected override void ApplyPreset() => Apply((FlareOrbSpritePreset)target);
        protected override void CapturePreset() => Capture((FlareOrbSpritePreset)target);

        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.FlareOrbSpritePreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<FlareOrbSpritePreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        static void FillSprite(System.Action<string, float> setF, System.Action<string, Color> setC,
                               FlareOrbSpritePreset p)
        {
            setC("_ColorTongue", p.tongueColor);
            setC("_ColorHighlight", p.tongueHighlightColor);
            setF("_Emission", p.tongueEmission);
            setF("_BaseRadius", p.baseRadius);
            setF("_MaxReach", p.maxReach);
            setF("_ReachSoft", p.reachSoft);
            setF("_LaneCount", p.laneCount);
            setF("_LaneWidth", p.laneWidth);
            setF("_LaneWidthJitter", p.laneWidthJitter);
            setF("_LanePosJitter", p.lanePosJitter);
            setF("_LaneTiltJitter", p.laneTiltJitter);
            setF("_WidthNoiseScale", p.widthNoiseScale);
            setF("_WidthNoiseAmount", p.widthNoiseAmount);
            setF("_RStart", p.birthRadiusStart);
            setF("_REnd", p.birthRadiusEnd);
            setF("_RJitter", p.birthRadiusJitter);
            setF("_BirthBias", p.birthInnerBias);
            setF("_VLengthJitter", p.laneLengthJitter);
            setF("_MaxLen", p.laneMaxLength);
            setF("_OuterShrink", p.outerShrink);
            setF("_TravelDist", p.laneTravelDist);
            setF("_TravelSpeed", p.laneTravelSpeed);
            setF("_LifeFadePeak", p.laneLifeFadePeak);
            setF("_TaperSharp", p.taperSharp);
            setF("_HighlightRatio", p.highlightRatio);
            setF("_EdgeSoftRatio", p.edgeSoftRatio);
            setF("_FlowSpeed", p.flowSpeed);
            setF("_SpinSpeed", p.spinSpeed);
            setF("_UpBias", p.upBias);
            setF("_SCurveAmount", p.sCurveAmount);
            setF("_SCurveFreq", p.sCurveFreq);
            setF("_DebugCircles", p.debugCircles);
        }

        static void LivePushStatic(FlareOrbSpritePreset p)
        {
            foreach (var aura in Object.FindObjectsByType<FlareOrbSpriteAura>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                aura.worldSize = p.worldSize;
                var mr = aura.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    FillSprite((n, f) => mpb.SetFloat(n, f), (n, c) => mpb.SetColor(n, c), p);
                    mr.SetPropertyBlock(mpb);
                }
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Apply(FlareOrbSpritePreset p)
        {
            var mat = Load<Material>(MatSprite);
            FillSprite((n, f) => mat.SetFloat(n, f), (n, c) => mat.SetColor(n, c), p);
            mat.renderQueue = 3002;   // 코어 위, 최상단
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            var full = PrefabUtility.LoadPrefabContents(FullPrefab);
            try
            {
                var aura = full.GetComponentInChildren<FlareOrbSpriteAura>(true);
                if (aura != null)
                {
                    var so = new SerializedObject(aura);
                    so.FindProperty("worldSize").floatValue = p.worldSize;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(full, FullPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(full); }

            foreach (var aura in Object.FindObjectsByType<FlareOrbSpriteAura>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mr = aura.GetComponent<MeshRenderer>();
                if (mr != null) mr.SetPropertyBlock(null);
            }
            Debug.Log("[FlareOrbSpritePreset] 프리팹에 적용 완료 (라이브 오버라이드 해제)");
        }

        public static void Capture(FlareOrbSpritePreset p)
        {
            Undo.RecordObject(p, "Capture Flare Orb F2");

            var mat = Load<Material>(MatSprite);
            p.tongueColor = mat.GetColor("_ColorTongue");
            p.tongueHighlightColor = mat.GetColor("_ColorHighlight");
            p.tongueEmission = mat.GetFloat("_Emission");
            p.baseRadius = mat.GetFloat("_BaseRadius");
            p.maxReach = mat.GetFloat("_MaxReach");
            p.reachSoft = mat.GetFloat("_ReachSoft");
            p.laneCount = Mathf.RoundToInt(mat.GetFloat("_LaneCount"));
            p.laneWidth = mat.GetFloat("_LaneWidth");
            p.laneWidthJitter = mat.GetFloat("_LaneWidthJitter");
            p.lanePosJitter = mat.GetFloat("_LanePosJitter");
            p.laneTiltJitter = mat.GetFloat("_LaneTiltJitter");
            p.widthNoiseScale = mat.GetFloat("_WidthNoiseScale");
            p.widthNoiseAmount = mat.GetFloat("_WidthNoiseAmount");
            p.birthRadiusStart = mat.GetFloat("_RStart");
            p.birthRadiusEnd = mat.GetFloat("_REnd");
            p.birthRadiusJitter = mat.GetFloat("_RJitter");
            p.birthInnerBias = mat.GetFloat("_BirthBias");
            p.laneLengthJitter = mat.GetFloat("_VLengthJitter");
            p.laneMaxLength = mat.GetFloat("_MaxLen");
            p.outerShrink = mat.GetFloat("_OuterShrink");
            p.laneTravelDist = mat.GetFloat("_TravelDist");
            p.laneTravelSpeed = mat.GetFloat("_TravelSpeed");
            p.laneLifeFadePeak = mat.GetFloat("_LifeFadePeak");
            p.taperSharp = mat.GetFloat("_TaperSharp");
            p.highlightRatio = mat.GetFloat("_HighlightRatio");
            p.edgeSoftRatio = mat.GetFloat("_EdgeSoftRatio");
            p.flowSpeed = mat.GetFloat("_FlowSpeed");
            p.spinSpeed = mat.GetFloat("_SpinSpeed");
            p.upBias = mat.GetFloat("_UpBias");
            p.sCurveAmount = mat.GetFloat("_SCurveAmount");
            p.sCurveFreq = mat.GetFloat("_SCurveFreq");
            p.debugCircles = mat.GetFloat("_DebugCircles");

            var full = Load<GameObject>(FullPrefab);
            var aura = full != null ? full.GetComponentInChildren<FlareOrbSpriteAura>(true) : null;
            if (aura != null) p.worldSize = aura.worldSize;

            EditorUtility.SetDirty(p);
            Debug.Log("[FlareOrbSpritePreset] 현재값 캡처 완료");
        }
    }
}
