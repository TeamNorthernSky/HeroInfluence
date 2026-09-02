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

    // DH ??? ?? ?? ?? ?? ???.
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
    // ??? ?? ???? ?? ?(Start_BE###). ?? EnemyGroupKey? ?? ???? ????.
    public string BattleKey;
    // ?? ?? ? DH ?? ??? ??? Chat_ID. 0?? ?? ?? ?? ??.
    public int ResumeChatId;
    // ??? ? ??? ???? ?? ?? ?? ?? ?? MapProgress placement key.
    public string SourceEnemyPlacementKey;
    // ????? ???? ??/?? ? ?? ??? ???? ??? ???? EventKey.
    public string SourceMainEventKey;
    public int EnemyLevel;
    // ???? ???? ?? ?? ???. ?: HostageInjuredCount.
    public List<DHEventNumericState> NumericResults = new List<DHEventNumericState>();

    // ASB ?? ???? "? ?? ?" ???? ??? ? ??? ?? ??.
    // ??? ????? BattleKey? ? ?? ?? ? ??? ??.
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

    public CombatPartyPersistentData CombatParty => combatParty;
    // ?? ??/??/???? ?? ??. ??? ??? ?? null? ???.
    public CombatEnemyPersistentData CombatEnemy => combatEnemy;
    // ??? ?? ??. HasEventBattle? true? ASB? CombatEnemy?? ? ???? ?? ????.
    public CombatEventBattleData EventBattle => eventBattle;
    public CombatResult Result => combatResult;
    public bool HasEventBattle => eventBattle != null && !string.IsNullOrWhiteSpace(eventBattle.BattleKey);
    public int EnemyLevel => Mathf.Max(1, enemyLevel);
    public BattleEntryMode EntryMode => entryMode;
    public bool IsSimulation => entryMode == BattleEntryMode.Simulation;
    public string ReturnSceneName => returnSceneName ?? string.Empty;
    public string SimulationPartyId => simulationPartyId ?? string.Empty;
    public System.Collections.Generic.IReadOnlyList<SimulationAllyRuntimeData> SimulationAllies => simulationAllies;

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

        // ?? ?? ?? ? ??? ?? ???? ??? ???.
        // ASB? HasEventBattle false ???? CombatEnemy? ??? ??.
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
        // ??? ??? ?/?/??? CombatContext? ??, ASB? ?????? ??? ????? ????.
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
        if (simulationAllies == null)
            simulationAllies = new System.Collections.Generic.List<SimulationAllyRuntimeData>();
        else
            simulationAllies.Clear();
    }
}
