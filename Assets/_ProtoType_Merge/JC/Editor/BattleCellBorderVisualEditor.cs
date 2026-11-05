using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleCellBorderVisual))]
public sealed class BattleCellBorderVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var visual=(BattleCellBorderVisual)target;
        // JC | 2026-11-04 | 탐사 영역의 채움과 테두리를 부모 조절기에서 함께 관리해 하위 표시 설정이 저장값을 덮지 않게 합니다.
        var trigger=visual.GetComponentInParent<JC.Indicators.JcTriggerAreaIndicator>();
        if(trigger!=null)
        {
            using(new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("조절 오브젝트",trigger,typeof(JC.Indicators.JcTriggerAreaIndicator),true);
            EditorGUILayout.HelpBox("부모 TriggerAreaIndicator가 구동하는 표시 전용 오브젝트입니다. 셀 테두리 메시와 렌더러를 재사용하며 자체 설정은 적용하지 않습니다. 테두리·움직임·채움은 부모에서 조절하고 저장하세요.",MessageType.Info);
            return;
        }
        if(visual.profile==null){DrawDefaultInspector();return;}
        using(new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("외형 프로필",visual.profile,typeof(BattleCellBorderProfile),false);
            EditorGUILayout.EnumPopup("표시 상태",visual.presentationState);
        }
        EditorGUILayout.HelpBox("외형은 JC_TestScenes/z_JC_BattleCellBorderSettings의 '테두리 설정 · 프로필 저장' 오브젝트에서 조절하세요. 이 프리팹은 표시 구조만 담당합니다.",MessageType.Info);
    }
}
