using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    [CustomEditor(typeof(JcBankBackgroundMaterials))]
    public sealed class JcBankBackgroundMaterialsEditor : UnityEditor.Editor
    {
        void OnEnable() => Undo.undoRedoPerformed += Reapply;
        void OnDisable() => Undo.undoRedoPerformed -= Reapply;
        void Reapply() { if (target) { ((JcBankBackgroundMaterials)target).ApplyDraft(); Repaint(); } }

        public override void OnInspectorGUI()
        {
            var controls = (JcBankBackgroundMaterials)target;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("공유 저장 프로필", controls.Source, typeof(JcBankMaterialProfile), false);
            if (!controls.Draft) { EditorGUILayout.HelpBox("은행 배경 연결 후 조절할 수 있습니다.", MessageType.Info); return; }
            EditorGUILayout.HelpBox("현재 전투에 즉시 미리보기됩니다. 플레이 종료 전에 저장 버튼을 눌러야 유지됩니다. 저장값은 본게임 은행전과 BankTestScene에서 함께 사용합니다.", MessageType.Info);
            if (!controls.isActiveAndEnabled) EditorGUILayout.HelpBox("컴포넌트가 꺼져 있어 미리보기를 중지했습니다.", MessageType.Warning);
            var serialized = new SerializedObject(controls.Draft);
            serialized.Update();
            var surfaces = serialized.FindProperty("surfaces");
            // JC | 2026-11-05 | 표면 연결은 고정하고 조절값만 노출하여 슬롯 변경 실수를 방지합니다.
            for (int i = 0; i < surfaces.arraySize; i++)
            {
                var surface = surfaces.GetArrayElementAtIndex(i);
                surface.isExpanded = EditorGUILayout.Foldout(surface.isExpanded, surface.FindPropertyRelative("label").stringValue, true);
                if (!surface.isExpanded) continue;
                EditorGUI.indentLevel++;
                foreach (var field in new[] { "tint", "metallic", "smoothness", "specularHighlights", "environmentReflections", "emissionColor", "emissionIntensity" })
                    EditorGUILayout.PropertyField(surface.FindPropertyRelative(field));
                var material = surface.FindPropertyRelative("source").objectReferenceValue as Material;
                if (material && material.GetTexture("_BumpMap"))
                {
                    EditorGUILayout.PropertyField(surface.FindPropertyRelative("normalStrength"));
                    EditorGUILayout.PropertyField(surface.FindPropertyRelative("groutContrast"));
                    EditorGUILayout.PropertyField(surface.FindPropertyRelative("tileSize"));
                }
                EditorGUI.indentLevel--;
            }
            if (serialized.ApplyModifiedProperties()) controls.ApplyDraft();
            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("현재 은행 재질 값을 공유 프로필에 저장", "현재 재질 초안을 본게임과 BankTestScene 공유 프로필에 저장합니다. 플레이 종료 전에 누르세요.")))
            {
                Undo.RecordObject(controls.Source, "은행 재질 프로필 저장");
                var name = controls.Source.name;
                EditorUtility.CopySerialized(controls.Draft, controls.Source);
                controls.Source.name = name; controls.Source.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(controls.Source);
                AssetDatabase.SaveAssetIfDirty(controls.Source);
            }
            if (GUILayout.Button(new GUIContent("저장값 다시 적용", "미저장 조절값을 버리고 공유 프로필의 저장값으로 되돌립니다."))) controls.ReloadProfile();
        }
    }
}
