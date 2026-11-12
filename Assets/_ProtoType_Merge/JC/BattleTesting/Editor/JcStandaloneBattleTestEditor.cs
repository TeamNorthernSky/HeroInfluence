using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-12 | 기존 SimulationBattleConfig를 초안으로 편집합니다. 실제 게임 프로필 저장과 시작 설정 저장을 구분합니다.
    [CustomEditor(typeof(JcStandaloneBattleTest))]
    public sealed class JcStandaloneBattleTestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var test = (JcStandaloneBattleTest)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            if (!test.Draft) { EditorGUILayout.HelpBox("시작 프로필이 필요합니다.", MessageType.Error); return; }
            var values = new SerializedObject(test.Draft);
            values.Update(); DrawPropertiesExcluding(values, "m_Script"); values.ApplyModifiedProperties();
            if (!test.ValidateEncounter(test.Draft, out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("파티·적 레벨·은행 전투 종류·안내 선택은 다음 시작/재시작에 적용됩니다. 전투 중 진형/적 추가/반복은 우상단 버튼을 사용하세요. 시작 설정 저장은 아래 버튼이며, 카메라·DOF·환경은 각 조절기의 저장 버튼을 사용합니다.", MessageType.Info);
            if (GUILayout.Button("시작 설정을 프로필에 저장") && test.ValidateEncounter(test.Draft, out _))
            {
                Undo.RecordObject(test.profile, "JC 테스트 시작 설정");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(test.Draft), test.profile);
                EditorUtility.SetDirty(test.profile); AssetDatabase.SaveAssetIfDirty(test.profile);
            }
            if (GUILayout.Button("저장한 시작 설정 다시 불러오기")) test.ReloadProfile();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("현재 설정으로 전투 재시작")) test.Restart();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("정본 1 카메라 촬영 도구 열기")) JcBankSkillCameraWindow.Open();
            test.RefreshLighting();
            EditorGUILayout.LabelField("상태", test.Status);
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
