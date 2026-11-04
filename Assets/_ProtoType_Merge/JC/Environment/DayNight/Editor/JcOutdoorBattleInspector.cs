using UnityEditor;
using UnityEngine;

namespace JC.Environment.Editor
{
    internal static class JcOutdoorBattleInspector
    {
        static string TimeLabel(double hour) =>
            System.TimeSpan.FromMinutes(Mathf.RoundToInt(Mathf.Repeat((float)hour, 24) * 60)).ToString(@"hh\:mm");

        internal static void Draw(JcDayNightController controller, int selected = -1)
        {
            var source = controller.BattleSource;
            var draft = controller.BattleDraft;
            if (!source || !draft)
            {
                EditorGUILayout.HelpBox("야외 전투 보정 프로필을 기다리는 중입니다.", MessageType.Info);
                return;
            }
            var basis = controller.Settings;
            double hour = JcDayNightController.CurrentHours;
            basis.SegmentPair(hour, out int active, out int next, out float blend);
            EditorGUILayout.LabelField("현재 전투", TimeLabel(hour) + " · 구간 " + (active + 1)
                + (blend > 0 ? " → " + (next + 1) : ""));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("저장 대상", source, typeof(JcOutdoorBattleLightingProfile), false);
            int index = selected < 0 ? active : selected;
            float anchor = basis.segments[index].hour;
            EditorGUILayout.LabelField($"구간 {index + 1} · {TimeLabel(anchor)}", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("루트에서는 현재 구간을 조절합니다. 다른 시간대는 하위 구간을 선택하세요. 저장은 선택한 구간에만 적용됩니다.", MessageType.Info);
            var so = new SerializedObject(draft);
            var slot = so.FindProperty("segments").GetArrayElementAtIndex(index);
            string[] fields = { "sunlight", "sunlightTint", "sunShadow", "secondarySun", "ambient", "characterBrightness", "exposure", "screenTint" };
            string[] labels = { "방향광 밝기 배율", "방향광 색조", "주 방향광 그림자 배율", "보조 방향광 밝기 배율", "환경광 배율", "전투 캐릭터 밝기 배율", "화면 노출 보정", "화면 색조" };
            for (int i = 0; i < fields.Length; ++i)
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(fields[i]), new GUIContent(labels[i]));
            if (so.ApplyModifiedProperties()) controller.ApplyHour(hour);
            var inherited = basis.SampleSegment(anchor);
            var final = basis.SampleSegment(anchor);
            float actors = draft.Apply(basis, anchor, final);
            EditorGUILayout.LabelField("구간 기준값 → 전투 보정 후");
            EditorGUILayout.LabelField("방향광 설정", $"{inherited.lightIntensity:F3} → {final.lightIntensity:F3}");
            EditorGUILayout.LabelField("환경광 배율", $"{inherited.ambientMultiplier:F2} → {final.ambientMultiplier:F2}");
            EditorGUILayout.LabelField("캐릭터 밝기 배율", $"1 → {actors:F2}");
            EditorGUILayout.LabelField("화면 노출", $"{inherited.screenExposure * inherited.screenColorStrength:F2} → {final.screenExposure * final.screenColorStrength:F2}");
            if (GUILayout.Button("이 구간 전투 보정값 저장")) SaveSegment(source, draft, index);
            if (GUILayout.Button("이 구간 보정 초기화"))
            {
                Undo.RecordObject(draft, "전투 보정 초기화");
                draft.segments[index] = new JcOutdoorBattleLightingProfile.Adjustment();
                controller.ApplyHour(hour);
            }
        }

        internal static void SaveSegment(JcOutdoorBattleLightingProfile source, JcOutdoorBattleLightingProfile draft, int index)
        {
            Undo.RecordObject(source, "야외 전투 구간 저장");
            source.segments[index] = JsonUtility.FromJson<JcOutdoorBattleLightingProfile.Adjustment>(
                JsonUtility.ToJson(draft.segments[index]));
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);
        }
    }
}
