using UnityEditor;
using UnityEngine;

// JC | 2026-11-04 | 굉음 대화의 진동과 개별 발걸음 반동을 별도로 조절해 반복 이동 중에도 타이밍을 유지합니다.
[CustomEditor(typeof(JcArnoldShake))]
public sealed class JcArnoldShakeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var control = (JcArnoldShake)target;
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("draft"), true);
        serializedObject.ApplyModifiedProperties();
        using (new EditorGUI.DisabledScope(control.profile == null))
        {
            if (GUILayout.Button(new GUIContent("프로필 불러와 적용", "현재 조절값을 선택한 프로필의 저장값으로 교체합니다. 아직 저장하지 않은 조절값은 사라집니다.")))
            {
                Undo.RecordObject(control, "아놀드 셰이크 프로필 적용");
                control.LoadProfile();
                EditorUtility.SetDirty(control);
            }
            if (GUILayout.Button(new GUIContent("현재 값을 프로필에 저장", "현재 조절값으로 선택한 프로필을 덮어씁니다. 플레이를 종료한 뒤에도 이 값이 유지됩니다.")))
            {
                Undo.RecordObject(control.profile, "아놀드 셰이크 저장");
                control.profile.values = control.draft;
                EditorUtility.SetDirty(control.profile);
                AssetDatabase.SaveAssetIfDirty(control.profile);
            }
        }
        EditorGUILayout.HelpBox("플레이 중 조절값은 다음 박자부터 반영됩니다. HEROTALK 창이 열려 있으면 채팅 미리보기에서 창도 함께 흔들립니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !control.isActiveAndEnabled))
        {
            if (GUILayout.Button(new GUIContent("발걸음 한 번 미리보기", "플레이 중 현재 강도와 지속 시간으로 카메라를 한 번 흔듭니다. 발소리는 재생하지 않습니다."))) control.Preview(false);
            if (GUILayout.Button(new GUIContent("등장 채팅 네 박자 미리보기", "플레이 중 굉음 네 박자를 재생합니다. HEROTALK가 열려 있으면 창과 카메라가 함께, 닫혀 있으면 카메라만 흔들립니다."))) control.Preview(true);
            if (GUILayout.Button(new GUIContent("임시 채팅창으로 셰이크 테스트", "굉음 안내가 담긴 HEROTALK 테스트 창을 띄워 현재 설정으로 네 번 흔든 뒤 자동으로 닫습니다."))) control.TestChatWindow();
        }
    }
}
