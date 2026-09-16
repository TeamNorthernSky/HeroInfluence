using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class CombatEventBattleUnitData
{
    public string UnitKey;
    public int Slot;
    public int Level;
    public string EnemyName;
    public string EnemyConcept;
    public int MaxHp;
    public int Atk;
    public int Def;
    public float CriticalRate;
    public float CounterRate;
    public float ReduceRate;
    public int Speed;
    public string UnitAI;
    public int ExperiencePoint;
    public string PrefabResourcePath;
    public List<SkillData> Skills = new List<SkillData>();

    public CombatEventBattleUnitData() { }

    // DH 이벤트 전투 전용 임시 유닛 데이터.
    // Event battles keep a lightweight unit DTO until the ASB battle scene reads every enemy from templates directly.
    public CombatEventBattleUnitData(string unitKey, int slot, int level, DHEventBattleUnitTemplate source)
    {
        UnitKey = unitKey;
        Slot = slot;
        Level = level;

        if (source == null)
            return;

        EnemyName = source.EnemyName;
        EnemyConcept = source.EnemyConcept;
        MaxHp = source.BaseMaxHp;
        Atk = source.BaseAtk;
        Def = source.BaseDef;
        CriticalRate = source.CriticalRate;
        CounterRate = source.CounterRate;
        ReduceRate = source.ReduceRate;
        Speed = source.Speed;
        UnitAI = source.UnitAI;
        ExperiencePoint = source.ExperiencePoint;
    }
}

[Serializable]
public class CombatEventBattleData
{
    public int ZoneId;
    // 이벤트 전투 테이블의 전투 키(Start_BE###). 일반 EnemyGroupKey와 다른 테이블을 가리킨다.
    public string BattleKey;
    // 전투 종료 후 DH 채팅 흐름이 재개할 Chat_ID. 0이면 별도 재개 채팅 없음.
    public int ResumeChatId;
    // 이동형 적 이벤트 전투에서 원본 필드 적을 찾기 위한 MapProgress placement key.
    public string SourceEnemyPlacementKey;
    // 메인이벤트 전투에서 승리/패배 후 원본 이벤트 오브젝트 처리에 사용하는 EventKey.
    public string SourceMainEventKey;
    public int EnemyLevel;
    // 전투씬이 돌려줘야 하는 숫자 결과값. 예: HostageInjuredCount.
    public List<DHEventNumericState> NumericResults = new List<DHEventNumericState>();

    // ASB 공통 처리에서 "적 그룹 키" 이름으로 읽어야 할 경우를 위한 별칭.
    // 이벤트 전투에서는 BattleKey가 곧 전투 그룹 키 역할을 한다.
    public CombatEventBattleData() { }

    public CombatEventBattleData(
        int zoneId,
        string battleKey,
        int resumeChatId,
        int enemyLevel)
    {
        ZoneId = zoneId;
        BattleKey = battleKey;
        ResumeChatId = resumeChatId;
        EnemyLevel = enemyLevel;
    }

    public void SetNumericResult(string key, float value)
    {
        string normalizedKey = DHEventStateRepository.NormalizeKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return;

        for (int i = 0; i < NumericResults.Count; i++)
        {
            DHEventNumericState state = NumericResults[i];
            if (state == null)
                continue;

            if (!string.Equals(DHEventStateRepository.NormalizeKey(state.key), normalizedKey, StringComparison.Ordinal))
                continue;

            state.key = normalizedKey;
            state.value = value;
            return;
        }

        NumericResults.Add(new DHEventNumericState(normalizedKey, value));
    }

    public bool TryGetNumericResult(string key, out float value)
    {
        value = 0f;
        string normalizedKey = DHEventStateRepository.NormalizeKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return false;

        for (int i = 0; i < NumericResults.Count; i++)
        {
            DHEventNumericState state = NumericResults[i];
            if (state == null)
                continue;

            if (!string.Equals(DHEventStateRepository.NormalizeKey(state.key), normalizedKey, StringComparison.Ordinal))
                continue;

            value = state.value;
            return true;
        }

        return false;
    }
}

[DisallowMultipleComponent]
public class CombatContext : MonoBehaviour
{
    public static CombatContext Instance { get; private set; }

    [Header("Current Combat Context")]
    [SerializeField] private CombatPartyPersistentData combatParty;
    [SerializeField] private CombatEnemyPersistentData combatEnemy;
    [SerializeField] private CombatEventBattleData eventBattle;
    [SerializeField, Min(1)] private int enemyLevel = 1;
    [SerializeField] private CombatResult combatResult = CombatResult.None;

