using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-12 | 기존 SimulationBattleConfig를 초안으로 편집합니다. 실제 게임 프로필 저장과 시작 설정 저장을 구분합니다.
    [CustomEditor(typeof(JcStandaloneBattleTest))]
    public sealed class JcStandaloneBattleTestEditor : UnityEditor.Editor
    {
        Scene previewScene;
        DHCsvTemplateCatalog previewCatalog;
        // JC | 2026-11-12 | 편집 모드에서도 씬이 참조하는 카탈로그 복사본으로 미리보기만 계산합니다. 원본과 전역 Instance는 변경하지 않습니다.
        DHCsvTemplateCatalog Catalog(JcStandaloneBattleTest test)
        {
            if (Application.isPlaying) return DHCsvTemplateCatalog.Instance;
            if (previewCatalog) return previewCatalog;
            var source = test.gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<DHCsvTemplateCatalog>(true)).FirstOrDefault();
            if (!source) return null;
            previewScene = EditorSceneManager.NewPreviewScene();
            var copy = Instantiate(source.gameObject);
            copy.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(copy, previewScene);
            previewCatalog = copy.GetComponent<DHCsvTemplateCatalog>();
            return previewCatalog;
        }
        void OnDisable()
        {
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            previewCatalog = null;
        }
        public override void OnInspectorGUI()
        {
            var test = (JcStandaloneBattleTest)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            if (!test.Draft) { EditorGUILayout.HelpBox("시작 프로필이 필요합니다.", MessageType.Error); return; }
            var values = new SerializedObject(test.Draft);
            values.Update(); DrawPropertiesExcluding(values, "m_Script", "allies"); values.ApplyModifiedProperties();
            DrawHeroes(test);
            if (!test.ValidateEncounter(test.Draft, out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("랭크/IP 편집은 플레이 중 선택 대기 상태에서 반영됩니다. 공격 연출 중에는 반영을 기다립니다. 히어로 종류·장비·HP·적 구성은 재시작 때 적용됩니다. 저장 전에는 초안만 바뀌며, 카메라·DOF·환경은 각 조절기의 저장 버튼을 사용합니다.", MessageType.Info);
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

        // JC | 2026-11-12 | 랭크 이름과 레벨을 이중 저장하지 않습니다. 성장표 행을 고르면 기존 Level과 습득 스킬이 함께 바뀝니다.
        void DrawHeroes(JcStandaloneBattleTest test)
        {
            var catalog = Catalog(test);
            if (!catalog) { EditorGUILayout.HelpBox("이 테스트씬의 원본 카탈로그 연결이 필요합니다.", MessageType.Error); return; }
            var ranks = catalog.GetUnitGrowthTemplates().Where(r => r != null).OrderBy(r => r.Level).ToArray();
            var heroes = catalog.GetAllPlayerUnitTemplates();
            var inputs = test.Draft.allies;
            int count = Mathf.Clamp(EditorGUILayout.IntField("아군 슬롯 수", inputs.Count), 1, 6);
            while (inputs.Count < count) inputs.Add(new SimulationAllyInput { Enabled = true, UnitTemplateKey = heroes.FirstOrDefault()?.UnitKey });
            while (inputs.Count > count) inputs.RemoveAt(inputs.Count - 1);
            foreach (var input in inputs)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                input.Enabled = EditorGUILayout.Toggle("사용", input.Enabled);
                using (new EditorGUI.DisabledScope(!input.Enabled))
                {
                    int hero = heroes.FindIndex(h => h.UnitKey == input.UnitTemplateKey);
                    int selectedHero = EditorGUILayout.Popup("히어로", hero, heroes.Select(h => h.UnitName).ToArray());
                    if (selectedHero >= 0 && selectedHero != hero) input.UnitTemplateKey = heroes[selectedHero].UnitKey;
                    int previousLevel = input.Level;
                    float previousIP = input.InitialInfluence;
                    int rank = System.Array.FindLastIndex(ranks, r => r.Level <= input.Level);
                    int selectedRank = EditorGUILayout.Popup(new GUIContent("히어로 랭크", "기존 성장표의 레벨·스킬 습득·IP 상한을 함께 적용합니다."),
                        rank, ranks.Select(r => $"{r.Rank} · 레벨 {r.Level}").ToArray());
                    if (selectedRank >= 0 && selectedRank != rank) input.Level = ranks[selectedRank].Level;
                    EditorGUILayout.LabelField("적용 레벨", input.Level.ToString());
                    input.WeaponKey = EditorGUILayout.TextField("시작 코어 키 (빈 값=기본)", input.WeaponKey);
                    input.HpRatio = EditorGUILayout.Slider("시작 HP 비율", input.HpRatio, .01f, 1f);
                    // 미습득 스킬 ID를 임의 주입하는 입력 대신 실제 성장표의 습득 결과를 표시합니다.
                    if (SimulationUnitFactory.TryCreateRuntimeData(input, 0, catalog, out var preview, out var error))
                    {
                        float cap = preview.UnitData.IngameStats.Influence;
                        float requested = EditorGUILayout.FloatField(new GUIContent("IP", "직접 입력합니다. 현재 랭크의 IP 상한으로 강제 제한하며 플레이 중에도 반영됩니다."), input.InitialInfluence);
                        input.InitialInfluence = Mathf.Clamp(float.IsNaN(requested) ? 0 : requested, 0, cap);
                        EditorGUILayout.LabelField("IP 상한 / 칭호", $"{cap:0} / {JcHeroInfluenceBadge.Title(input.InitialInfluence)}");
                        var live = test.GetLiveHero(input);
                        if (live) EditorGUILayout.LabelField("현재 전투 상태", $"{UnitRankLookup.GetRank(live.Level)} · IP {live.CurrentInfluence:0}/{live.MaxInfluence:0}");
                        if (catalog.TryGetPlayerUnitTemplate(input.UnitTemplateKey, out var template))
                        {
                            var learned = catalog.GetCurrentClassSkills(template.ClassIndex, input.Level);
                            EditorGUILayout.LabelField("습득 스킬", string.Join(" / ", learned.Select(s => s.skillName)), EditorStyles.wordWrappedLabel);
                        }
                        if (input.Level != previousLevel || input.InitialInfluence != previousIP) test.RequestHeroTuning(input);
                    }
                    else EditorGUILayout.HelpBox(error, MessageType.Error);
                }
                EditorGUILayout.EndVertical();
            }
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
