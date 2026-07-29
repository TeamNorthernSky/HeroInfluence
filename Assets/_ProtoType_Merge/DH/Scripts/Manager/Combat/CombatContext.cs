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

    public CombatEventBattleUnitData(string unitKey, int slot, int level, EnemyUnit1SectorData source)
    {
        UnitKey = unitKey;
        Slot = slot;
        Level = level;

        if (source == null)
            return;

        EnemyName = source.EnemyName;
        EnemyConcept = source.EnemyConcept;
        MaxHp = source.UnitMaxHP;
        Atk = source.UnitATK;
        Def = source.UnitDEF;
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
    public string BattleKey;
    public int ResumeChatId;
    public int EnemyLevel;
    public List<CombatEventBattleUnitData> EnemyUnits = new List<CombatEventBattleUnitData>();
    public List<DHEventNumericState> NumericResults = new List<DHEventNumericState>();
    public BattleScenarioConfig Scenario;

    public CombatEventBattleData() { }

    public CombatEventBattleData(
        int zoneId,
        string battleKey,
        int resumeChatId,
        int enemyLevel,
        IReadOnlyList<CombatEventBattleUnitData> enemyUnits)
    {
        ZoneId = zoneId;
        BattleKey = battleKey;
        ResumeChatId = resumeChatId;
        EnemyLevel = enemyLevel;

        EnemyUnits.Clear();
        if (enemyUnits == null)
            return;

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            CombatEventBattleUnitData unit = enemyUnits[i];
            if (unit != null)
                EnemyUnits.Add(unit);
        }
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
    [SerializeField] private CombatResult combatResult = CombatResult.None;

    public CombatPartyPersistentData CombatParty => combatParty;
    public CombatEnemyPersistentData CombatEnemy => combatEnemy;
    public CombatEventBattleData EventBattle => eventBattle;
    public CombatResult Result => combatResult;
    public bool HasEventBattle => eventBattle != null && !string.IsNullOrWhiteSpace(eventBattle.BattleKey);

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

    public void RegisterCombatEnemy(string enemyId, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        RegisterCombatEnemy(enemyId, string.Empty, unitIndices);
    }

    public void RegisterCombatEnemy(string enemyId, string placementKey, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        if (combatEnemy == null)
        {
            combatEnemy = new CombatEnemyPersistentData(enemyId, placementKey, unitIndices);
            return;
        }

        combatEnemy.SetEnemyId(enemyId);
        combatEnemy.SetPlacementKey(placementKey);
        combatEnemy.SetUnitIndices(unitIndices);
    }

    public void RegisterEventBattle(CombatEventBattleData nextEventBattle)
    {
        eventBattle = nextEventBattle;
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
        combatResult = CombatResult.None;
    }
}
