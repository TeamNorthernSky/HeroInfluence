using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// T9(배리지) 에디터 — 따름 잠금 UI + 디스크 저장.
    /// ★배리지 시각물은 TaoBarrageVfx 가 절차 생성(코어·시트·낙하 스타·기둥) — 프리팹에 베이크할
    ///   컴포넌트가 없어서 「프리팹에 적용/캡처」 개념 자체가 없다. SO 프리셋 값이 곧 라이브 정본이며
    ///   플레이모드에서 고친 값도 SO 에 남는다. 「디스크 저장」은 그 값을 파일로 확정하는 버튼(260807).
    /// 색·룩은 변종별 재질(TaoBarrageSheet/Veil/Core ±)이 정본 — 재질 인스펙터에서 직접 조절.
    /// </summary>
    [CustomEditor(typeof(TaoBarragePreset))]
    public class TaoBarragePresetEditor : Editor
    {
        // 따름 잠금 — 색(기둥) 외 전부(원점·시트·스타·타이밍·피격)는 Basic 이 정본.
        static readonly string[] TransformProps =
        {
            "originHeight", "originBack", "handSocketName", "handOffset",
            "sheetCount", "sheetSpreadDeg", "sheetWidth", "sheetLengthMul", "veilWidthMul",
            "coreSize", "coreStarSize",
            "starRate", "starSpeedMin", "starSpeedMax", "starSizeMin", "starSizeMax", "starConeDeg", "starTrailTime",
            "buildTime", "sustainTime", "fadeOutTime", "hitDelay",
            "hitOrder", "stagger", "pillarWidth", "pillarHeight", "pillarLife",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.TaoBarrage.Fold", TransformProps);
            EditorGUILayout.Space();
            JcPresetEditorUtil.DrawSaveButton(target, wide: true);
            EditorGUILayout.HelpBox("배리지 시각물은 절차 생성 — 프리팹 베이크(적용/캡처)가 없습니다.\n" +
                "조절값은 메모리에 유지(플레이 출입·리컴파일 생존)되고, 💾 클릭 시에만 파일로 확정됩니다.\n" +
                "색·룩은 변종별 재질(TaoBarrageSheet/Veil/Core ±)에서 직접 조절합니다.", MessageType.Info);
        }
    }
}
