using UnityEditor;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // 원본 대응: SimulationBattleConfigEditor + JC DOF 프로필 Inspector의 명시적 저장 버튼.
    [CustomEditor(typeof(JcBattleTestSession))]
    public sealed class JcBattleTestSessionEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            var session = (JcBattleTestSession)target;
            if (session.cameraProfile == null) session.LoadCameraProfile();
        }

        private void DrawCameraTools(JcBattleTestSession session)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("카메라 프로필 편집", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("A/B 초안의 '원근감 강도': 1 = 기존, 0.5 = 완화, 0.2 = 강하게 완화. 플레이 중 현재 구도의 위치·회전·FOV·원근감 강도는 수정 즉시 선형 전환으로 반영됩니다. 카메라 프로필 저장을 눌러야 종료 후에도 유지됩니다. F8 기본 뷰는 바뀌지 않습니다.", MessageType.Info);
            EditorGUILayout.HelpBox("우클릭 홀드 + WASD 이동 / Q·E 상하 / Shift 가속 / Ctrl 감속. 현재 자세 가져오기는 F6·F7에서만 가능합니다. 프리카메라 이동 후에는 현재 자세를 가져온 다음 초안을 조절하세요. 초안 변경 시 프리카메라를 종료하고 해당 초안으로 이동합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button(session.FreeCameraActive ? "프리카메라 종료 (현재 위치 유지)" : "프리카메라 시작"))
                    session.SetFreeCamera(!session.FreeCameraActive);
                using (new EditorGUI.DisabledScope(!session.CanCaptureCamera))
                    if (GUILayout.Button("현재 카메라 → 선택 구도 초안 가져오기"))
                    {
                        Undo.RecordObject(session, "카메라 자세 가져오기");
                        session.CaptureCurrentCamera();
                        serializedObject.Update();
                    }
                if (session.CurrentCameraView == 2)
                    EditorGUILayout.HelpBox("F8 기존 뷰: 현재 위치 저장이 잠겨 있습니다. F6 또는 F7로 이동한 뒤 구도를 잡아 주세요.", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("A 초안 적용 (F6)")) session.SelectCameraView(0);
                if (GUILayout.Button("B 초안 적용 (F7)")) session.SelectCameraView(1);
                if (GUILayout.Button("기존 뷰 (F8)")) session.SelectCameraView(2);
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(session.cameraProfile == null))
            {
                if (GUILayout.Button(new GUIContent("카메라 프로필 저장", "A/B 초안을 에셋에 저장합니다. 현재 카메라 위치를 자동으로 가져오지 않으며 F8은 저장하지 않습니다.")))
                {
                    Undo.RecordObject(session.cameraProfile, "카메라 프로필 저장");
                    session.cameraProfile.viewA = session.cameraDraftA.Copy();
                    session.cameraProfile.viewB = session.cameraDraftB.Copy();
                    EditorUtility.SetDirty(session.cameraProfile);
                    AssetDatabase.SaveAssetIfDirty(session.cameraProfile);
                }
                if (GUILayout.Button(new GUIContent("카메라 프로필 불러오기", "저장된 A/B로 초안을 덮어씁니다. 플레이 중 현재 구도의 위치·회전·FOV·원근감은 자동 반영됩니다.")))
                {
                    Undo.RecordObject(session, "카메라 프로필 불러오기");
                    session.LoadCameraProfile();
                    if (!Application.isPlaying) EditorUtility.SetDirty(session);
                    serializedObject.Update();
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            var session = (JcBattleTestSession)target;
            DrawEncounterTools(session);
            DrawCameraTools(session);
            EditorGUILayout.HelpBox(session.Status, MessageType.Info);
            if (GUILayout.Button(new GUIContent("설정 검증", "초기조건·프리팹·스킬을 검사합니다. 진행 중인 전투는 변경하지 않습니다.")))
            {
                bool valid = session.Validate(out var error);
                EditorUtility.DisplayDialog("JC 전투 테스트", valid ? "설정 연결 검증을 통과했습니다. 이벤트 적·스킬의 런타임 검증은 전투 시작 전에 수행합니다." : error, "확인");
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button(new GUIContent("설정 적용 및 전투 재시작", "행동 중에는 적용하지 않습니다. 인카운터 변경 시 적과 배경을 함께 교체하며 아군 초안·카메라는 유지합니다."))) session.ApplyAndRestart();
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

        private void DrawEncounterTools(JcBattleTestSession session)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("고정 인카운터 전투·배경", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("프리셋 선택 후 '설정 적용 및 전투 재시작'을 누르세요. 아군 초안과 카메라는 유지됩니다. 도로 보정 사용 시 폭·배율·방향 변경은 편집 모드에서 '현재 배경 도로 보정 갱신' 후 적용됩니다. 개별 소품 보정은 재시작 시 적용됩니다.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            foreach (string key in new[] { "Main_1-2", "Main_1-1", "Main_2-2" })
            {
                var preset = Resources.Load<JcEnemyPreset>("JC_Encounters/" + key);
                using (new EditorGUI.DisabledScope(preset == null))
                    if (GUILayout.Button(key))
                    {
                        Undo.RecordObject(session, "인카운터 프리셋 선택"); session.settings.enemyPreset = preset;
                        EditorUtility.SetDirty(session); serializedObject.Update();
                    }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("아놀드 · 좌표 기반 동적 배경",EditorStyles.boldLabel);
            if(!string.IsNullOrEmpty(session.DynamicBackgroundReport))EditorGUILayout.HelpBox(session.DynamicBackgroundReport,MessageType.Info);
            EditorGUILayout.HelpBox("아래 버튼은 좌표만 바꿉니다. settings > Dynamic Background에서 임의 조우/적 좌표를 입력할 수 있습니다. 플레이 중 설정 적용 및 전투 재시작으로 동일 생성 로직을 실행합니다. 카메라·아군은 유지합니다.",MessageType.Info);
            var dynamicPreset=Resources.Load<JcEnemyPreset>("JC_Encounters/Arnold_Dynamic");
            using(new EditorGUI.DisabledScope(dynamicPreset==null))
            {
                EditorGUILayout.BeginHorizontal();
                string[] labels={"외곽 (8,7)","교차로 (24,19)","교량 입구 (41,19)"};
                Vector3[] points={new Vector3(8,.02f,7),new Vector3(24,.02f,19),new Vector3(41,.02f,19)};
                for(int i=0;i<3;i++)if(GUILayout.Button(new GUIContent(labels[i],"미리 만든 배경을 고르는 것이 아니라 동적 생성 입력 좌표를 설정합니다.")))
                {
                    Undo.RecordObject(session,"동적 배경 테스트 좌표");session.settings.enemyPreset=dynamicPreset;
                    session.settings.dynamicBackground.source=Resources.Load<JcDynamicBattleSource>("JC_Encounters/DynamicSource");
                    session.settings.dynamicBackground.encounterPosition=points[i];session.settings.dynamicBackground.enemyPosition=points[i]+Vector3.right;
                    EditorUtility.SetDirty(session);serializedObject.Update();
                }
                EditorGUILayout.EndHorizontal();
            }
            using(new EditorGUI.DisabledScope(Application.isPlaying))
                if(GUILayout.Button(new GUIContent("탐사맵 공통 외형 입력 갱신","맵 전체 외형만 수집합니다. 조우 위치별 배경은 실행 시 생성합니다. 씬은 저장하지 않습니다.")))
                    try{JcEncounterBackgroundBuilder.BuildDynamicSource();}catch(System.Exception ex){Debug.LogException(ex);}
            var background = session.settings.enemyPreset != null ? session.settings.enemyPreset.background : null;
            if (background != null && GUILayout.Button("현재 인카운터 배경 설정 선택")) Selection.activeObject = background;
            using (new EditorGUI.DisabledScope(Application.isPlaying || background == null || !background.fitBattleRoad))
                if (GUILayout.Button(new GUIContent("현재 배경 도로 보정 갱신", "이미 추출한 외형에 도로 폭과 외곽 여유를 적용하여 저장합니다. 원본 탐사씬과 전투씬은 저장하지 않습니다.")))
                {
                    try { JcEncounterBackgroundBuilder.FitBattleRoad(background); }
                    catch (System.Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("도로 보정 실패", ex.Message, "확인"); }
                }
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("탐사씬 저장본 → 3종 배경 재추출"))
                {
                    try { JcEncounterBackgroundBuilder.BuildAll(); }
                    catch (System.Exception ex) { Debug.LogException(ex); EditorUtility.DisplayDialog("배경 추출 실패", ex.Message, "확인"); }
                }
        }
    }
}
