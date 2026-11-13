using UnityEditor;
using UnityEngine;

// JC | 2026-11-13 | 화살표 편집을 VFX 하위로 이전하여 툴팁에 별도 임시값이 남지 않게 합니다.
[CustomEditor(typeof(BattleEnemyInfoTooltip))]
public sealed class BattleEnemyInfoTooltipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("배지·화살표: JC_BattleUI_VFX/TurnSlotHover. 대상 그리드·모델 윤곽선: CellBox에서 조절합니다.", MessageType.Info);
        DrawDefaultInspector();
    }
}
