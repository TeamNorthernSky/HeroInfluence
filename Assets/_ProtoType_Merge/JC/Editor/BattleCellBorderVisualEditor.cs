using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleCellBorderVisual))]
public sealed class BattleCellBorderVisualEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var visual=(BattleCellBorderVisual)target;
        if(visual.profile==null){DrawDefaultInspector();return;}
        using(new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("외형 프로필",visual.profile,typeof(BattleCellBorderProfile),false);
            EditorGUILayout.EnumPopup("표시 상태",visual.presentationState);
        }
        EditorGUILayout.HelpBox("외형은 JC_TestScenes/z_JC_BattleCellBorderSettings의 '테두리 설정 · 프로필 저장' 오브젝트에서 조절하세요. 이 프리팹은 표시 구조만 담당합니다.",MessageType.Info);
    }
}
