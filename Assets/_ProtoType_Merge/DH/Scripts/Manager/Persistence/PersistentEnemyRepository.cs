using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentEnemyRepository : MonoBehaviour
{
    public static PersistentEnemyRepository Instance { get; private set; }
    private const string EnemyIdPrefix = "enemy_";

    [Header("Persistent Enemies")]
    [SerializeField] private int nextUnitIndex = 1;
    [SerializeField] private int nextEnemySequence = 1;
    [SerializeField] private List<EnemyUnitPersistentData> units = new List<EnemyUnitPersistentData>();
    [SerializeField] private List<EnemyPersistentData> enemies = new List<EnemyPersistentData>();
    [Header("Combat Context")]
    [SerializeField] private CombatEnemyPersistentData combatEnemy;

    private readonly Dictionary<int, EnemyUnitPersistentData> unitLookup = new Dictionary<int, EnemyUnitPersistentData>();
    private readonly Dictionary<string, EnemyPersistentData> enemyLookup = new Dictionary<string, EnemyPersistentData>();

    public IReadOnlyList<EnemyUnitPersistentData> Units => units;
    public IReadOnlyList<EnemyPersistentData> Enemies => enemies;
    public CombatEnemyPersistentData CombatEnemy => combatEnemy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats)
    {
        int unitIndex = Mathf.Max(1, nextUnitIndex);
        nextUnitIndex = unitIndex + 1;

        EnemyUnitPersistentData newData = new EnemyUnitPersistentData(unitIndex, unitTemplateKey, level, baseStats);
        units.Add(newData);
        unitLookup[unitIndex] = newData;
        return unitIndex;
    }

    public string CreateEnemy(IReadOnlyList<int> unitIndices)
    {
        int sequence = Mathf.Max(1, nextEnemySequence);
        nextEnemySequence = sequence + 1;
        string enemyId = FormatEnemyId(sequence);

        var newData = new EnemyPersistentData(enemyId, unitIndices);
        enemies.Add(newData);
        enemyLookup[enemyId] = newData;
        return enemyId;
    }

    public void RegisterOrUpdateEnemy(string enemyId, IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        if (enemyLookup.TryGetValue(enemyId, out EnemyPersistentData existingData))
        {
            existingData.SetUnitIndices(unitIndices);
            return;
        }

        EnemyPersistentData newData = new EnemyPersistentData(enemyId, unitIndices);
        enemies.Add(newData);
        enemyLookup[enemyId] = newData;
        UpdateNextEnemySequence(enemyId);
    }

    public bool ContainsUnit(int unitIndex)
    {
        return unitIndex > 0 && unitLookup.ContainsKey(unitIndex);
    }

    public bool TryGetUnit(int unitIndex, out EnemyUnitPersistentData data)
    {
        if (unitIndex <= 0)
        {
            data = null;
            return false;
        }

        return unitLookup.TryGetValue(unitIndex, out data);
    }

    public bool RemoveUnit(int unitIndex)
    {
        if (unitIndex <= 0 || !unitLookup.TryGetValue(unitIndex, out EnemyUnitPersistentData data))
            return false;

        unitLookup.Remove(unitIndex);
        units.Remove(data);
        return true;
    }

    public bool ContainsEnemy(string enemyId)
    {
        return !string.IsNullOrWhiteSpace(enemyId) && enemyLookup.ContainsKey(enemyId);
    }

    public bool TryGetEnemy(string enemyId, out EnemyPersistentData data)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            data = null;
            return false;
        }

        return enemyLookup.TryGetValue(enemyId, out data);
    }

    public bool RemoveEnemy(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId) || !enemyLookup.TryGetValue(enemyId, out EnemyPersistentData data))
            return false;

        enemyLookup.Remove(enemyId);
        enemies.Remove(data);
        return true;
    }

    public void RegisterCombatEnemy(string enemyId, IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        if (combatEnemy == null)
        {
            combatEnemy = new CombatEnemyPersistentData(enemyId, unitIndices);
            return;
        }

        combatEnemy.SetEnemyId(enemyId);
        combatEnemy.SetUnitIndices(unitIndices);
    }

    public void ClearCombatEnemy()
    {
        combatEnemy = null;
    }

    public void ClearAllEnemies()
    {
        units.Clear();
        unitLookup.Clear();
        enemies.Clear();
        enemyLookup.Clear();
        combatEnemy = null;
        nextUnitIndex = 1;
        nextEnemySequence = 1;
    }

    private void RebuildLookup()
    {
        unitLookup.Clear();
        enemyLookup.Clear();
        int highestUnitIndex = 0;
        int highestEnemySequence = 0;

        for (int i = 0; i < units.Count; i++)
        {
            EnemyUnitPersistentData data = units[i];
            if (data == null)
                continue;

            int unitIndex = data.UnitIndex;
            if (unitIndex <= 0)
                continue;

            if (unitLookup.ContainsKey(unitIndex))
            {
                Debug.LogWarning($"PersistentEnemyRepository has duplicate enemy unit index '{unitIndex}'.", this);
                continue;
            }

            unitLookup.Add(unitIndex, data);
            if (unitIndex > highestUnitIndex)
                highestUnitIndex = unitIndex;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyPersistentData data = enemies[i];
            if (data == null || string.IsNullOrWhiteSpace(data.EnemyId))
                continue;

            if (enemyLookup.ContainsKey(data.EnemyId))
            {
                Debug.LogWarning($"PersistentEnemyRepository has duplicate enemyId '{data.EnemyId}'.", this);
                continue;
            }

            enemyLookup.Add(data.EnemyId, data);
            if (TryParseEnemySequence(data.EnemyId, out int sequence) && sequence > highestEnemySequence)
                highestEnemySequence = sequence;
        }

        if (nextUnitIndex <= highestUnitIndex)
            nextUnitIndex = highestUnitIndex + 1;

        if (nextEnemySequence <= highestEnemySequence)
            nextEnemySequence = highestEnemySequence + 1;
    }

    private static string FormatEnemyId(int sequence)
    {
        return $"{EnemyIdPrefix}{Mathf.Max(1, sequence):000}";
    }

    private void UpdateNextEnemySequence(string enemyId)
    {
        if (TryParseEnemySequence(enemyId, out int sequence) && nextEnemySequence <= sequence)
            nextEnemySequence = sequence + 1;
    }

    private static bool TryParseEnemySequence(string enemyId, out int sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(enemyId) ||
            !enemyId.StartsWith(EnemyIdPrefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(enemyId.Substring(EnemyIdPrefix.Length), out sequence) && sequence > 0;
    }
}