    [Header("Simulation Combat")]
    [SerializeField] private BattleEntryMode entryMode = BattleEntryMode.Normal;
    [SerializeField] private string returnSceneName;
    [SerializeField] private string simulationPartyId;
    [SerializeField] private System.Collections.Generic.List<SimulationAllyRuntimeData> simulationAllies =
        new System.Collections.Generic.List<SimulationAllyRuntimeData>();
    [SerializeField] private string tutorialPartyId;
    [SerializeField] private System.Collections.Generic.List<SimulationAllyRuntimeData> tutorialAllies =
        new System.Collections.Generic.List<SimulationAllyRuntimeData>();
    [SerializeField] private bool tutorialCombatActive;

    public CombatPartyPersistentData CombatParty => combatParty;
    // 일반 필드/거점/빌런연합 전투 정보. 이벤트 전투일 때는 null로 비운다.
    public CombatEnemyPersistentData CombatEnemy => combatEnemy;
    // 이벤트 전투 정보. HasEventBattle이 true면 ASB는 CombatEnemy보다 이 데이터를 우선 사용한다.
    public CombatEventBattleData EventBattle => eventBattle;
    public CombatResult Result => combatResult;
    public bool HasEventBattle => eventBattle != null && !string.IsNullOrWhiteSpace(eventBattle.BattleKey);
    public int EnemyLevel => Mathf.Max(1, enemyLevel);
    public BattleEntryMode EntryMode => entryMode;
    public bool IsSimulation => entryMode == BattleEntryMode.Simulation;
    public bool IsTutorial => tutorialCombatActive;
    public string ReturnSceneName => returnSceneName ?? string.Empty;
    public string SimulationPartyId => simulationPartyId ?? string.Empty;
    public string TutorialPartyId => tutorialPartyId ?? string.Empty;
    public System.Collections.Generic.IReadOnlyList<SimulationAllyRuntimeData> SimulationAllies => simulationAllies;
    public System.Collections.Generic.IReadOnlyList<SimulationAllyRuntimeData> TutorialAllies => tutorialAllies;

    public bool TryGetSimulationAlly(int runtimeUnitIndex, out SimulationAllyRuntimeData runtimeData)
    {
        runtimeData = null;
        if (!IsSimulation || simulationAllies == null)
            return false;

        for (int i = 0; i < simulationAllies.Count; i++)
        {
            SimulationAllyRuntimeData candidate = simulationAllies[i];
            if (candidate != null && candidate.RuntimeUnitIndex == runtimeUnitIndex)
            {
                runtimeData = candidate;
                return candidate.UnitData != null;
            }
        }

        return false;
    }

