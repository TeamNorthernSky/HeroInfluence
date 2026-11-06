using UnityEditor;
using UnityEngine;

namespace JC.Environment.Editor
{
    // JC | 2026-11-06 | 플레이 중 초안을 조절하고 명시적 저장 버튼으로만 공용 프로필에 기록합니다.
    [CustomEditor(typeof(JcContactShadingController))]
    public sealed class JcContactShadingControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (JcContactShadingController)target;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(new GUIContent("공유 저장 프로필", "본게임과 테스트 씬에서 함께 읽는 프로필입니다."), controller.Source, typeof(JcContactShadingProfile), false);
            if (!controller.Draft) { EditorGUILayout.HelpBox("배경과 접점 프로필을 연결한 뒤 사용할 수 있습니다.", MessageType.Info); return; }
            EditorGUILayout.HelpBox("현재 배경에 즉시 미리보기합니다. 유지하려면 플레이 종료 전에 프로필 저장 버튼을 누르세요. 원본 재질·메시는 수정하지 않습니다.", MessageType.Info);
            using (var draft = new SerializedObject(controller.Draft))
            {
                draft.Update();
                EditorGUI.BeginChangeCheck();
                foreach (var name in new[] { "shadingEnabled", "tint", "strength", "width", "softness", "targetMaterials", "contactShader", "segments" })
                    EditorGUILayout.PropertyField(draft.FindProperty(name), true);
                if (EditorGUI.EndChangeCheck()) { draft.ApplyModifiedProperties(); controller.ApplyDraft(); }
            }
            EditorGUILayout.LabelField(new GUIContent("적용 재질 수", "등록된 복제 재질 중 접점 음영이 활성화된 수입니다."), controller.AppliedMaterialCount.ToString());
            if (GUILayout.Button(new GUIContent("현재 접점 음영 값을 공유 프로필에 저장", "현재 초안을 저장합니다. 본게임과 BankTestScene에서 다음 실행부터 동일하게 적용합니다.")))
            {
                var source = controller.Source;
                if (source)
                {
                    Undo.RecordObject(source, "JC 접점 음영 프로필 저장");
                    string sourceName = source.name;
                    var sourceFlags = source.hideFlags;
                    EditorUtility.CopySerialized(controller.Draft, source);
                    source.name = sourceName;
                    source.hideFlags = sourceFlags;
                    EditorUtility.SetDirty(source);
                    AssetDatabase.SaveAssetIfDirty(source);
                }
            }
            if (GUILayout.Button(new GUIContent("저장값 다시 적용", "저장하지 않은 초안을 버리고 공유 프로필 값을 다시 읽습니다."))) controller.ReloadProfile();
        }
    }
}
