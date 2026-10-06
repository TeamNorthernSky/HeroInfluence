using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // 원본 대응: SimulationBattleConfigEditor + JC DOF 프로필 Inspector의 명시적 저장 버튼.
    [CustomEditor(typeof(JcBattleTestSession))]
    public sealed class JcBattleTestSessionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            var session = (JcBattleTestSession)target;
            EditorGUILayout.HelpBox(session.Status, MessageType.Info);
            if (GUILayout.Button(new GUIContent("설정 검증", "초기조건·프리팹·스킬을 검사합니다. 진행 중인 전투는 변경하지 않습니다.")))
            {
                bool valid = session.Validate(out var error);
                EditorUtility.DisplayDialog("JC 전투 테스트", valid ? "설정 검증을 통과했습니다." : error, "확인");
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button(new GUIContent("설정 적용 및 전투 재시작", "행동 중에는 적용하지 않습니다. 검증 성공 후 전투 상태만 초기화하며 카메라·환경 설정은 유지합니다."))) session.ApplyAndRestart();
            if (GUILayout.Button(new GUIContent("프로필 저장", "인스펙터 초기 설정만 .asset으로 명시적으로 저장합니다. 현재 HP·턴·예약·소환·카메라·환경 진행 값은 저장하지 않습니다.")))
            {
                if (session.profile == null)
                {
                    string path = EditorUtility.SaveFilePanelInProject("테스트 프로필 저장", "JC_BattleTestProfile", "asset", "초기 설정을 저장할 프로필 경로를 선택하세요.");
                    if (string.IsNullOrEmpty(path)) return;
                    session.profile = CreateInstance<JcBattleTestProfile>();
                    AssetDatabase.CreateAsset(session.profile, path);
                }
                Undo.RecordObject(session.profile, "JC 테스트 초기 설정 저장");
                session.profile.settings = JsonUtility.FromJson<JcBattleTestSettings>(JsonUtility.ToJson(session.settings));
                EditorUtility.SetDirty(session.profile); AssetDatabase.SaveAssetIfDirty(session.profile);
                EditorUtility.SetDirty(session);
            }
            using (new EditorGUI.DisabledScope(session.profile == null))
                if (GUILayout.Button(new GUIContent("프로필 불러오기", "선택한 프로필을 인스펙터 초안으로 불러옵니다. 실제 전투는 설정 적용 및 재시작 시에만 바뀝니다.")))
                {
                    Undo.RecordObject(session, "JC 테스트 프로필 불러오기");
                    session.settings = JsonUtility.FromJson<JcBattleTestSettings>(JsonUtility.ToJson(session.profile.settings));
                    EditorUtility.SetDirty(session);
                }
        }
    }
}
