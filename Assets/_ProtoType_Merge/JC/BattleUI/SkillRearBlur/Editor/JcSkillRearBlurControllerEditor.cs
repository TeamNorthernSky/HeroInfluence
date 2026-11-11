using UnityEditor;
using UnityEngine;

namespace JC.BattleUI.Editor
{
    // JC | 2026-11-11 | 기존 전투 조절기와 같은 초안/명시 저장 규칙입니다. 미리보기 상태와 월드 후방 기준는 저장하지 않습니다.
    [CustomEditor(typeof(JcSkillRearBlurController))]
    public sealed class JcSkillRearBlurControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject,"m_Script");
            serializedObject.ApplyModifiedProperties();
            var control=(JcSkillRearBlurController)target;
            if (!control.Draft) { EditorGUI.EndChangeCheck();EditorGUILayout.HelpBox("공통 저장 프로필을 연결해 주세요.",MessageType.Warning);return; }
            var values=new SerializedObject(control.Draft); values.Update();
            DrawPropertiesExcluding(values,"m_Script");
            values.ApplyModifiedProperties();control.RefreshNow();
            // JC | 2026-11-11 | 일시정지 중에도 바꾼 값으로 게임뷰를 다시 그립니다. 시간 진행·Play 상태는 변경하지 않습니다.
            if (EditorGUI.EndChangeCheck())
            {
                EditorApplication.QueuePlayerLoopUpdate();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
            EditorGUILayout.HelpBox("첫 튜토리얼·일반 야외 전투가 같은 프로필을 사용합니다. 수정은 즉시 반영되지만 저장 버튼을 누르기 전에는 원본을 변경하지 않습니다. 시작 거리는 적 진형 중심에서 후방으로 잽니다. 숫자·투명 이펙트는 블러 뒤에 그립니다. 개체별 보호 마스크는 없으므로 전투 영역보다 뒤에서 시작하도록 조절해 주세요.",MessageType.Info);
            EditorGUILayout.LabelField("현재 상태",control.Status);
            // JC | 2026-11-11 | 월드 평면과 구간을 표시하며 카메라 기준 깊이는 더 이상 노출하지 않습니다.
            EditorGUILayout.LabelField("월드 후방 평면",control.RearPlane.ToString("F2"));
            var d=control.CurrentDistances;
            EditorGUILayout.LabelField("약한 구간 / 강한 구간",$"{d.x:F2}~{d.y:F2} / {d.z:F2}~{d.w:F2}");
            if (GUILayout.Button("현재 값을 프로필에 저장"))
            {
                Undo.RecordObject(control.profile,"JC 후방 블러 저장");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(control.Draft),control.profile);
                EditorUtility.SetDirty(control.profile);AssetDatabase.SaveAssetIfDirty(control.profile);
            }
            if (GUILayout.Button("저장값 다시 불러오기")) control.ReloadProfile();
        }
        public override bool RequiresConstantRepaint()=>true;
    }
}
