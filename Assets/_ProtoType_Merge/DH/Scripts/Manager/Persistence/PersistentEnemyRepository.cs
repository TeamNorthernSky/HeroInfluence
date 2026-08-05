using System.Collections.Generic;
using System.IO;
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

    // [KJ 260703] 저장 기능(GameSaveService)용 읽기 노출
    public int NextUnitIndex => nextUnitIndex;

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
        StatBlock ingameStats = CalculateEnemyIngameStats(unitTemplateKey, baseStats, level);
        return CreateUnit(unitTemplateKey, level, baseStats, ingameStats, ingameStats.HP);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock ingameStats, float currentHp, float currentInfluence = -1f)
    {
        int unitIndex = Mathf.Max(1, nextUnitIndex);
        nextUnitIndex = unitIndex + 1;

        EnemyUnitPersistentData newData = new EnemyUnitPersistentData(unitIndex, unitTemplateKey, level, baseStats, ingameStats, currentHp, currentInfluence);
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

    public bool UpdateUnitRuntimeState(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock ingameStats, float currentHp, float currentInfluence = -1f, bool? isIncapacitated = null)
    {
        if (unitIndex <= 0 || !unitLookup.TryGetValue(unitIndex, out EnemyUnitPersistentData data))
            return false;

        data.ApplyRuntimeState(unitTemplateKey, level, baseStats, ingameStats, currentHp, currentInfluence, isIncapacitated);
        return true;
    }

    public bool ApplyLevelUp(int unitIndex, int amount = 1)
    {
        if (unitIndex <= 0 || !unitLookup.TryGetValue(unitIndex, out EnemyUnitPersistentData data))
            return false;

        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return true;

        return ApplyEnemyLevel(data, data.Level + safeAmount, true);
    }

    public bool SetUnitLevel(int unitIndex, int nextLevel)
    {
        if (unitIndex <= 0 || !unitLookup.TryGetValue(unitIndex, out EnemyUnitPersistentData data))
            return false;

        return ApplyEnemyLevel(data, nextLevel, false);
    }

    private static bool ApplyEnemyLevel(EnemyUnitPersistentData data, int nextLevel, bool healByMaxHpDelta)
    {
        if (data == null)
            return false;

        int safeNextLevel = Mathf.Max(1, nextLevel);
        float previousMaxHp = Mathf.Max(0f, data.IngameStats.HP);
        StatBlock nextIngameStats = CalculateEnemyIngameStats(data.UnitTemplateKey, data.BaseStats, safeNextLevel);
        float nextMaxHp = Mathf.Max(0f, nextIngameStats.HP);
        float nextCurrentHp = data.CurrentHp;

        if (healByMaxHpDelta)
        {
            float maxHpDelta = Mathf.Max(0f, nextMaxHp - previousMaxHp);
            nextCurrentHp += maxHpDelta;
        }

        nextCurrentHp = Mathf.Clamp(nextCurrentHp, 0f, nextMaxHp);
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            safeNextLevel,
            data.BaseStats,
            nextIngameStats,
            nextCurrentHp,
            data.CurrentInfluence,
            data.IsIncapacitated);
        return true;
    }

    private static StatBlock CalculateEnemyIngameStats(string unitTemplateKey, StatBlock baseStats, int level)
    {
        StatBlock levelupStats = ResolveEnemyLevelupStats(unitTemplateKey);
        return UnitStatCalculator.CalculateLevelAdjustedBaseStats(baseStats, levelupStats, level);
    }

    private static StatBlock ResolveEnemyLevelupStats(string unitTemplateKey)
    {
        if (string.IsNullOrWhiteSpace(unitTemplateKey))
            return default;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && catalog.TryGetEnemyUnitTemplate(unitTemplateKey, out DHEnemyUnitTemplate template) && template != null)
            return template.LevelupStats;

        return default;
    }
    public bool SetIncapacitated(int unitIndex, bool isIncapacitated)
    {
        if (unitIndex <= 0 || !unitLookup.TryGetValue(unitIndex, out EnemyUnitPersistentData data))
            return false;

        float nextHp = isIncapacitated ? 0f : data.CurrentHp;
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.IngameStats,
            nextHp,
            data.CurrentInfluence,
            isIncapacitated);
        return true;
    }

    public void RestoreFromSave(int restoredNextUnitIndex, IReadOnlyList<EnemyUnitPersistentDataDiskRow> savedUnits)
    {
        units.Clear();
        unitLookup.Clear();

        if (savedUnits != null)
        {
            for (int i = 0; i < savedUnits.Count; i++)
            {
                EnemyUnitPersistentDataDiskRow row = savedUnits[i];
                if (row == null)
                    continue;

                EnemyUnitPersistentData data = row.ToEnemyUnitPersistentData();
                if (data == null || data.UnitIndex <= 0 || unitLookup.ContainsKey(data.UnitIndex))
                    continue;

                units.Add(data);
                unitLookup.Add(data.UnitIndex, data);
            }
        }

        nextUnitIndex = Mathf.Max(1, restoredNextUnitIndex);
        RebuildLookup();
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

    private const string DiskFileName = "persistent_enemy_repo.json";

    [System.Serializable]
    private class EnemyRepositoryDiskPayload
    {
        public int nextUnitIndex;
        public EnemyUnitPersistentDataDiskRow[] units;
    }

    /// <summary>메모리상 적 유닛 목록을 Application.persistentDataPath JSON으로 저장합니다.</summary>
    public void SaveRuntimeStateToDisk()
    {
        try
        {
            EnemyUnitPersistentDataDiskRow[] rows;
            if (units == null || units.Count == 0)
                rows = System.Array.Empty<EnemyUnitPersistentDataDiskRow>();
            else
            {
                rows = new EnemyUnitPersistentDataDiskRow[units.Count];
                for (int i = 0; i < units.Count; i++)
                    rows[i] = EnemyUnitPersistentDataDiskRow.From(units[i]);
            }

            var payload = new EnemyRepositoryDiskPayload
            {
                nextUnitIndex = nextUnitIndex,
                units = rows
            };

            string path = Path.Combine(Application.persistentDataPath, DiskFileName);
            File.WriteAllText(path, JsonUtility.ToJson(payload));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PersistentEnemyRepository] SaveRuntimeStateToDisk 실패: {ex.Message}", this);
        }
    }
}
