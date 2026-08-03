using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// F1(레인 방식) 프리셋 에디터. 공통 골격은 FlareOrbPresetEditorBase 참조.
    /// F1 전담: 레인 획 재질(전/후면)뿐 — 코어/림/광원은 겹침 구성에서 F0 담당.
    /// 렌더 큐(겹침 구성): F1후면 2995(최심) < F0후면 2996 < 림 2997 < 코어 3000 < F0전면 3001 < F1전면 3002(최상).
    /// </summary>
    [CustomEditor(typeof(FlareOrbAltPreset))]
    public class FlareOrbAltPresetEditor : FlareOrbPresetEditorBase
    {
        // ★변종 대응(260730): 경로를 프리셋의 variantSuffix로 만든다. 비면 종전과 동일(크림판).
        static string Sfx(FlareOrbPresetBase p) => p != null && !string.IsNullOrEmpty(p.variantSuffix) ? p.variantSuffix : "";
        static string MatLane(FlareOrbPresetBase p)     => DIR + "/FlareAuraLane" + Sfx(p) + ".mat";
        static string MatLaneBack(FlareOrbPresetBase p) => DIR + "/FlareAuraLaneBack" + Sfx(p) + ".mat";
        /// <summary>F1은 흑염 전용 오브 프리팹이 없다(셸 형태는 크림판과 공유) — 접미사를 붙이지 않는다.</summary>
        static string OrbPrefab   => DIR + "/FlareOrbAlt.prefab";
        const string PresetPath   = DIR + "/F1_FlareOrbAltPreset.asset";

        /// <summary>이 프리셋이 흑염 변종을 대상으로 하는가 — 라이브 프리뷰 스코프를 가른다.</summary>
        static bool WantsDark(FlareOrbPresetBase p) => Sfx(p).Contains("Dark");

        protected override string LiveKey => "JC.FlareOrbAltPreset.LivePreview";
        protected override string HelpText =>
            "라이브 프리뷰: 슬라이더를 움직이면 씬의 F1 셸(FlareOrbAlt)에 즉시 반영(MPB, 에셋 무변경). 플레이 중에도 동작.\n" +
            "적용: 이 값을 레인 재질(전/후면) + FlareOrbAlt 프리팹(셸 형태)에 확정 기록하고 프리뷰 오버라이드를 해제.\n" +
            "캡처: 현재 재질/프리팹 값을 이 프리셋으로 역방향 읽기.";

        protected override void LivePush() => LivePushStatic((FlareOrbAltPreset)target);
        protected override void ApplyPreset() => Apply((FlareOrbAltPreset)target);
        protected override void CapturePreset() => Capture((FlareOrbAltPreset)target);

        // 플레이모드 진입 시 씬 리로드로 MPB(라이브 프리뷰)가 초기화되므로 자동 재푸시
        [InitializeOnLoadMethod]
        static void HookPlayModeRepush()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                if (!EditorPrefs.GetBool("JC.FlareOrbAltPreset.LivePreview", true)) return;
                var p = AssetDatabase.LoadAssetAtPath<FlareOrbAltPreset>(PresetPath);
                if (p != null) EditorApplication.delayCall += () => LivePushStatic(p);
            };
        }

        // ---------- 재질 값 채우기 (재질/MPB 공용) ----------

        static void FillLaneCommon(System.Action<string, float> setF, System.Action<string, Color> setC,
                                   FlareOrbAltPreset p, bool back)
        {
            setC("_ColorTongue", p.tongueColor);
            setC("_ColorHighlight", p.tongueHighlightColor);
            setF("_Emission", back ? p.tongueEmission * p.backEmissionMul : p.tongueEmission);
            setF("_Opacity", back ? p.backOpacity : 1f);
            setF("_LaneCount", p.laneCount);
            setF("_LaneWidth", p.laneWidth);
            setF("_LaneWidthJitter", p.laneWidthJitter);
            setF("_LanePosJitter", p.lanePosJitter);
            setF("_LaneTiltJitter", p.laneTiltJitter);
            setF("_WidthNoiseScale", p.widthNoiseScale);
            setF("_WidthNoiseAmount", p.widthNoiseAmount);
            setF("_VStart", p.laneVStart);
            setF("_VEnd", p.laneVEnd);
            setF("_VJitter", p.laneVJitter);
            setF("_BirthBias", p.birthBottomBias);
            setF("_VLengthJitter", p.laneLengthJitter);
            setF("_MaxLen", p.laneMaxLength);
            setF("_UpperShrink", p.laneUpperShrink);
            setF("_TravelDist", p.laneTravelDist);
            setF("_TravelSpeed", p.laneTravelSpeed);
            setF("_LifeFadePeak", p.laneLifeFadePeak);
            setF("_TaperSharp", p.taperSharp);
            setF("_HighlightRatio", p.highlightRatio);
            setF("_EdgeSoftRatio", p.edgeSoftRatio);
            setF("_FlowSpeed", p.flowSpeed);
            setF("_Shear", p.spiralShear);
            setF("_SCurveAmount", p.sCurveAmount);
            setF("_SCurveFreq", p.sCurveFreq);
            setF("_SCurveUpperRatio", p.sCurveUpperRatio);
            setF("_TipErodeStart", p.tipErodeStart);
            setF("_TipErodeStrength", p.tipErodeStrength);
            setF("_DebugOutline", back ? 0f : p.debugOutline);   // 아웃라인은 전면 셸만
        }

        // ---------- 라이브 프리뷰 ----------

        static void LivePushStatic(FlareOrbAltPreset p)
        {
            foreach (var shell in Object.FindObjectsByType<FlareOrbShell>(FindObjectsSortMode.None))
            {
                if (!IsAltShell(shell)) continue;
                // ★변종 스코프(260730): 크림판은 흑염을 건너뛰고, 흑염 프리셋은 흑염만 만진다.
                if (InDarkVariant(shell) != WantsDark(p)) continue;
                bool isBack = shell.gameObject.name.Contains("Back");
                ApplyShellToInstance(shell, p, isBack);

                var mr = shell.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(mpb);
                    FillLaneCommon((n, f) => mpb.SetFloat(n, f), (n, c) => mpb.SetColor(n, c), p, isBack);
                    mr.SetPropertyBlock(mpb);
                }
            }
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        // ---------- 확정 적용 / 캡처 ----------

        public static void Apply(FlareOrbAltPreset p)
        {
            var lane = Load<Material>(MatLane(p));
            FillLaneCommon((n, f) => lane.SetFloat(n, f), (n, c) => lane.SetColor(n, c), p, false);
            lane.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            lane.renderQueue = 3002;   // 최상단(레인 전면)
            EditorUtility.SetDirty(lane);

            var laneBack = Load<Material>(MatLaneBack(p));
            if (laneBack != null)
            {
                FillLaneCommon((n, f) => laneBack.SetFloat(n, f), (n, c) => laneBack.SetColor(n, c), p, true);
                laneBack.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
                laneBack.renderQueue = 2995;   // 최심(레인 후면 = 제일 먼저)
                EditorUtility.SetDirty(laneBack);
            }
            AssetDatabase.SaveAssets();

            // ★흑염은 전용 오브 프리팹이 없다 — 기록하면 크림판 프리팹을 덮어쓰므로 건너뛴다.
            if (WantsDark(p))
            {
                Debug.Log("[FlareOrbAltPreset] " + p.name + " → 흑염 레인 재질에 적용 완료 (프리팹은 크림판 공유이므로 미기록)");
                return;
            }

            var orb = PrefabUtility.LoadPrefabContents(OrbPrefab);
            try
            {
                ApplyShellsToPrefab(orb, p);
                PrefabUtility.SaveAsPrefabAsset(orb, OrbPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(orb); }

            ClearShellOverrides(IsAltShell, false);
            Debug.Log("[FlareOrbAltPreset] 프리팹에 적용 완료 (라이브 오버라이드 해제)");
        }

        public static void Capture(FlareOrbAltPreset p)
        {
            Undo.RecordObject(p, "Capture Flare Orb F1");

            var lane = Load<Material>(MatLane(p));
            p.tongueColor = lane.GetColor("_ColorTongue");
            p.tongueHighlightColor = lane.GetColor("_ColorHighlight");
            p.tongueEmission = lane.GetFloat("_Emission");
            p.laneCount = Mathf.RoundToInt(lane.GetFloat("_LaneCount"));
            p.laneWidth = lane.GetFloat("_LaneWidth");
            p.laneWidthJitter = lane.GetFloat("_LaneWidthJitter");
            p.lanePosJitter = lane.GetFloat("_LanePosJitter");
            p.laneTiltJitter = lane.GetFloat("_LaneTiltJitter");
            p.widthNoiseScale = lane.GetFloat("_WidthNoiseScale");
            p.widthNoiseAmount = lane.GetFloat("_WidthNoiseAmount");
            p.laneVStart = lane.GetFloat("_VStart");
            p.laneVEnd = lane.GetFloat("_VEnd");
            p.laneVJitter = lane.GetFloat("_VJitter");
            p.birthBottomBias = lane.GetFloat("_BirthBias");
            p.laneLengthJitter = lane.GetFloat("_VLengthJitter");
            p.laneMaxLength = lane.GetFloat("_MaxLen");
            p.laneUpperShrink = lane.GetFloat("_UpperShrink");
            p.laneTravelDist = lane.GetFloat("_TravelDist");
            p.laneTravelSpeed = lane.GetFloat("_TravelSpeed");
            p.laneLifeFadePeak = lane.GetFloat("_LifeFadePeak");
            p.taperSharp = lane.GetFloat("_TaperSharp");
            p.highlightRatio = lane.GetFloat("_HighlightRatio");
            p.edgeSoftRatio = lane.GetFloat("_EdgeSoftRatio");
            p.flowSpeed = lane.GetFloat("_FlowSpeed");
            p.spiralShear = lane.GetFloat("_Shear");
            p.sCurveAmount = lane.GetFloat("_SCurveAmount");
            p.sCurveFreq = lane.GetFloat("_SCurveFreq");
            p.sCurveUpperRatio = lane.GetFloat("_SCurveUpperRatio");
            p.tipErodeStart = lane.GetFloat("_TipErodeStart");
            p.tipErodeStrength = lane.GetFloat("_TipErodeStrength");
            p.debugOutline = lane.GetFloat("_DebugOutline");

            var laneBack = Load<Material>(MatLaneBack(p));
            if (laneBack != null)
            {
                p.backOpacity = laneBack.GetFloat("_Opacity");
                float frontEm = Mathf.Max(lane.GetFloat("_Emission"), 0.001f);
                p.backEmissionMul = laneBack.GetFloat("_Emission") / frontEm;
            }

            var orb = Load<GameObject>(OrbPrefab);
            CaptureShellsFromPrefab(orb, p);

            EditorUtility.SetDirty(p);
            Debug.Log("[FlareOrbAltPreset] 현재값 캡처 완료");
        }
    }
}
