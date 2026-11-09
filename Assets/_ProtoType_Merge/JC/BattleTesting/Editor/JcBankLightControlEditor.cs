using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-05 | 플레이에서도 편집 가능한 공유 초안의 해당 항목만 표시하고 명시 저장합니다.
    [CustomEditor(typeof(JcBankLightControl))]
    public sealed class JcBankLightControlEditor : UnityEditor.Editor
    {
        void OnEnable() => Undo.undoRedoPerformed += Reapply;
        void OnDisable() => Undo.undoRedoPerformed -= Reapply;
        void Reapply() { if (target && ((JcBankLightControl)target).IsBound) ((JcBankLightControl)target).Owner.ApplyDraft(); Repaint(); }
        public static void SaveLight(JcBankLightControl control)
        {
            if (!control.IsBound || !control.Owner.Source) return;
            var source = control.Owner.Source;
            if (control.Index >= source.lights.Count) { Debug.LogError("새 조명 목록은 실내 환경 인스펙터에서 전체 저장해 주세요.", control); return; }
            Undo.RecordObject(source, "은행 개별 조명 저장");
            source.lights[control.Index] = JsonUtility.FromJson<JcBankEnvironmentProfile.CeilingLight>(JsonUtility.ToJson(control.Owner.Draft.lights[control.Index]));
            EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
        }
        public override void OnInspectorGUI()
        {
            var control = (JcBankLightControl)target;
            if (!control.IsBound) { EditorGUILayout.HelpBox("은행 환경 연결 후 조절할 수 있습니다.", MessageType.Info); return; }
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(new GUIContent("공유 저장 프로필", "본게임 은행전과 BankTestScene에서 함께 사용하는 저장값입니다."), control.Owner.Source, typeof(JcBankEnvironmentProfile), false);
            EditorGUILayout.HelpBox("현재 조명만 즉시 미리보기합니다. 플레이 종료 전 저장해야 유지됩니다. 벽·금고 조명 위치는 광원만 이동하며 기존 벽 메시를 이동하지 않습니다.", MessageType.Info);
            var serialized = new SerializedObject(control.Owner.Draft); serialized.Update();
            var entry = serialized.FindProperty("lights").GetArrayElementAtIndex(control.Index);
            // JC | 2026-11-06 | 플레이어 전용 보조광의 대상·그림자는 고정하고 실제 조절 가능한 항목만 표시합니다.
            bool playerFill = entry.FindPropertyRelative("playerBackFill").boolValue;
            if (playerFill) EditorGUILayout.HelpBox("영향 대상: 플레이어 4종(저스티스·루미나·블랙불릿·네코밍). 배경·적군 제외, 그림자 없음. 툰과 독립적입니다.", MessageType.Info);
            var fields = playerFill ? new[] { "enabled", "color", "intensity", "rotation" } : new[] { "label", "enabled", "type", "color", "intensity", "position", "rotation", "range", "spotAngle", "innerSpotRatio", "shadows", "shadowStrength", "shadowBias", "shadowNormalBias" };
            foreach (var name in fields) {
                var property = entry.FindPropertyRelative(name);
                if (playerFill && name == "intensity") { EditorGUILayout.Slider(property, 0, 1, new GUIContent("보조광 밝기", "0은 꺼짐, 1은 최대 보조광입니다. 플레이어 4종에만 적용됩니다.")); continue; }
                EditorGUILayout.PropertyField(property, new GUIContent(property.displayName, property.tooltip), true);
            }
            if (serialized.ApplyModifiedProperties()) control.Owner.ApplyDraft();
            if (GUILayout.Button(new GUIContent("이 조명 값을 공유 프로필에 저장", "이 조명만 저장합니다. 다른 조명의 미저장 값은 저장하지 않습니다. 본게임과 테스트에 함께 적용됩니다."))) SaveLight(control);
            if (GUILayout.Button(new GUIContent("이 조명 저장값 다시 적용", "이 조명만 저장값으로 복원합니다. 다른 조명의 미저장 초안은 유지합니다."))) control.ReloadLight();
            if (GUILayout.Button(new GUIContent("실내 반사 다시 캡처", "현재 조명과 재질을 실내 반사에 갱신합니다."))) control.Owner.RequestReflection();
        }
    }
}
