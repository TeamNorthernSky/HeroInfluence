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
        // ★변종 대응(260730): 경로를 프리셋의 variantSuffix로 만든다. 비면 종전과 동일(크림판).
        static string Sfx(FlareOrbSpritePreset p) => p != null && !string.IsNullOrEmpty(p.variantSuffix) ? p.variantSuffix : "";
        static string MatSprite(FlareOrbSpritePreset p) => DIR + "/Materials/FlareAuraSprite" + Sfx(p) + ".mat";
        /// <summary>통합 프리팹만 흑염 이름에 언더바가 없다(FlareBombOrbFullDark) — 접미사에서 '_'를 뺀다.</summary>
        static string FullPrefab(FlareOrbSpritePreset p) => DIR + "/Prefabs/FlareBombOrbFull" + Sfx(p).Replace("_", "") + ".prefab";

        /// <summary>이 프리셋이 흑염 변종을 대상으로 하는가 — 라이브 프리뷰 스코프를 가른다.</summary>
        static bool WantsDark(FlareOrbSpritePreset p) => Sfx(p).Contains("Dark");
        const string PresetPath  = DIR + "/Presets/F2_FlareOrbSpritePreset.asset";

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
            setF("_EllipseRatio", p.ellipseRatio);
            setF("_MaxReach", p.maxReach);
            setF("_ReachRatio", p.reachRatio);
            setF("_ReachSoft", p.reachSoft);
            setF("_YOffset", p.patternYOffset);
            setF("_EmitterDepth", p.emitterDepth);
            setF("_CutHeight", p.cutHeight);
            setF("_LaneCount", p.laneCount);
            setF("_LaneWidth", p.laneWidth);
            setF("_LaneWidthJitter", p.laneWidthJitter);
            setF("_LanePosJitter", p.lanePosJitter);
            setF("_LaneTiltJitter", p.laneTiltJitter);
            setF("_WidthNoiseScale", p.widthNoiseScale);
            setF("_WidthNoiseAmount", p.widthNoiseAmount);
            setF("_BirthMin", p.birthOffsetMin);
            setF("_BirthMax", p.birthOffsetMax);
            setF("_BirthBias", p.birthInnerBias);
            setF("_VLengthJitter", p.laneLengthJitter);
            setF("_MaxLen", p.laneMaxLength);
            setF("_OuterShrink", p.outerShrink);
            setF("_CutFarShrink", p.cutFarShrink);
            setF("_TravelDist", p.laneTravelDist);
            setF("_TravelSpeed", p.laneTravelSpeed);
            setF("_LifeFadePeak", p.laneLifeFadePeak);
            setF("_TaperSharp", p.taperSharp);
            setF("_HighlightRatio", p.highlightRatio);
            setF("_EdgeSoftRatio", p.edgeSoftRatio);
            setF("_FlowSpeed", p.flowSpeed);
            setF("_SCurveAmount", p.sCurveAmount);
            setF("_SCurveFreq", p.sCurveFreq);
            setF("_DebugCircles", p.debugCircles);
        }

        static void LivePushStatic(FlareOrbSpritePreset p)
        {
            foreach (var aura in Object.FindObjectsByType<FlareOrbSpriteAura>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // ★변종 스코프(260730): 크림판은 흑염을 건너뛰고, 흑염 프리셋은 흑염만 만진다.
                if (InDarkVariant(aura) != WantsDark(p)) continue;
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
            var mat = Load<Material>(MatSprite(p));
            FillSprite((n, f) => mat.SetFloat(n, f), (n, c) => mat.SetColor(n, c), p);
            mat.renderQueue = 3002;   // 코어 위, 최상단
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            var full = PrefabUtility.LoadPrefabContents(FullPrefab(p));
            try
            {
                var aura = full.GetComponentInChildren<FlareOrbSpriteAura>(true);
                if (aura != null)
                {
                    var so = new SerializedObject(aura);
                    so.FindProperty("worldSize").floatValue = p.worldSize;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(full, FullPrefab(p));
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

            var mat = Load<Material>(MatSprite(p));
            p.tongueColor = mat.GetColor("_ColorTongue");
            p.tongueHighlightColor = mat.GetColor("_ColorHighlight");
            p.tongueEmission = mat.GetFloat("_Emission");
            p.baseRadius = mat.GetFloat("_BaseRadius");
            p.ellipseRatio = mat.GetFloat("_EllipseRatio");
            p.maxReach = mat.GetFloat("_MaxReach");
            p.reachRatio = mat.GetFloat("_ReachRatio");
            p.reachSoft = mat.GetFloat("_ReachSoft");
            p.patternYOffset = mat.GetFloat("_YOffset");
            p.emitterDepth = mat.GetFloat("_EmitterDepth");
            p.cutHeight = mat.GetFloat("_CutHeight");
            p.laneCount = Mathf.RoundToInt(mat.GetFloat("_LaneCount"));
            p.laneWidth = mat.GetFloat("_LaneWidth");
            p.laneWidthJitter = mat.GetFloat("_LaneWidthJitter");
            p.lanePosJitter = mat.GetFloat("_LanePosJitter");
            p.laneTiltJitter = mat.GetFloat("_LaneTiltJitter");
            p.widthNoiseScale = mat.GetFloat("_WidthNoiseScale");
            p.widthNoiseAmount = mat.GetFloat("_WidthNoiseAmount");
            p.birthOffsetMin = mat.GetFloat("_BirthMin");
            p.birthOffsetMax = mat.GetFloat("_BirthMax");
            p.birthInnerBias = mat.GetFloat("_BirthBias");
            p.laneLengthJitter = mat.GetFloat("_VLengthJitter");
            p.laneMaxLength = mat.GetFloat("_MaxLen");
            p.outerShrink = mat.GetFloat("_OuterShrink");
            p.cutFarShrink = mat.GetFloat("_CutFarShrink");
            p.laneTravelDist = mat.GetFloat("_TravelDist");
            p.laneTravelSpeed = mat.GetFloat("_TravelSpeed");
            p.laneLifeFadePeak = mat.GetFloat("_LifeFadePeak");
            p.taperSharp = mat.GetFloat("_TaperSharp");
            p.highlightRatio = mat.GetFloat("_HighlightRatio");
            p.edgeSoftRatio = mat.GetFloat("_EdgeSoftRatio");
            p.flowSpeed = mat.GetFloat("_FlowSpeed");
            p.sCurveAmount = mat.GetFloat("_SCurveAmount");
            p.sCurveFreq = mat.GetFloat("_SCurveFreq");
            p.debugCircles = mat.GetFloat("_DebugCircles");

            var full = Load<GameObject>(FullPrefab(p));
            var aura = full != null ? full.GetComponentInChildren<FlareOrbSpriteAura>(true) : null;
            if (aura != null) p.worldSize = aura.worldSize;

            EditorUtility.SetDirty(p);
            Debug.Log("[FlareOrbSpritePreset] 현재값 캡처 완료");
        }
    }
}