    public bool TryGetTutorialAlly(int runtimeUnitIndex, out SimulationAllyRuntimeData runtimeData)
    {
        runtimeData = null;
        if (!IsTutorial || tutorialAllies == null)
            return false;

        for (int i = 0; i < tutorialAllies.Count; i++)
        {
            SimulationAllyRuntimeData candidate = tutorialAllies[i];
            if (candidate != null && candidate.RuntimeUnitIndex == runtimeUnitIndex)
            {
                runtimeData = candidate;
                return candidate.UnitData != null;
            }
        }

        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool BeginSimulation(
        string partyId,
        System.Collections.Generic.IReadOnlyList<SimulationAllyRuntimeData> allies,
        string enemyGroupKey,
        int nextEnemyLevel,
        string nextReturnSceneName)
    {
        Clear();
        if (string.IsNullOrWhiteSpace(partyId) ||
            string.IsNullOrWhiteSpace(enemyGroupKey) ||
            allies == null ||
            allies.Count == 0)
        {
            return false;
        }

        entryMode = BattleEntryMode.Simulation;
        simulationPartyId = partyId.Trim();
        returnSceneName = string.IsNullOrWhiteSpace(nextReturnSceneName)
            ? "BattleSimulationScene"
            : nextReturnSceneName.Trim();

        var unitIndices = new System.Collections.Generic.List<int>(allies.Count);
        for (int i = 0; i < allies.Count; i++)
        {
            SimulationAllyRuntimeData ally = allies[i];
            if (ally == null || ally.UnitData == null || ally.RuntimeUnitIndex <= 0)
                continue;

            simulationAllies.Add(ally);
            unitIndices.Add(ally.RuntimeUnitIndex);
        }

        if (unitIndices.Count == 0)
        {
            Clear();
            return false;
        }

        RegisterCombatParty(simulationPartyId, unitIndices);
        RegisterCombatEnemy(
            "SIMULATION_ENEMY",
            string.Empty,
            enemyGroupKey.Trim(),
            nextEnemyLevel,
            CombatEnemySourceType.Simulation);
        SetCombatResult(CombatResult.None);
        return true;
    }

    public void ClearSimulation()
    {
        Clear();
    }

    public bool BeginTutorial(
        string partyId,
        System.Collections.Generic.IReadOnlyList<SimulationAllyRuntimeData> allies,
        string enemyGroupKey,
        int nextEnemyLevel,
        string nextReturnSceneName)
    {
        Clear();
        if (string.IsNullOrWhiteSpace(partyId) ||
            string.IsNullOrWhiteSpace(enemyGroupKey) ||
            allies == null ||
            allies.Count == 0)
        {
            return false;
        }

        entryMode = BattleEntryMode.Normal;
        tutorialCombatActive = true;
        tutorialPartyId = partyId.Trim();
        returnSceneName = string.IsNullOrWhiteSpace(nextReturnSceneName)
            ? "TutorialExploreScene"
            : nextReturnSceneName.Trim();

        var unitIndices = new System.Collections.Generic.List<int>(allies.Count);
        for (int i = 0; i < allies.Count; i++)
        {
            SimulationAllyRuntimeData ally = allies[i];
            if (ally == null || ally.UnitData == null || ally.RuntimeUnitIndex <= 0)
                continue;

            tutorialAllies.Add(ally);
            unitIndices.Add(ally.RuntimeUnitIndex);
        }

        if (unitIndices.Count == 0)
        {
            Clear();
            return false;
        }

        RegisterCombatParty(tutorialPartyId, unitIndices);
        RegisterCombatEnemy(
            "TUTORIAL_ENEMY",
            string.Empty,
            enemyGroupKey.Trim(),
            nextEnemyLevel,
            CombatEnemySourceType.Tutorial);
        SetCombatResult(CombatResult.None);
        return true;
    }

    public void ClearTutorial()
    {
        Clear();
    }

    public void RegisterCombatParty(string partyId, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(partyId))
            return;

        if (combatParty == null)
        {
            combatParty = new CombatPartyPersistentData(partyId, unitIndices);
            return;
        }

        combatParty.SetPartyId(partyId);
        combatParty.SetUnitIndices(unitIndices);
    }

    public void RegisterCombatEnemy(
        string enemyId,
        string placementKey,
        string enemyGroupKey,
        int enemyLevel,
        CombatEnemySourceType sourceType)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        // 일반 전투 등록 시 이벤트 전투 데이터는 반드시 비운다.
        // ASB는 HasEventBattle false 상태에서 CombatEnemy를 읽으면 된다.
        eventBattle = null;

        if (combatEnemy == null)
        {
            combatEnemy = new CombatEnemyPersistentData(enemyId, placementKey, enemyGroupKey, sourceType);
            SetEnemyLevel(enemyLevel);
            return;
        }

        combatEnemy.SetEnemyId(enemyId);
        combatEnemy.SetPlacementKey(placementKey);
        combatEnemy.SetEnemyGroupKey(enemyGroupKey);
        combatEnemy.SetSourceType(sourceType);
        SetEnemyLevel(enemyLevel);
    }

    public void RegisterEventBattle(CombatEventBattleData nextEventBattle)
    {
        // 이벤트 전투는 키/존/레벨만 CombatContext에 담고, ASB가 카탈로그에서 유닛과 시나리오를 해석한다.
        combatEnemy = null;
        eventBattle = nextEventBattle;
        SetEnemyLevel(nextEventBattle != null ? nextEventBattle.EnemyLevel : 1);
    }

    public void SetEnemyLevel(int nextEnemyLevel)
    {
        enemyLevel = Mathf.Max(1, nextEnemyLevel);
    }

    public void SetCombatResult(CombatResult nextResult)
    {
        combatResult = nextResult;
    }

    public void Clear()
    {
        combatParty = null;
        combatEnemy = null;
        eventBattle = null;
        enemyLevel = 1;
        combatResult = CombatResult.None;
        entryMode = BattleEntryMode.Normal;
        returnSceneName = string.Empty;
        simulationPartyId = string.Empty;
        tutorialPartyId = string.Empty;
        tutorialCombatActive = false;
        if (simulationAllies == null)
            simulationAllies = new System.Collections.Generic.List<SimulationAllyRuntimeData>();
        else
            simulationAllies.Clear();
        if (tutorialAllies == null)
            tutorialAllies = new System.Collections.Generic.List<SimulationAllyRuntimeData>();
        else
            tutorialAllies.Clear();
    }
}
