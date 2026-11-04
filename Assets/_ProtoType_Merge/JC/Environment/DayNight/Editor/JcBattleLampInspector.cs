using UnityEditor;
using UnityEngine;

namespace JC.Environment.Editor
{
    [CustomEditor(typeof(JcBattleLampSettings))]
    internal sealed class JcBattleLampInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (JcBattleLampSettings)target;
            if (!settings.Draft) return;
            int index = settings.Segment;
            EditorGUILayout.LabelField($"현재 전투 · 구간 {index + 1}", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("저장 대상", settings.Source, typeof(JcBattleLampProfile), false);
            var draft = new SerializedObject(settings.Draft);
            var slot = draft.FindProperty("segments").GetArrayElementAtIndex(index);
            foreach (string field in new[] { "lit", "color", "range", "core", "spill", "fill", "shadowStrength", "glow" })
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(field), true);
            if (draft.ApplyModifiedProperties()) settings.NotifyChanged();
            if (GUILayout.Button("현재 구간을 프로필에 저장"))
            {
                Undo.RecordObject(settings.Source, "전투 가로등 구간 저장");
                settings.Source.segments[index] = JsonUtility.FromJson<JcBattleLampProfile.Segment>(
                    JsonUtility.ToJson(settings.Draft.segments[index]));
                EditorUtility.SetDirty(settings.Source);
                AssetDatabase.SaveAssetIfDirty(settings.Source);
            }
        }
    }
}
