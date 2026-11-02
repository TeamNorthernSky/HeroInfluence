using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YT.Simulation
{
    /// <summary>설정만 소유하고, 전투는 메인의 TmpBattleScene과 현재 카탈로그에 위임합니다.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class YtOutpostSimulation : MonoBehaviour
    {
        public const string ScenePath = "Assets/_ProtoType_Merge/YT/OutpostSimulationScene.unity";
        public const string BattleScene = "TmpBattleScene";
        public enum TurnMode { InOrder, HeroesOnly, EnemiesOnly }

        private sealed class AllyRow
        {
            public Toggle enabled;
            public Dropdown hero, weapon;
            public InputField level, hp;
            public List<DHWeaponTemplate> weapons;
        }

        private readonly List<AllyRow> rows = new List<AllyRow>();
        private readonly Dictionary<BattleCharactor, IDisposable> protection = new Dictionary<BattleCharactor, IDisposable>();
        private List<DHPlayerUnitTemplate> heroes;
        private readonly List<Encounter> encounters = new List<Encounter>();
        private sealed class Encounter { public string key, label; public int zone; }
        private Canvas canvas;
        private GameObject setup, toolbar;
        private Font font;
        private Dropdown encounter, mode, turns;
        private Toggle refillIp;
        private Text status, battleStatus;
        private BattleFlowManager flow;
        private BattleSceneManager sceneManager;
        private BattleResultPanel resultPanel;
        private GameObject enemyControls;
        private Dropdown manualEnemy, manualSkill, manualTarget;
        private Text manualMessage, manualDescription;
        private UnityEngine.UI.Button manualPlay;
        private readonly List<BattleCharactor> manualEnemies = new List<BattleCharactor>();
        private List<SkillData> manualSkills = new List<SkillData>();
        private List<BattleCharactor> manualTargets = new List<BattleCharactor>();
        private BattleCharactor selectedEnemy;
        private bool waitingForEnemyInput, enemyCastRequested, enemyCasting;
        public int CompletedEnemyCasts { get; private set; }
        public bool LastEnemyCastSucceeded { get; private set; }
        private bool ManualEnemyMode => freeMode && turnMode == TurnMode.EnemiesOnly;
        private bool loading, inBattle, freeMode, refill;
        private TurnMode turnMode;
        private string selectedLabel;
        private float oldBackground;
        private static List<SimulationAllyInput> sessionInputs;
        private static string sessionEncounter;
        private static bool sessionFree, sessionRefill;
        private static TurnMode sessionTurns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { sessionInputs = null; sessionEncounter = null; }
        public int CompletedTurns { get; private set; }
        public bool IsInBattle => inBattle;
        public string LastError { get; private set; }
        public IReadOnlyList<string> EncounterKeys => encounters.Select(e => e.key).ToArray();

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            oldBackground = Application.runInBackground ? 1f : 0f;
            Application.runInBackground = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            while (DHCsvTemplateCatalog.Instance == null || EventScriptCatalog.Instance == null) yield return null;
            DHCsvTemplateCatalog.Instance.ReloadTemplates();
            heroes = DHCsvTemplateCatalog.Instance.GetAllPlayerUnitTemplates().OrderBy(h => h.UnitKey).ToList();
            if (heroes.Count == 0) { Debug.LogError("[YT 모의전] 메인 영웅 테이블이 비어 있습니다."); yield break; }
            // 전투 수치/구성은 이곳에 복사하지 않고 현행 이벤트 카탈로그로 해석합니다.
            AddEncounter(1, "BE002", "1구역 거점 · 은행강도 / 인질 구출");
            AddEncounter(1, "BE003", "1구역 거점 · 은행강도 / 정면 돌파");
            AddEncounter(1, "BE004", "1구역 거점 · 은행강도 / 루미나 폭주");
            AddEncounter(2, "BE490", "빌런협회 · 율리아");
            BuildUi();
        }

        private void AddEncounter(int zone, string key, string label)
        {
            if (EventScriptCatalog.Instance.TryGetBattleEnemyGroupTemplate(zone, key, out var group))
                encounters.Add(new Encounter { zone = zone, key = key, label = label + "  (" + group.Members.Count + "명)" });
            else Debug.LogError("[YT 모의전] 메인 이벤트 전투를 찾을 수 없습니다: " + key);
        }

        private void BuildUi()
        {
            font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 20);
            var root = new GameObject("YT Simulation UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            setup = Box(root.transform, "Setup", 0, 0, 1920, 1080, new Color(.035f, .05f, .08f, .98f));
            var panel = Box(setup.transform, "Settings", 290, 100, 1340, 890, new Color(.075f, .1f, .15f));
            Label(panel.transform, "거점 모의전", 36, 22, 1100, 54, 34);
            Label(panel.transform, "메인 전투 데이터 · 실제 전투 씬 사용 · 성장과 전투 결과는 저장하지 않습니다", 38, 82, 1250, 35, 20);
            Label(panel.transform, "전투 대상", 38, 140, 180, 32);
            encounter = Select(panel.transform, 210, 136, 1088, encounters.Select(e => e.label).ToList());
            Label(panel.transform, "모드", 38, 200, 180, 32);
            mode = Select(panel.transform, 210, 196, 360, new List<string> { "배틀 모드", "프리 모드 · 스킬 관찰" });
            turns = Select(panel.transform, 590, 196, 330, new List<string> { "정상 턴 순서", "영웅 턴만", "적 턴만" });
            refillIp = Check(panel.transform, "매 턴 IP 회복", 950, 196, true);
            mode.onValueChanged.AddListener(_ => UpdateModeUi());
            Label(panel.transform, "편성 / 성장 설정", 38, 267, 300, 34, 24);
            foreach (int lv in new[] { 1, 5, 10, 20 })
            {
                int value = lv;
                Button(panel.transform, "전체 Lv." + lv, 560 + Array.IndexOf(new[] { 1, 5, 10, 20 }, lv) * 185, 263, 170, () => rows.ForEach(r => r.level.text = value.ToString()));
            }
            Label(panel.transform, "사용", 38, 321, 70, 30, 18);
            Label(panel.transform, "영웅 (위에서부터 배치)", 130, 321, 310, 30, 18);
            Label(panel.transform, "레벨", 465, 321, 100, 30, 18);
            Label(panel.transform, "장비 (기본 강화 단계)", 595, 321, 430, 30, 18);
            Label(panel.transform, "시작 HP %", 1085, 321, 180, 30, 18);
            for (int i = 0; i < 6; i++)
            {
                float y = 364 + i * 61;
                var row = new AllyRow();
                rows.Add(row);
                row.enabled = Check(panel.transform, (i + 1).ToString(), 38, y, i < 4);
                row.hero = Select(panel.transform, 130, y, 310, heroes.Select(h => h.UnitName).ToList());
                row.hero.value = i % heroes.Count;
                row.level = Field(panel.transform, "1", 465, y, 100);
                row.weapon = Select(panel.transform, 595, y, 465, new List<string>());
                row.hp = Field(panel.transform, "100", 1085, y, 180);
                PopulateWeapons(row);
                row.hero.onValueChanged.AddListener(_ => PopulateWeapons(row));
            }
            Label(panel.transform, "레벨별 해금 스킬과 장비 능력은 메인의 성장표를 따릅니다. 적 레벨도 본게임의 구역/전투 규칙으로 결정합니다.", 38, 745, 1260, 42, 18);
            status = Label(panel.transform, "배틀: 정상 승패 판정   /   프리: 양 진영 사망 방지 + 선택한 진영의 턴 진행", 38, 791, 960, 65, 18);
            Button(panel.transform, "모의전 시작", 1050, 795, 245, () => LaunchFromUi());
            toolbar = Box(root.transform, "BattleToolbar", 570, 12, 780, 68, new Color(.035f, .05f, .08f, .95f));
            battleStatus = Label(toolbar.transform, "", 14, 7, 440, 52, 16);
            Button(toolbar.transform, "회복", 460, 12, 100, RestoreAll);
            Button(toolbar.transform, "설정으로", 580, 12, 180, ReturnToSetup);
            toolbar.SetActive(false);
            BuildEnemyControls(root.transform);
            RestoreSessionUi();
            UpdateModeUi();
        }

        private void RestoreSessionUi()
        {
            if (sessionInputs == null) return;
            encounter.value = Mathf.Max(0, encounters.FindIndex(e => e.key == sessionEncounter));
            mode.value = sessionFree ? 1 : 0; turns.value = (int)sessionTurns; refillIp.isOn = sessionRefill;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; row.enabled.isOn = i < sessionInputs.Count;
                if (i >= sessionInputs.Count) continue;
                var input = sessionInputs[i];
                row.hero.value = Mathf.Max(0, heroes.FindIndex(h => h.UnitKey == input.UnitTemplateKey));
                PopulateWeapons(row);
                row.weapon.value = row.weapons.FindIndex(w => w.WeaponKey == input.WeaponKey) + 1;
                row.level.text = input.Level.ToString(); row.hp.text = Mathf.RoundToInt(input.HpRatio * 100).ToString();
            }
        }

        private void PopulateWeapons(AllyRow row)
        {
            row.weapons = DHCsvTemplateCatalog.Instance.GetWeaponTemplatesByClassIndex(heroes[row.hero.value].ClassIndex);
            row.weapon.ClearOptions();
            row.weapon.AddOptions(new[] { "기본 장비" }.Concat(row.weapons.Select(w => w.WeaponName)).ToList());
            row.weapon.value = 0;
        }

        private void UpdateModeUi()
        {
            turns.interactable = mode.value == 1;
            refillIp.interactable = mode.value == 1;
        }

        private void LaunchFromUi()
        {
            var inputs = new List<SimulationAllyInput>();
            foreach (var row in rows.Where(r => r.enabled.isOn))
            {
                if (!int.TryParse(row.level.text, out int level) || level < 1 || level > SimulationUnitFactory.MaxSimulationLevel ||
                    !int.TryParse(row.hp.text, out int hp) || hp < 1 || hp > 100)
                { SetError("영웅 레벨은 1~999, 시작 HP는 1~100 사이 정수로 입력하세요."); return; }
                inputs.Add(new SimulationAllyInput { Enabled = true, UnitTemplateKey = heroes[row.hero.value].UnitKey,
                    Level = level, HpRatio = hp / 100f, WeaponKey = row.weapon.value > 0 ? row.weapons[row.weapon.value - 1].WeaponKey : "" });
            }
            if (encounters.Count == 0) { SetError("메인 전투 목록이 없습니다."); return; }
            var chosen = encounters[encounter.value];
            Launch(chosen.key, chosen.zone, inputs, mode.value == 1, (TurnMode)turns.value, refillIp.isOn);
        }

        /// <summary>UI와 검증에서 공통 사용하는 진입점. 실패하면 설정 화면을 유지합니다.</summary>
        public bool Launch(string key, int zone, List<SimulationAllyInput> inputs, bool isFree, TurnMode order, bool restoreIp)
        {
            if (loading || inBattle) return false;
            DHCsvTemplateCatalog.Instance.ReloadTemplates();
            EventScriptCatalog.Instance.Reload();
            if (inputs == null || inputs.Count(i => i != null && i.Enabled) < 1 || inputs.Count(i => i != null && i.Enabled) > 6)
            { SetError("영웅을 1~6명 선택하세요."); return false; }
            var allies = new List<SimulationAllyRuntimeData>();
            foreach (var input in inputs.Where(i => i != null && i.Enabled))
            {
                if (!SimulationUnitFactory.TryCreateRuntimeData(input, allies.Count, out var ally, out var error))
                { SetError(error); return false; }
                // 모의전 시작 IP는 해당 성장 단계의 정상 상한입니다.
                var d = ally.UnitData;
                d.ApplyRuntimeState(d.UnitTemplateKey, d.Level, d.BaseStats, d.LevelupStats, d.CurrentSkillIndex,
                    d.CurrentWeaponKey, d.CurrentWeaponIndex, d.CurrentWeaponStats, d.IngameStats, d.CurrentHp,
                    nextCurrentInfluence: d.IngameStats.Influence);
                allies.Add(ally);
            }
            int enemyLevel = ResolveEnemyLevel(zone, key, inputs.Where(i => i != null && i.Enabled).Average(i => i.Level));
            if (!SimulationUnitFactory.ValidateEnemy(key, zone, enemyLevel, out string validation))
            { SetError(validation); return false; }
            if (!CombatContext.EnsureInstance().BeginSimulation("YT_OUTPOST_SIMULATION", allies, key, enemyLevel, "OutpostSimulationScene", zone))
            { SetError("모의전 컨텍스트 생성에 실패했습니다."); return false; }
            freeMode = isFree; turnMode = order; refill = restoreIp; CompletedTurns = 0;
            sessionInputs = inputs.Where(i => i != null && i.Enabled)
                .Select(i => JsonUtility.FromJson<SimulationAllyInput>(JsonUtility.ToJson(i))).ToList();
            sessionEncounter = key; sessionFree = isFree; sessionTurns = order; sessionRefill = restoreIp;
            selectedLabel = encounters.FirstOrDefault(e => e.key == key && e.zone == zone)?.label ?? key;
            LastError = null;
            loading = true;
            setup.SetActive(false);
            toolbar.SetActive(true);
            try
            {
                JC.BattleTesting.JcArnoldBattlePresentation.PrepareCameraOnly(BattleScene);
                SceneManager.LoadScene(BattleScene);
            }
            catch (Exception ex)
            {
                loading = false;
                CombatContext.Instance.ClearSimulation();
                setup.SetActive(true); toolbar.SetActive(false); SetError(ex.Message); return false;
            }
            return true;
        }

        public static int ResolveEnemyLevel(int zone, string key, double averageHeroLevel)
        {
            var repository = MapProgressRepository.Instance;
            int level = repository != null && repository.TryGetZoneEnemyLevel(zone.ToString(), out int stored)
                ? stored : Mathf.Max(1, (int)averageHeroLevel);
            if (EventScriptCatalog.Instance.TryGetBattleEnemyGroupTemplate(zone, key, out var group))
            {
                if (group.MinLevel > 0) level = Mathf.Max(level, group.MinLevel);
                if (group.MaxLevel > 0) level = Mathf.Min(level, group.MaxLevel);
            }
            return Mathf.Max(1, level);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name != BattleScene || !loading) return;
            flow = FindObjectOfType<BattleFlowManager>();
            sceneManager = FindObjectOfType<BattleSceneManager>();
            resultPanel = FindObjectsOfType<BattleResultPanel>(true).FirstOrDefault();
            if (flow == null || sceneManager == null) { SetError("메인 전투 씬 연결이 없습니다."); ReturnToSetup(); return; }
            inBattle = true; loading = false;
            flow.SimulationTurnFilter = freeMode && turnMode != TurnMode.InOrder
                ? new Predicate<BattleCharactor>(u => u.IsPlayer == (turnMode == TurnMode.HeroesOnly)) : null;
            if (ManualEnemyMode)
            {
                flow.SimulationTurnFilter = u => !u.IsPlayer && (selectedEnemy == null || u == selectedEnemy);
                flow.SimulationEnemyTurnOverride = RunManualEnemyTurn;
            }
            flow.OnTurnStarted += OnTurnStarted;
            flow.OnTurnResolved += OnTurnResolved;
            flow.OnParticipantRegistered += Protect;
            if (resultPanel != null) resultPanel.OnAccepted += ReturnToSetup;
            UpdateBattleStatus();
        }

        private void OnTurnStarted(int round, BattleCharactor actor)
        {
            // 원본 무기 클립의 빈 레거시 이벤트는 YT 씬에서만 수신합니다.
            foreach (var unit in flow.Participants)
            {
                var animator = unit != null && unit.Anim != null ? unit.Anim.Animator : null;
                if (animator != null && animator.runtimeAnimatorController != null &&
                    animator.GetComponent<YT.Preview.YtPreviewAnimationEvents>() == null &&
                    animator.runtimeAnimatorController.animationClips.Any(c => c.events.Any(e => e.functionName == "NewEvent")))
                    animator.gameObject.AddComponent<YT.Preview.YtPreviewAnimationEvents>();
            }
            if (freeMode)
            {
                foreach (var unit in flow.Participants) Protect(unit);
                if (refill && actor != null) actor.SetInfluence(actor.MaxInfluence);
            }
            UpdateBattleStatus();
        }
        private void OnTurnResolved(TurnResolutionContext context) { CompletedTurns++; UpdateBattleStatus(); }
        private void Protect(BattleCharactor unit)
        {
            if (freeMode && unit != null && !unit.IsDead && unit.MaxHp > 0 && !protection.ContainsKey(unit))
                protection.Add(unit, unit.AddMinimumHpConstraint(1f, this));
            if (ManualEnemyMode && selectedEnemy != null && unit != null && !unit.IsPlayer && !unit.IsDead && !manualEnemies.Contains(unit))
            {
                manualEnemies.Add(unit);
                manualEnemy.AddOptions(new List<string> { unit.DisplayName + " · " + manualEnemies.Count });
            }
        }
        private void RestoreAll()
        {
            if (!freeMode || flow == null || (flow.IsActionInProgress && !(ManualEnemyMode && waitingForEnemyInput && !enemyCasting))) return;
            foreach (var unit in flow.Participants.Where(u => u != null && !u.IsDead))
            { unit.SetHp(unit.MaxHp); unit.SetInfluence(unit.MaxInfluence); }
        }
        private void UpdateBattleStatus()
        {
            if (battleStatus != null) battleStatus.text = (freeMode ? "프리 · " + new[] { "정상 순서", "영웅만", "적만" }[(int)turnMode] : "배틀 모드") +
                "  /  " + (flow != null ? flow.RoundIndex : 0) + " 라운드\n" + selectedLabel;
            if (toolbar != null) toolbar.transform.Find("회복").GetComponent<UnityEngine.UI.Button>().interactable = freeMode;
        }
        private void SetError(string error) { LastError = error; if (status != null) status.text = error; Debug.LogWarning("[YT 모의전] " + error); }

        public void ReturnToSetup()
        {
            if (!inBattle && !loading) return;
            if (sceneManager != null) sceneManager.StopAllCoroutines();
            if (flow != null) flow.StopBattleLoop();
            Time.timeScale = 1f;
            if (CombatContext.Instance != null && CombatContext.Instance.IsSimulation) CombatContext.Instance.ClearSimulation();
            Detach();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Destroy(gameObject);
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene("OutpostSimulationScene");
#endif
        }
        private void Detach()
        {
            if (flow != null)
            {
                flow.SimulationTurnFilter = null;
                flow.SimulationEnemyTurnOverride = null;
                flow.OnTurnStarted -= OnTurnStarted; flow.OnTurnResolved -= OnTurnResolved; flow.OnParticipantRegistered -= Protect;
            }
            if (resultPanel != null) resultPanel.OnAccepted -= ReturnToSetup;
            foreach (var handle in protection.Values) handle.Dispose();
            protection.Clear();
        }

        private void BuildEnemyControls(Transform parent)
        {
            enemyControls = Box(parent, "ManualEnemyControls", 30, 135, 510, 415, new Color(.035f, .05f, .08f, .97f));
            Label(enemyControls.transform, "적 스킬 직접 재생", 18, 10, 470, 40, 25);
            Label(enemyControls.transform, "행동할 적", 18, 65, 110, 40, 18);
            manualEnemy = Select(enemyControls.transform, 130, 65, 360, new List<string>());
            Label(enemyControls.transform, "스킬", 18, 120, 110, 40, 18);
            manualSkill = Select(enemyControls.transform, 130, 120, 360, new List<string>());
            Label(enemyControls.transform, "대상", 18, 175, 110, 40, 18);
            manualTarget = Select(enemyControls.transform, 130, 175, 360, new List<string>());
            manualDescription = Label(enemyControls.transform, "", 18, 230, 470, 88, 17);
            manualMessage = Label(enemyControls.transform, "스킬을 고른 뒤 재생을 누르세요.", 18, 322, 470, 32, 17);
            Button(enemyControls.transform, "선택 스킬 재생", 18, 363, 470, RequestEnemyCast);
            manualPlay = enemyControls.transform.Find("선택 스킬 재생").GetComponent<UnityEngine.UI.Button>();
            manualEnemy.onValueChanged.AddListener(index =>
            {
                if (enemyCasting || index >= manualEnemies.Count) return;
                selectedEnemy = manualEnemies[index];
                enemyCastRequested = false;
                PopulateManualSkills();
                manualPlay.interactable = false;
                manualMessage.text = "선택한 적의 턴으로 전환 중…";
            });
            manualSkill.onValueChanged.AddListener(_ => PopulateManualTargets());
            enemyControls.SetActive(false);
        }

        private void PopulateManualSkills()
        {
            manualSkills = selectedEnemy != null ? selectedEnemy.availableSkills.Where(s => s != null)
                .GroupBy(s => s.skillKey ?? s.skillIndex.ToString()).Select(g => g.First()).OrderBy(s => s.slot).ThenBy(s => s.skillIndex).ToList()
                : new List<SkillData>();
            manualSkill.ClearOptions();
            manualSkill.AddOptions(manualSkills.Count > 0 ? manualSkills.Select(s => s.skillName).ToList()
                : new List<string> { "보유 스킬 없음" });
            manualSkill.SetValueWithoutNotify(0); manualSkill.RefreshShownValue();
            PopulateManualTargets();
        }

        private void PopulateManualTargets()
        {
            var skill = manualSkill.value < manualSkills.Count ? manualSkills[manualSkill.value] : null;
            manualTargets = skill == null ? new List<BattleCharactor>() : GetManualTargets(selectedEnemy, skill);
            manualTarget.ClearOptions();
            manualTarget.AddOptions(manualTargets.Count > 0 ? manualTargets.Select((u, i) => u.DisplayName + " · " + (i + 1)).ToList()
                : new List<string> { "선택 가능한 대상 없음" });
            manualTarget.SetValueWithoutNotify(0); manualTarget.RefreshShownValue();
            manualDescription.text = skill != null ? skill.description : "";
            manualMessage.text = manualTargets.Count > 0 ? "재생 후에는 다음 입력을 기다립니다." : "이 스킬을 사용할 수 있는 대상이 없습니다.";
            manualPlay.interactable = !enemyCasting && waitingForEnemyInput && flow.CurrentUnit == selectedEnemy && manualTargets.Count > 0;
        }

        private List<BattleCharactor> GetManualTargets(BattleCharactor actor, SkillData skill)
        {
            // 방패병 대신 맞기는 기존 YT 프리뷰와 같은 아군 전열 지정 규칙을 사용합니다.
            if (skill.skillIndex == 200032)
                return flow.Participants.Where(u => u != null && u != actor && !u.IsDead && !u.IsIncapacitated &&
                    u.IsPlayer == actor.IsPlayer && TargetingHelper.IsUnitInFixedFrontRow(u)).ToList();
            return TargetingHelper.GetValidTargetsForSkillData(actor, skill);
        }

        private void RequestEnemyCast()
        {
            if (!waitingForEnemyInput || enemyCasting || selectedEnemy == null || flow.CurrentUnit != selectedEnemy ||
                flow.IsTurnPresentationPending || manualSkill.value >= manualSkills.Count || manualTarget.value >= manualTargets.Count) return;
            enemyCastRequested = true;
        }

        private IEnumerator RunManualEnemyTurn(BattleCharactor actor)
        {
            if (selectedEnemy == null)
            {
                manualEnemies.AddRange(flow.Participants.Where(u => u != null && !u.IsPlayer && !u.IsDead));
                selectedEnemy = actor;
                manualEnemy.ClearOptions();
                manualEnemy.AddOptions(manualEnemies.Select((u, i) => u.DisplayName + " · " + (i + 1)).ToList());
                manualEnemy.SetValueWithoutNotify(manualEnemies.IndexOf(actor)); manualEnemy.RefreshShownValue();
                PopulateManualSkills();
            }
            enemyControls.SetActive(true);
            if (actor != selectedEnemy) yield break;
            waitingForEnemyInput = true;
            manualPlay.interactable = manualTargets.Count > 0;
            enemyCastRequested = false;
            while (!enemyCastRequested && selectedEnemy == actor && !actor.IsDead && !flow.IsBattleEnded) yield return null;
            waitingForEnemyInput = false;
            if (!enemyCastRequested || selectedEnemy != actor || actor.IsDead || flow.IsBattleEnded) yield break;
            enemyCastRequested = false;
            var skill = manualSkills[manualSkill.value];
            var target = manualTargets[manualTarget.value];
            if (!GetManualTargets(actor, skill).Contains(target)) { PopulateManualTargets(); yield break; }
            enemyCasting = true;
            manualEnemy.interactable = manualSkill.interactable = manualTarget.interactable = manualPlay.interactable = false;
            manualMessage.text = skill.skillName + " 재생 중…";
            LastEnemyCastSucceeded = false;
            try
            {
                yield return flow.BattleManager.ExecuteGridSkill(actor, target, skill, success => LastEnemyCastSucceeded = success);
                if (LastEnemyCastSucceeded) CompletedEnemyCasts++;
            }
            finally
            {
                enemyCasting = false;
                manualEnemy.interactable = manualSkill.interactable = manualTarget.interactable = true;
                manualPlay.interactable = false;
                manualMessage.text = LastEnemyCastSucceeded ? "재생 완료 · 같은 스킬을 다시 재생할 수 있습니다." : "실행 실패 · 스킬과 대상을 확인하세요.";
            }
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Detach();
            Application.runInBackground = oldBackground > 0;
            if (font != null) Destroy(font);
        }

        private RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height); return rt;
        }
        private GameObject Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        { var rt = Rect(parent, name, x, y, w, h); rt.gameObject.AddComponent<UnityEngine.UI.Image>().color = color; return rt.gameObject; }
        private Text Label(Transform parent, string text, float x, float y, float w, float h, int size = 20)
        {
            var rt = Rect(parent, "Text", x, y, w, h); var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = new Color(.91f, .94f, 1f); t.text = text; t.raycastTarget = false;
            t.alignment = TextAnchor.MiddleLeft; return t;
        }
        private void Button(Transform parent, string title, float x, float y, float width, Action action)
        {
            var go = Box(parent, title, x, y, width, 44, new Color(.12f, .3f, .48f));
            var b = go.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = go.GetComponent<UnityEngine.UI.Image>();
            b.onClick.AddListener(() => action()); Label(go.transform, title, 12, 0, width - 24, 44, 18).alignment = TextAnchor.MiddleCenter;
        }
        private InputField Field(Transform parent, string value, float x, float y, float width)
        {
            var go = Box(parent, "Input", x, y, width, 44, new Color(.14f, .18f, .24f));
            var input = go.AddComponent<InputField>(); input.textComponent = Label(go.transform, "", 12, 0, width - 24, 44);
            input.contentType = InputField.ContentType.IntegerNumber; input.text = value; return input;
        }
        private Toggle Check(Transform parent, string title, float x, float y, bool value)
        {
            var go = Rect(parent, "Toggle", x, y, 300, 44).gameObject;
            var bg = Box(go.transform, "Box", 0, 8, 28, 28, new Color(.22f, .28f, .36f));
            var mark = Box(bg.transform, "Mark", 5, 5, 18, 18, new Color(.2f, .8f, .8f));
            var toggle = go.AddComponent<Toggle>(); toggle.targetGraphic = bg.GetComponent<UnityEngine.UI.Image>();
            toggle.graphic = mark.GetComponent<UnityEngine.UI.Image>(); toggle.isOn = value;
            Label(go.transform, title, 40, 0, 245, 44, 18); return toggle;
        }
        private Dropdown Select(Transform parent, float x, float y, float width, List<string> options)
        {
            var go = Box(parent, "Dropdown", x, y, width, 44, new Color(.14f, .18f, .24f));
            var dropdown = go.AddComponent<Dropdown>(); dropdown.targetGraphic = go.GetComponent<UnityEngine.UI.Image>();
            dropdown.captionText = Label(go.transform, "", 12, 0, width - 50, 44, 18);
            Label(go.transform, "▾", width - 32, 0, 25, 44, 20);
            var template = Box(go.transform, "Template", 0, 44, width, 240, new Color(.1f, .14f, .2f));
            var viewport = Box(template.transform, "Viewport", 0, 0, width, 240, Color.white);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect(viewport.transform, "Content", 0, 0, width, 44);
            var item = Box(content, "Item", 0, 0, width, 44, new Color(.18f, .25f, .34f));
            var toggle = item.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<UnityEngine.UI.Image>();
            var mark = Box(item.transform, "Selected", 0, 0, 5, 44, new Color(.2f, .8f, .8f)); toggle.graphic = mark.GetComponent<UnityEngine.UI.Image>();
            dropdown.itemText = Label(item.transform, "", 12, 0, width - 24, 44, 18);
            var scroll = template.AddComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            dropdown.template = (RectTransform)template.transform; dropdown.AddOptions(options); template.SetActive(false); return dropdown;
        }
    }
}
