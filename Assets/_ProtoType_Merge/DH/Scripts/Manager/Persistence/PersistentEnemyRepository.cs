using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentEnemyRepository : MonoBehaviour
{
    public static PersistentEnemyRepository Instance { get; private set; }

    [Header("Persistent Enemies")]
    [SerializeField] private int nextUnitIndex = 1;
    [SerializeField] private List<EnemyUnitPersistentData> units = new List<EnemyUnitPersistentData>();

    private readonly Dictionary<int, EnemyUnitPersistentData> unitLookup = new Dictionary<int, EnemyUnitPersistentData>();

    public IReadOnlyList<EnemyUnitPersistentData> Units => units;

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
        return CreateUnit(unitTemplateKey, level, baseStats, baseStats, baseStats.HP);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock ingameStats, float currentHp)
    {
        int unitIndex = Mathf.Max(1, nextUnitIndex);
        nextUnitIndex = unitIndex + 1;

        EnemyUnitPersistentData newData = new EnemyUnitPersistentData(unitIndex, unitTemplateKey, level, baseStats, ingameStats, currentHp);
        units.Add(newData);
        unitLookup[unitIndex] = newData;
        return unitIndex;
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

    public bool UpdateUnitRuntimeState(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock ingameStats, float currentHp)
    {
        if (unitIndex <= 0 || !unitLookup.TryGetValue(unitIndex, out EnemyUnitPersistentData data))
            return false;

        data.ApplyRuntimeState(unitTemplateKey, level, baseStats, ingameStats, currentHp);
        return true;
    }

    public void ClearAllEnemies()
    {
        units.Clear();
        unitLookup.Clear();
        nextUnitIndex = 1;
    }

    private void RebuildLookup()
    {
        unitLookup.Clear();
        int highestUnitIndex = 0;

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

        if (nextUnitIndex <= highestUnitIndex)
            nextUnitIndex = highestUnitIndex + 1;
    }
}
