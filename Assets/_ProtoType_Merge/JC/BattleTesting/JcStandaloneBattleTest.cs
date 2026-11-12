using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    // JC | 2026-11-12 | 전용 테스트씬 안에서 원본 전투를 기동합니다. 데이터/스폰/전투는 기존 Simulation 경로를 재사용합니다.
    [DefaultExecutionOrder(-31000), DisallowMultipleComponent]
    public sealed class JcStandaloneBattleTest : MonoBehaviour
    {
        public enum Scenario { Boss, Bank, TutorialFirst, Main12 }
        [Tooltip("이 씬이 검수하는 실제 전투 종류입니다.")] public Scenario scenario;
        [Tooltip("시작 파티·적 구성의 저장 프로필입니다. 편집은 초안에만 반영됩니다.")] public SimulationBattleConfig profile;
        [Tooltip("첫 전투의 실제 안내를 실행합니다. 시작/재시작 때 적용하며 기본은 자유 테스트입니다.")] public bool tutorialGuidance;
        [Range(0, 3), Tooltip("아침·오후·저녁·새벽. 은행 실내 고정 조명은 유지합니다.")] public int period;
        [HideInInspector] public GameObject stage;
        [HideInInspector] public BattleSceneManager manager;
        [HideInInspector] public GameObject[] runtimeRoots;
        SimulationBattleConfig draft, draftSource;
        JcArnoldBattlePresentation presentation;
        bool started, ownsContext;
        int appliedPeriod = -1;
        public string Status { get; private set; } = "편집 모드 · Play로 전용 전투 시작";
        public bool Started => started;
        static readonly float[] Hours = { 8.5f, 14.5f, 20.5f, 2.5f };
        static string restartPath, restartConfig;
        static int restartPeriod;
        static bool restartGuidance;
        public SimulationBattleConfig Draft
        {
            get
            {
                // JC | 2026-11-12 | 다른 저장 프로필을 연결하면 이전 초안을 새 프로필에 잘못 저장하지 않도록 분리합니다.
                if (draftSource != profile)
                {
                    if (draft) { if (Application.isPlaying) Destroy(draft); else DestroyImmediate(draft); }
                    draft = null; draftSource = profile;
                }
                if (!draft && profile) { draft = Instantiate(profile); draft.hideFlags = HideFlags.DontSave; }
                return draft;
            }
        }
        public static JcStandaloneBattleTest ForScene(Scene scene) => scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().Select(r => r.GetComponent<JcStandaloneBattleTest>()).FirstOrDefault(c => c) : null;
        public static bool IsTestScene(Scene scene) => ForScene(scene) != null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { restartPath = restartConfig = null; }

        void Awake()
        {
            // JC | 2026-11-12 | 카탈로그 Awake 뒤, 보스 그리드 Awake 전에 컨텍스트를 준비하도록 전투 루트만 대기시킵니다.
            // 카탈로그는 별도 활성 루트에 두고, 실제 전투 오브젝트의 직렬화 연결/루트 이름은 유지합니다.
            foreach (var root in runtimeRoots ?? Array.Empty<GameObject>()) if (root) root.SetActive(false);
        }
        void Start()
        {
            if (restartPath == gameObject.scene.path && Draft)
            {
                JsonUtility.FromJsonOverwrite(restartConfig, Draft);
                period = restartPeriod; tutorialGuidance = restartGuidance;
                restartPath = restartConfig = null;
            }
            try { Begin(); }
            catch (Exception e) { Status = "시작 실패: " + e.Message; Debug.LogException(e, this); }
        }
        public bool ValidateRequest(out List<SimulationAllyRuntimeData> allies, out string error)
        {
            allies = new List<SimulationAllyRuntimeData>(); error = "";
            if (!Draft || !manager || !stage) { error = "시작 프로필·전투 관리자·무대 연결을 확인하세요."; return false; }
            if (!DHCsvTemplateCatalog.Instance || !EventScriptCatalog.Instance) { error = "기존 원본 카탈로그가 준비되지 않았습니다."; return false; }
            if (!ValidateEncounter(Draft, out error)) return false;
            var inputs = (Draft.allies ?? new List<SimulationAllyInput>()).Where(a => a != null && a.Enabled).ToArray();
            if (inputs.Length < 1 || inputs.Length > 6) { error = "아군을 1~6명 지정하세요."; return false; }
            foreach (var input in inputs)
            {
                if (!SimulationUnitFactory.TryCreateRuntimeData(input, allies.Count, out var ally, out error)) return false;
                allies.Add(ally);
            }
            return SimulationUnitFactory.ValidateEnemy(Draft.enemyGroupKey, Draft.eventZoneId, Draft.enemyLevel, out error);
        }
        public bool ValidateEncounter(SimulationBattleConfig config, out string error)
        {
            bool valid = config && config.enemyLevel > 0 && (scenario switch
            {
                Scenario.Boss => config.eventZoneId == 2 && config.enemyGroupKey == "BE490",
                Scenario.Bank => config.eventZoneId == 1 && new[] { "BE002", "BE003", "BE004" }.Contains(config.enemyGroupKey),
                Scenario.TutorialFirst => config.eventZoneId == 0 && config.enemyGroupKey == "TUFEP001",
                _ => config.eventZoneId == 1 && config.enemyGroupKey == "BE006"
            });
            error = valid ? "" : "이 씬의 전투 키·구역 또는 적 레벨이 올바르지 않습니다.";
            return valid;
        }
        void Begin()
        {
            if (!ValidateRequest(out var allies, out string error)) throw new InvalidOperationException(error);
            var context = CombatContext.EnsureInstance();
            string party = scenario == Scenario.Boss ? "JC_BOSS_PREVIEW" : "JC_STANDALONE_" + scenario;
            if (!context.BeginSimulation(party, allies, Draft.enemyGroupKey, Draft.enemyLevel, gameObject.scene.name, Draft.eventZoneId))
                throw new InvalidOperationException("테스트 전투 컨텍스트 생성 실패");
            ownsContext = true;
            // JC | 2026-11-12 | 안내 실행도 Simulation을 유지하여 튜토리얼 진행/보상 저장 경로에 진입하지 않습니다.
            manager.ConfigureStandaloneSimulation(scenario == Scenario.TutorialFirst && tutorialGuidance ? "TUT_01" : null);
            foreach (var root in runtimeRoots) if (root) root.SetActive(true);
            presentation = JcArnoldBattlePresentation.ApplyStandalone(gameObject.scene, stage, Hours[Mathf.Clamp(period, 0, 3)], scenario == Scenario.TutorialFirst);
            JcSimulationBattleControls.AttachToScene(gameObject.scene);
            started = true; appliedPeriod = period;
            Status = "실제 전투 로직 · 임시 파티 · " + Draft.enemyGroupKey;
        }
        public void RefreshLighting()
        {
            if (!started || !presentation || appliedPeriod == period) return;
            period = Mathf.Clamp(period, 0, 3);
            presentation.SetTestPeriod(period);
            appliedPeriod = period;
        }
        void Update() { if (started && period != appliedPeriod) RefreshLighting(); }
        public void ReloadProfile()
        {
            if (profile && Draft) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), Draft);
        }
        public void Restart()
        {
            if (!Application.isPlaying || !Draft) return;
            restartPath = gameObject.scene.path; restartConfig = JsonUtility.ToJson(Draft);
            restartPeriod = period; restartGuidance = tutorialGuidance;
            // JC | 2026-11-12 | 테스트씬 자체를 다시 열며 TmpBattleScene이나 본게임으로 전환하지 않습니다.
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(gameObject.scene.name);
#endif
        }
        void OnGUI()
        {
            if (!Application.isPlaying) return;
            using var fontScope = new DebugPanelManager.DeveloperGuiFontScope(GUI.skin);
            var old = GUI.matrix;
            float scale = Mathf.Max(.65f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(12, 8, 390, 100), GUI.skin.box);
            GUILayout.Label(Status);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 설정으로 재시작")) Restart();
            if (scenario == Scenario.TutorialFirst) tutorialGuidance = GUILayout.Toggle(tutorialGuidance, "다음 시작: 안내 실행");
            GUILayout.EndHorizontal();
            if (scenario != Scenario.Bank)
            {
                int next = GUILayout.Toolbar(period, new[] { "아침", "오후", "저녁", "새벽" });
                if (next != period) { period = next; RefreshLighting(); }
            }
            GUILayout.EndArea(); GUI.matrix = old;
        }
        void OnDestroy()
        {
            if (ownsContext && CombatContext.Instance && CombatContext.Instance.IsSimulation && CombatContext.Instance.ReturnSceneName == gameObject.scene.name)
                CombatContext.Instance.ClearSimulation();
            if (draft) { if (Application.isPlaying) Destroy(draft); else DestroyImmediate(draft); }
        }
    }
}
