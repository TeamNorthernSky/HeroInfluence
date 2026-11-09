using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    [CustomEditor(typeof(JcBankEnvironment))]
    public sealed class JcBankEnvironmentEditor : UnityEditor.Editor
    {
        void OnEnable() => Undo.undoRedoPerformed += Reapply;
        void OnDisable() => Undo.undoRedoPerformed -= Reapply;
        void Reapply() { if (target) { ((JcBankEnvironment)target).ApplyDraft(); Repaint(); } }
        public override void OnInspectorGUI()
        {
            var controls = (JcBankEnvironment)target;
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(new GUIContent("공유 저장 프로필", "본게임 은행전과 BankTestScene이 함께 사용하는 조명 저장값입니다."), controls.Source, typeof(JcBankEnvironmentProfile), false);
            if (!controls.Draft) { EditorGUILayout.HelpBox("은행 배경 연결 후 조절할 수 있습니다.", MessageType.Info); return; }
            EditorGUILayout.HelpBox("실외광·하늘·낮밤 보정을 차단한 고정 실내 조명입니다. 조절값은 즉시 반영되며 플레이 종료 전에 저장해야 유지됩니다.", MessageType.Info);
            if (!controls.isActiveAndEnabled) EditorGUILayout.HelpBox("실내 조명 미리보기가 중지되었습니다.", MessageType.Warning);
            // JC | 2026-11-06 | 기존 은행 환경 오브젝트에서 SSAO도 조절합니다. 누락된 연결은 임의 대체하지 않고 표시합니다.
            if (!controls.SsaoAvailable) EditorGUILayout.HelpBox("은행 SSAO 연결이 없습니다. 공유 환경 프로필의 연결을 확인해 주세요.", MessageType.Warning);
            // JC | 2026-11-05 | 저장 전 초안만 조절하여 플레이 검수 중 공유 원본을 자동 변경하지 않습니다.
            var serialized = new SerializedObject(controls.Draft); serialized.Update();
            foreach (var name in new[] { "ssaoEnabled", "ssaoIntensity", "ssaoRadius", "ssaoDirectLightingStrength", "ssaoDownsample", "ambientColor", "ambientIntensity", "reflectionIntensity", "reflectionPosition", "reflectionSize", "glyphHaloColor", "glyphHaloIntensity", "glyphHaloWidth", "lights" })
                EditorGUILayout.PropertyField(serialized.FindProperty(name), true);
            if (serialized.ApplyModifiedProperties()) controls.ApplyDraft();
            // JC | 2026-11-05 | 은행 전용 Forward+ 적용으로 이전 8개 제한 경고를 제거합니다.
            EditorGUILayout.HelpBox("각 조명 오브젝트의 은행 조명 조절기에서도 동일한 초안을 편집할 수 있습니다.", MessageType.Info);
            // JC | 2026-11-06 | SSAO와 조명 초안을 같은 프로필에 보관하여 다음 은행전에 함께 재적용합니다.
            if (GUILayout.Button(new GUIContent("현재 실내 조명·SSAO 값을 공유 프로필에 저장", "현재 초안을 저장합니다. 다음 본게임 은행전과 BankTestScene에 함께 적용됩니다. 렌더러 원본에는 저장하지 않습니다.")))
            {
                Undo.RecordObject(controls.Source, "은행 실내 조명 저장");
                var name = controls.Source.name; EditorUtility.CopySerialized(controls.Draft, controls.Source);
                controls.Source.name = name; controls.Source.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(controls.Source); AssetDatabase.SaveAssetIfDirty(controls.Source);
            }
            if (GUILayout.Button(new GUIContent("저장값 다시 적용", "저장하지 않은 변경을 버리고 공유 프로필의 값으로 되돌립니다."))) controls.ReloadProfile();
            if (GUILayout.Button(new GUIContent("실내 반사 다시 캡처", "재질·배치 변경 후 실내 반사를 갱신합니다. 실외 하늘은 포함하지 않습니다."))) controls.RequestReflection();
        }
    }
}
