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
        BattleFlowManager runtimeFlow;
        bool started, ownsContext;
        int appliedPeriod = -1;
        public string Status { get; private set; } = "편집 모드 · Play로 전용 전투 시작";
        public bool Started => started;
        static readonly float[] Hours = { 8.5f, 14.5f, 20.5f, 2.5f };
        static string restartPath, restartConfig;
        static int restartPeriod;
        static bool restartGuidance;
        // JC | 2026-11-12 | 편집한 랭크/IP는 연출 중 교체하지 않고 안전한 선택 대기 시점에 한 번만 적용합니다.
        readonly Dictionary<int, SimulationAllyInput> pendingHeroTuning = new Dictionary<int, SimulationAllyInput>();
        readonly Dictionary<SimulationAllyInput, int> startedHeroes = new Dictionary<SimulationAllyInput, int>();
        // JC | 2026-11-13 | 테스트용 사망 요청은 실제 유닛 참조만 보관하며 시작 프로필에는 저장하지 않습니다.
        readonly Queue<BattleCharactor> pendingHeroDeaths = new Queue<BattleCharactor>();
        public SimulationBattleConfig Draft
        {
            get
            {
                // JC | 2026-11-12 | 다른 저장 프로필을 연결하면 이전 초안을 새 프로필에 잘못 저장하지 않도록 분리합니다.
                if (draftSource != profile)
                {
                    pendingHeroTuning.Clear(); startedHeroes.Clear();
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
            int slot = 0;
            foreach (var input in Draft.allies.Where(a => a != null && a.Enabled))
                startedHeroes[input] = allies[slot++].RuntimeUnitIndex;
            Status = "실제 전투 로직 · 임시 파티 · " + Draft.enemyGroupKey;
        }
        public void RefreshLighting()
        {
            if (!started || !presentation || appliedPeriod == period) return;
            period = Mathf.Clamp(period, 0, 3);
            presentation.SetTestPeriod(period);
            appliedPeriod = period;
        }
        void Update()
        {
            if (started && period != appliedPeriod) RefreshLighting();
            ApplyPendingHeroTuning();
            ApplyPendingHeroDeaths();
        }

        public void RequestHeroDeath(SimulationAllyInput input)
        {
            if (!Application.isPlaying || !started) return;
            var unit = GetLiveHero(input);
            if (!unit || !unit.IsPlayer || unit.IsDead || pendingHeroDeaths.Contains(unit)) return;
            pendingHeroDeaths.Enqueue(unit);
            Status = unit.DisplayName + " · 사망 처리 대기 (공격/턴 연출 종료 후)";
            ApplyPendingHeroDeaths();
        }

        // JC | 2026-11-13 | 기존 피해→사망 이벤트를 그대로 사용해 턴/애니메이션/부활 연결을 유지합니다.
        // 무적·최소 HP·전용 무력화 규칙은 강제로 우회하지 않고 결과를 상태란에 알립니다.
        void ApplyPendingHeroDeaths()
        {
            if (!Application.isPlaying || !started || pendingHeroDeaths.Count == 0 || !runtimeFlow) return;
            if (runtimeFlow.IsEndingBattle || runtimeFlow.IsBattleEnded) { pendingHeroDeaths.Clear(); return; }
            if (!runtimeFlow.CurrentUnit || runtimeFlow.IsActionInProgress || runtimeFlow.IsTurnPresentationPending || runtimeFlow.IsFlowBlocked) return;
            var unit = pendingHeroDeaths.Dequeue();
            if (!unit || unit.IsDead || !unit.IsPlayer || !runtimeFlow.Participants.Contains(unit)) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var input in root.GetComponentsInChildren<InputHandler>(true)) input.ClearSelectionState();
            unit.TakeDamage(unit.CurrentHp);
            Status = unit.IsDead ? unit.DisplayName + " · 사망 처리 완료 (시작 설정은 유지)"
                : unit.DisplayName + " · 전투의 피해 방지/무력화 규칙에 의해 사망하지 않았습니다.";
        }

        public void RequestHeroTuning(SimulationAllyInput input)
        {
            if (!Application.isPlaying || !started || !startedHeroes.TryGetValue(input, out int id)) return;
            pendingHeroTuning[id] = JsonUtility.FromJson<SimulationAllyInput>(JsonUtility.ToJson(input));
            Status = "랭크/IP 변경 · 선택 대기 상태에서 반영";
            ApplyPendingHeroTuning();
        }

        // JC | 2026-11-12 | 초안 입력과 실제 전투 중 변화한 IP를 구분하여 보여줍니다. 다른 씬의 유닛은 조회하지 않습니다.
        public BattleCharactor GetLiveHero(SimulationAllyInput input)
        {
            if (!started || !startedHeroes.TryGetValue(input, out int id)) return null;
            if (!runtimeFlow) runtimeFlow = gameObject.scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<BattleFlowManager>(true)).FirstOrDefault();
            return runtimeFlow ? runtimeFlow.Participants.FirstOrDefault(u => u && u.SourceData != null &&
                u.SourceData.UnitIndex == id && u.SourceData.UnitTemplateKey == input.UnitTemplateKey) : null;
        }

        void ApplyPendingHeroTuning()
        {
            if (!Application.isPlaying || pendingHeroTuning.Count == 0 || !started) return;
            var flow = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleFlowManager>(true)).FirstOrDefault();
            if (!flow || !flow.CurrentUnit || flow.IsActionInProgress || flow.IsTurnPresentationPending ||
                flow.IsFlowBlocked || flow.IsEndingBattle || flow.IsBattleEnded) return;
            using (flow.AcquireFlowLock(this, "테스트 랭크/IP 반영"))
            {
                bool refreshed = false, changedRank = false;
                foreach (var pair in pendingHeroTuning)
                {
                    var unit = flow.Participants.FirstOrDefault(u => u && u.SourceData != null && u.SourceData.UnitIndex == pair.Key);
                    if (!unit || unit.IsDead || unit.SourceData.UnitTemplateKey != pair.Value.UnitTemplateKey)
                    { Status = "사망하거나 교체된 히어로는 재시작 후 설정됩니다."; continue; }
                    // 장비·HP 비율은 시작 설정이므로 현재 장비를 보존한 채 랭크와 IP만 변경합니다.
                    pair.Value.WeaponKey = unit.SourceData.CurrentWeaponKey;
                    if (!SimulationUnitFactory.TryCreateRuntimeData(pair.Value, 0, out var next, out var error))
                    { Status = error; continue; }
                    if (unit.Level != pair.Value.Level)
                    {
                        changedRank = true;
                        foreach (var input in gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InputHandler>(true)))
                            input.ClearSelectionState();
                    }
                    if (unit.ApplySimulationGrowth(next.UnitData))
                    { refreshed = true; Status = $"{unit.DisplayName} · {UnitRankLookup.GetRank(unit.Level)} · IP {unit.CurrentInfluence:0}/{unit.MaxInfluence:0}"; }
                }
                pendingHeroTuning.Clear();
                if (refreshed)
                {
                    foreach (var root in gameObject.scene.GetRootGameObjects())
                    {
                        foreach (var info in root.GetComponentsInChildren<HeroInfoPanel>(true)) info.RefreshSimulationDisplay();
                        if (changedRank)
                        {
                            foreach (var skills in root.GetComponentsInChildren<SkillButtonController>(true)) skills.RefreshSimulationSkills();
                            foreach (var turns in root.GetComponentsInChildren<TurnOrderUI>(true)) turns.RefreshSimulationPreview();
                        }
                    }
                }
            }
        }
        public void ReloadProfile()
        {
            if (!profile || !Draft) return;
            var slots = Draft.allies.Select(a => a != null && startedHeroes.TryGetValue(a, out int id) ? id : 0).ToArray();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), Draft);
            pendingHeroTuning.Clear(); startedHeroes.Clear();
            for (int i = 0; i < Mathf.Min(slots.Length, Draft.allies.Count); i++)
            {
                var input = Draft.allies[i];
                if (input == null || slots[i] == 0) continue;
                startedHeroes[input] = slots[i];
                RequestHeroTuning(input);
            }
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
