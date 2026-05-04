using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentEnemyRepository : MonoBehaviour
{
    public static PersistentEnemyRepository Instance { get; private set; }

    [Header("Persistent Enemies")]
    [SerializeField] private int nextUnitIndex = 1;
    [SerializeField] private int nextEnemyId = 1;
    [SerializeField] private List<EnemyUnitPersistentData> units = new List<EnemyUnitPersistentData>();
    [SerializeField] private List<EnemyPersistentData> enemies = new List<EnemyPersistentData>();

    private readonly Dictionary<int, EnemyUnitPersistentData> unitLookup = new Dictionary<int, EnemyUnitPersistentData>();
    private readonly Dictionary<int, EnemyPersistentData> enemyLookup = new Dictionary<int, EnemyPersistentData>();

    public IReadOnlyList<EnemyUnitPersistentData> Units => units;
    public IReadOnlyList<EnemyPersistentData> Enemies => enemies;

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

    public int CreateEnemy(IReadOnlyList<int> unitIndices)
    {
        int enemyId = Mathf.Max(1, nextEnemyId);
        nextEnemyId = enemyId + 1;

        var newData = new EnemyPersistentData(enemyId, unitIndices);
        enemies.Add(newData);
        enemyLookup[enemyId] = newData;
        return enemyId;
    }

    public void RegisterOrUpdateEnemy(int enemyId, IReadOnlyList<int> unitIndices)
    {
        if (enemyId <= 0)
            return;

        if (enemyLookup.TryGetValue(enemyId, out EnemyPersistentData existingData))
        {
            existingData.SetUnitIndices(unitIndices);
            return;
        }

        EnemyPersistentData newData = new EnemyPersistentData(enemyId, unitIndices);
        enemies.Add(newData);
        enemyLookup[enemyId] = newData;
        if (nextEnemyId <= enemyId)
            nextEnemyId = enemyId + 1;
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

    public bool ContainsEnemy(int enemyId)
    {
        return enemyId > 0 && enemyLookup.ContainsKey(enemyId);
    }

    public bool TryGetEnemy(int enemyId, out EnemyPersistentData data)
    {
        if (enemyId <= 0)
        {
            data = null;
            return false;
        }

        return enemyLookup.TryGetValue(enemyId, out data);
    }

    public bool RemoveEnemy(int enemyId)
    {
        if (enemyId <= 0 || !enemyLookup.TryGetValue(enemyId, out EnemyPersistentData data))
            return false;

        enemyLookup.Remove(enemyId);
        enemies.Remove(data);
        return true;
    }

    public void ClearAllEnemies()
    {
        units.Clear();
        unitLookup.Clear();
        enemies.Clear();
        enemyLookup.Clear();
        nextUnitIndex = 1;
        nextEnemyId = 1;
    }

    private void RebuildLookup()
    {
        unitLookup.Clear();
        enemyLookup.Clear();
        int highestUnitIndex = 0;
        int highestEnemyId = 0;

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
            if (data == null || data.EnemyId <= 0)
                continue;

            if (enemyLookup.ContainsKey(data.EnemyId))
            {
                Debug.LogWarning($"PersistentEnemyRepository has duplicate enemyId '{data.EnemyId}'.", this);
                continue;
            }

            enemyLookup.Add(data.EnemyId, data);
            if (data.EnemyId > highestEnemyId)
                highestEnemyId = data.EnemyId;
        }

        if (nextUnitIndex <= highestUnitIndex)
            nextUnitIndex = highestUnitIndex + 1;

        if (nextEnemyId <= highestEnemyId)
            nextEnemyId = highestEnemyId + 1;
    }
}
