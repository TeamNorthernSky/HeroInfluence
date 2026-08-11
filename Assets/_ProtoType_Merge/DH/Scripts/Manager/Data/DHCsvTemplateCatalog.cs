using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class DHCsvTemplateCatalog : MonoBehaviour
{
    private const float DefaultPlayerBaseInfluence = 100f;

    public static DHCsvTemplateCatalog Instance { get; private set; }

    [Header("SO DataTables (Excel Importer)")]
    [SerializeField] private PlayerUnitDataTable    playerUnitDataTable;
    [SerializeField] private EnemyUnitDataTable enemyUnitInfoDataTable;
    [SerializeField] private PlayerWeaponDataTable  playerWeaponDataTable;
    [SerializeField] private ClassSkillDataTable    classSkillDataTable;
    [SerializeField] private UnitGrowthExpDataTable unitGrowthExpDataTable;
    [SerializeField] private EnemyGroupDataTable    enemyGroupDataTable;

    [Header("Settings")]
    [SerializeField] private bool loadOnAwake      = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private List<UnitData>   cachedPlayerTemplates = new List<UnitData>();
    [SerializeField] private List<EnemyData>  cachedEnemyTemplates  = new List<EnemyData>();
    [SerializeField] private List<WeaponData> cachedWeapons         = new List<WeaponData>();

    private readonly Dictionary<string, UnitData>          playerTemplateLookup = new Dictionary<string, UnitData>();
    private readonly Dictionary<string, DHPlayerUnitTemplate> playerUnitTemplateLookup = new Dictionary<string, DHPlayerUnitTemplate>();
    private readonly Dictionary<string, EnemyData>         enemyTemplateLookup  = new Dictionary<string, EnemyData>();
    private readonly Dictionary<string, DHEnemyUnitTemplate> enemyUnitTemplateLookup = new Dictionary<string, DHEnemyUnitTemplate>();
    private readonly Dictionary<int,    WeaponData>        weaponLookup         = new Dictionary<int, WeaponData>();
    private readonly Dictionary<string, WeaponData>        weaponByKey          = new Dictionary<string, WeaponData>();              // §5-3 string 병렬 조회
    private readonly Dictionary<int,    DHWeaponTemplate>  weaponTemplateLookup = new Dictionary<int, DHWeaponTemplate>();
    private readonly Dictionary<string, DHWeaponTemplate>  weaponTemplateByKey  = new Dictionary<string, DHWeaponTemplate>();         // §5-3 string 병렬 조회
    private readonly Dictionary<int,    SkillData>         skillTemplates       = new Dictionary<int, SkillData>();
    private readonly Dictionary<string, SkillData>         skillTemplatesByKey  = new Dictionary<string, SkillData>();               // §5-3 string 병렬 조회
    private readonly Dictionary<int,    DHClassSkillTemplate> classSkillTemplateLookup = new Dictionary<int, DHClassSkillTemplate>();
    private readonly Dictionary<string, DHClassSkillTemplate> classSkillTemplateByKey  = new Dictionary<string, DHClassSkillTemplate>();  // §5-3 string 병렬 조회
    private readonly List<LevelUpData>                     levelUpTemplates     = new List<LevelUpData>();
    private readonly List<DHUnitGrowthTemplate>            unitGrowthTemplates  = new List<DHUnitGrowthTemplate>();
    private readonly Dictionary<string, DHEnemyGroupTemplate> enemyGroupLookup  = new Dictionary<string, DHEnemyGroupTemplate>();

    // Master caches for level-scaled lookup data.
    private readonly Dictionary<int, List<int>>        classSkillIndexListByClassIndex = new Dictionary<int, List<int>>();
    private readonly Dictionary<int, List<int>>        weaponIndexListByClassIndex = new Dictionary<int, List<int>>();

    // classIndex -> (level -> skillIndex)
    private readonly Dictionary<int, Dictionary<int, int>> skillUnlockByClassIndex
        = new Dictionary<int, Dictionary<int, int>>();

    private static readonly int[] GrowthSkillClassIndices = { 10001, 10002, 10003, 10004 };

    private bool isLoaded;

    public bool IsLoaded => isLoaded;

    // Unity lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (loadOnAwake)
            ReloadTemplates();
    }

    // Public API

    [ContextMenu("Reload Templates")]
    public void ReloadTemplates()
    {
        ClearCache();

        // CSV 로더 경로 제거됨 — SO(Excel Importer) 캐싱 데이터만 사용.
        ReloadFromSOTables();
    }

    public bool TryGetPlayerTemplate(string unitTemplateKey, out UnitData template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey)) { template = null; return false; }

        string key = NormalizeNumericTemplateKey(unitTemplateKey);
        return playerTemplateLookup.TryGetValue(key, out template);
    }

    public bool TryGetPlayerUnitTemplate(string unitTemplateKey, out DHPlayerUnitTemplate template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey)) { template = null; return false; }

        string key = NormalizeNumericTemplateKey(unitTemplateKey);
        return playerUnitTemplateLookup.TryGetValue(key, out template);
    }

    public bool TryGetEnemyTemplate(string unitTemplateKey, out EnemyData template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey)) { template = null; return false; }

        string key = NormalizeNumericTemplateKey(unitTemplateKey);
        return enemyTemplateLookup.TryGetValue(key, out template);
    }

    public bool TryGetEnemyUnitTemplate(string unitTemplateKey, out DHEnemyUnitTemplate template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey)) { template = null; return false; }

        string key = NormalizeNumericTemplateKey(unitTemplateKey);
        return enemyUnitTemplateLookup.TryGetValue(key, out template);
    }

    public bool TryGetWeapon(int weaponIndex, out WeaponData weaponData)
    {
        EnsureLoaded();
        if (weaponIndex <= 0) { weaponData = null; return false; }
        return weaponLookup.TryGetValue(weaponIndex, out weaponData);
    }

    public bool TryGetWeaponTemplate(int weaponIndex, out DHWeaponTemplate template)
    {
        EnsureLoaded();
        if (weaponIndex <= 0) { template = null; return false; }
        return weaponTemplateLookup.TryGetValue(weaponIndex, out template);
    }

    public bool TryGetWeaponStats(int weaponIndex, out EquipmentStatBlock equipmentStats)
    {
        if (TryGetWeaponTemplate(weaponIndex, out DHWeaponTemplate template))
        {
            equipmentStats = EquipmentStatBlock.FromStatBlock(template.GetBonusStatsAtLevel(1));
            return true;
        }
        equipmentStats = default;
        return false;
    }

    public SkillData GetSkillTemplate(int index)
    {
        EnsureLoaded();
        return skillTemplates.TryGetValue(index, out SkillData data) ? data : null;
    }

    public bool TryGetClassSkillTemplate(int skillIndex, out DHClassSkillTemplate template)
    {
        EnsureLoaded();
        if (skillIndex <= 0) { template = null; return false; }
        return classSkillTemplateLookup.TryGetValue(skillIndex, out template);
    }

    // ───────────────────────────────────────────────────────────
    // §5-3 string 키 조회 API (기존 int API와 병렬). 소비부는 점진적으로 이쪽으로 이행.
    // ───────────────────────────────────────────────────────────
    public bool TryGetWeapon(string weaponKey, out WeaponData weaponData)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(weaponKey)) { weaponData = null; return false; }
        return weaponByKey.TryGetValue(weaponKey.Trim(), out weaponData);
    }

    public bool TryGetWeaponTemplate(string weaponKey, out DHWeaponTemplate template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(weaponKey)) { template = null; return false; }
        return weaponTemplateByKey.TryGetValue(weaponKey.Trim(), out template);
    }

    public bool TryGetWeaponStats(string weaponKey, out EquipmentStatBlock equipmentStats)
    {
        if (TryGetWeaponTemplate(weaponKey, out DHWeaponTemplate template))
        {
            equipmentStats = EquipmentStatBlock.FromStatBlock(template.GetBonusStatsAtLevel(1));
            return true;
        }
        equipmentStats = default;
        return false;
    }

    public SkillData GetSkillTemplate(string skillKey)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(skillKey)) return null;
        return skillTemplatesByKey.TryGetValue(skillKey.Trim(), out SkillData data) ? data : null;
    }

    public bool TryGetClassSkillTemplate(string skillKey, out DHClassSkillTemplate template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(skillKey)) { template = null; return false; }
        return classSkillTemplateByKey.TryGetValue(skillKey.Trim(), out template);
    }

    public IReadOnlyList<LevelUpData> GetLevelUpTemplates()
    {
        EnsureLoaded();
        return levelUpTemplates;
    }

    public IReadOnlyList<DHUnitGrowthTemplate> GetUnitGrowthTemplates()
    {
        EnsureLoaded();
        return unitGrowthTemplates;
    }

    // Level-scaled lookup API

    /// <summary>Returns weapon stat bonuses for the given enhancement level.</summary>
    public bool TryGetWeaponBonusAtLevel(int weaponIndex, int level, out StatBlock bonus)
    {
        EnsureLoaded();
        if (!weaponTemplateLookup.TryGetValue(weaponIndex, out DHWeaponTemplate template))
        {
            bonus = default;
            return false;
        }

        bonus = template.GetBonusStatsAtLevel(level);
        return true;
    }

    public bool TryGetWeaponBonusAtLevel(string weaponKey, int level, out StatBlock bonus)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(weaponKey) ||
            !weaponTemplateByKey.TryGetValue(weaponKey.Trim(), out DHWeaponTemplate template))
        {
            bonus = default;
            return false;
        }

        bonus = template.GetBonusStatsAtLevel(level);
        return true;
    }

    /// <summary>Returns weapon skill value for the given enhancement level.</summary>
    public float GetWeaponSkillValueAtLevel(int weaponIndex, int level)
    {
        EnsureLoaded();
        return weaponTemplateLookup.TryGetValue(weaponIndex, out DHWeaponTemplate template)
            ? template.GetSkillValueAtLevel(level)
            : 0f;
    }

    public float GetWeaponSkillValueAtLevel(string weaponKey, int level)
    {
        EnsureLoaded();
        return !string.IsNullOrWhiteSpace(weaponKey) &&
               weaponTemplateByKey.TryGetValue(weaponKey.Trim(), out DHWeaponTemplate template)
            ? template.GetSkillValueAtLevel(level)
            : 0f;
    }

    /// <summary>Returns weapon skill sub-value for the given enhancement level.</summary>
    public float GetWeaponSkillSubValueAtLevel(int weaponIndex, int level)
    {
        EnsureLoaded();
        return weaponTemplateLookup.TryGetValue(weaponIndex, out DHWeaponTemplate template)
            ? template.GetSkillSubValueAtLevel(level)
            : 0f;
    }

    public float GetWeaponSkillSubValueAtLevel(string weaponKey, int level)
    {
        EnsureLoaded();
        return !string.IsNullOrWhiteSpace(weaponKey) &&
               weaponTemplateByKey.TryGetValue(weaponKey.Trim(), out DHWeaponTemplate template)
            ? template.GetSkillSubValueAtLevel(level)
            : 0f;
    }

    /// <summary>Returns class skill value for the given skill level.</summary>
    public float GetClassSkillValueAtLevel(int skillIndex, int level)
    {
        EnsureLoaded();
        return classSkillTemplateLookup.TryGetValue(skillIndex, out DHClassSkillTemplate template)
            ? LeveledFloat(template.ValueLv1, template.ValueLv2, template.ValueLv3,
                           template.ValueLv4, template.ValueLv5, level)
            : 0f;
    }

    /// <summary>Returns class skill sub-value for the given skill level.</summary>
    public float GetClassSkillSubValueAtLevel(int skillIndex, int level)
    {
        EnsureLoaded();
        return classSkillTemplateLookup.TryGetValue(skillIndex, out DHClassSkillTemplate template)
            ? LeveledFloat(template.SubValueLv1, template.SubValueLv2, template.SubValueLv3,
                           template.SubValueLv4, template.SubValueLv5, level)
            : 0f;
    }

    // Level mapping helpers.

    private static float LeveledFloat(float lv1, float lv2, float lv3, float lv4, float lv5, int level)
    {
        return level switch { 2 => lv2, 3 => lv3, 4 => lv4, 5 => lv5, _ => lv1 };
    }

    private static float LeveledInt(int lv1, int lv2, int lv3, int lv4, int lv5, int level)
    {
        return level switch { 2 => lv2, 3 => lv3, 4 => lv4, 5 => lv5, _ => lv1 };
    }

    public List<WeaponData> GetAllWeapons()
    {
        EnsureLoaded();
        return new List<WeaponData>(cachedWeapons);
    }

    public List<SkillData> GetAllSkills()
    {
        EnsureLoaded();
        var result = new List<SkillData>(skillTemplates.Values);
        result.Sort((a, b) => (a?.skillIndex ?? 0).CompareTo(b?.skillIndex ?? 0));
        return result;
    }

    public List<SkillData> GetSkillsByClassIndex(int classIndex)
    {
        EnsureLoaded();
        var result = new List<SkillData>();

        if (classIndex <= 0 ||
            !classSkillIndexListByClassIndex.TryGetValue(classIndex, out List<int> classSkillIndices) ||
            classSkillIndices == null)
        {
            return result;
        }

        for (int i = 0; i < classSkillIndices.Count; i++)
        {
            int skillIndex = classSkillIndices[i];
            if (skillIndex <= 0)
            {
                continue;
            }

            SkillData skill = GetSkillTemplate(skillIndex);
            if (skill != null)
            {
                result.Add(skill);
            }
        }

        return result;
    }

    public List<WeaponData> GetWeaponsByClassIndex(int classIndex)
    {
        EnsureLoaded();
        var result = new List<WeaponData>();

        if (classIndex <= 0 ||
            !weaponIndexListByClassIndex.TryGetValue(classIndex, out List<int> weaponIndices) ||
            weaponIndices == null)
        {
            return result;
        }

        for (int i = 0; i < weaponIndices.Count; i++)
        {
            int weaponIndex = weaponIndices[i];
            if (weaponIndex <= 0)
            {
                continue;
            }

            if (TryGetWeapon(weaponIndex, out WeaponData weapon) && weapon != null)
            {
                result.Add(weapon);
            }
        }

        return result;
    }

    public List<DHWeaponTemplate> GetWeaponTemplatesByClassIndex(int classIndex)
    {
        EnsureLoaded();
        var result = new List<DHWeaponTemplate>();

        if (classIndex <= 0 ||
            !weaponIndexListByClassIndex.TryGetValue(classIndex, out List<int> weaponIndices) ||
            weaponIndices == null)
        {
            return result;
        }

        for (int i = 0; i < weaponIndices.Count; i++)
        {
            int weaponIndex = weaponIndices[i];
            if (weaponIndex <= 0)
            {
                continue;
            }

            if (TryGetWeaponTemplate(weaponIndex, out DHWeaponTemplate weapon) && weapon != null)
            {
                result.Add(weapon);
            }
        }

        return result;
    }

    public List<SkillData> GetAvailableSkillsByClassIndex(int classIndex, int level)
    {
        int safeLevel = Mathf.Max(1, level);
        List<SkillData> allSkills = GetSkillsByClassIndex(classIndex);
        var result = new List<SkillData>();

        for (int i = 0; i < allSkills.Count; i++)
        {
            SkillData skill = allSkills[i];
            if (skill != null && skill.acquireLevel <= safeLevel)
            {
                result.Add(skill);
            }
        }

        return result;
    }

    public bool TryGetEnemyGroupTemplate(string groupKey, out DHEnemyGroupTemplate group)
    {
        EnsureLoaded();
        string normalizedKey = NormalizeEnemyGroupKey(groupKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            group = null;
            return false;
        }

        return enemyGroupLookup.TryGetValue(normalizedKey, out group);
    }

    public bool TryGetEnemyGroupTemplate(int groupIndex, out DHEnemyGroupTemplate group)
    {
        return TryGetEnemyGroupTemplate(groupIndex.ToString(), out group);
    }

    /// <summary>Returns every skillIndex unlocked by the class at or below currentLevel.</summary>
    public List<int> GetAvailableStudySkills(int classIndex, int currentLevel)
    {
        EnsureLoaded();
        var result = new List<int>();
        if (!skillUnlockByClassIndex.TryGetValue(classIndex, out var map))
            return result;

        foreach (var kvp in map)
        {
            if (kvp.Key <= currentLevel)
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>Returns skillIndex values newly unlocked between oldLevel and newLevel.</summary>
    public List<int> GetNewlyUnlockedStudySkills(int classIndex, int oldLevel, int newLevel)
    {
        EnsureLoaded();
        var result = new List<int>();
        if (newLevel <= oldLevel) return result;
        if (!skillUnlockByClassIndex.TryGetValue(classIndex, out var map))
            return result;

        foreach (var kvp in map)
        {
            if (kvp.Key > oldLevel && kvp.Key <= newLevel)
                result.Add(kvp.Value);
        }
        return result;
    }

    public List<DHEnemyGroupTemplate> GetAllEnemyGroupTemplates()
    {
        EnsureLoaded();
        return new List<DHEnemyGroupTemplate>(enemyGroupLookup.Values);
    }

    public List<SkillData> GetSkillsByClass(string className)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(className)) return new List<SkillData>();
        string normalized = className.Trim();

        if (TryResolvePlayerClassIndexByName(normalized, out int classIndex))
        {
            List<SkillData> byClassIndex = GetSkillsByClassIndex(classIndex);
            if (byClassIndex.Count > 0)
            {
                return byClassIndex;
            }
        }

        var result = new List<SkillData>();
        foreach (var skill in skillTemplates.Values)
        {
            if (skill != null &&
                !string.IsNullOrWhiteSpace(skill.skillClass) &&
                string.Equals(skill.skillClass.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
            {
                result.Add(skill);
            }
        }
        result.Sort((a, b) => a.acquireLevel.CompareTo(b.acquireLevel));
        return result;
    }

    // SO DataTable load path.

    public List<WeaponData> GetWeaponsByClass(string className)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(className)) return new List<WeaponData>();

        if (TryResolvePlayerClassIndexByName(className.Trim(), out int classIndex))
        {
            return GetWeaponsByClassIndex(classIndex);
        }

        return new List<WeaponData>();
    }

    private bool TryResolvePlayerClassIndexByName(string className, out int classIndex)
    {
        classIndex = 0;
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        string normalized = className.Trim();
        foreach (var pair in playerUnitTemplateLookup)
        {
            DHPlayerUnitTemplate unit = pair.Value;
            if (unit == null)
            {
                continue;
            }

            bool matchesClassName =
                !string.IsNullOrWhiteSpace(unit.ClassName) &&
                string.Equals(unit.ClassName.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase);
            bool matchesUnitName =
                !string.IsNullOrWhiteSpace(unit.UnitName) &&
                string.Equals(unit.UnitName.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase);

            if (matchesClassName || matchesUnitName)
            {
                classIndex = unit.ClassIndex;
                return classIndex > 0;
            }
        }

        return false;
    }

    private void ReloadFromSOTables()
    {
        // Player units
        if (playerUnitDataTable != null)
        {
            for (int i = 0; i < playerUnitDataTable.DataList.Count; i++)
            {
                PlayerUnitData src = playerUnitDataTable.DataList[i];
                DHPlayerUnitTemplate template = ConvertPlayerUnitTemplate(src);
                if (template == null || string.IsNullOrWhiteSpace(template.UnitKey)) continue;

                string playerKey = NormalizeNumericTemplateKey(template.UnitKey);
                if (playerTemplateLookup.ContainsKey(playerKey))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] Duplicate player unit key '{template.UnitKey}' skipped.", this);
                    continue;
                }
                RegisterPlayerUnitTemplate(template);
                RegisterPlayerUnitIndexLists(template);
                UnitData unit = ConvertPlayerUnit(template);
                playerTemplateLookup.Add(playerKey, unit);
                cachedPlayerTemplates.Add(unit);
            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] playerUnitDataTable is not assigned.", this);
        }

        // Enemy units and embedded enemy skills
        if (enemyUnitInfoDataTable != null)
        {
            for (int i = 0; i < enemyUnitInfoDataTable.DataList.Count; i++)
            {
                EnemyUnitData src = enemyUnitInfoDataTable.DataList[i];
                DHEnemyUnitTemplate enemyUnit = ConvertEnemyUnitTemplate(src);
                if (enemyUnit == null || string.IsNullOrWhiteSpace(enemyUnit.EnemyKey)) continue;

                string enemyKey = NormalizeNumericTemplateKey(enemyUnit.EnemyKey);
                if (enemyUnitTemplateLookup.ContainsKey(enemyKey))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] Duplicate enemy key '{enemyKey}' skipped.", this);
                    continue;
                }
                RegisterEnemyUnitTemplate(enemyUnit);
                EnemyData enemy = ConvertEnemyUnit(enemyUnit);
                RegisterEnemyTemplate(enemy);
                cachedEnemyTemplates.Add(enemy);

                // Extract embedded enemy skills.
                TryAddEnemySkill(src, slot: 1);
                TryAddEnemySkill(src, slot: 2);
            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] enemyUnitInfoDataTable is not assigned.", this);
        }

        // Enemy groups
        if (enemyGroupDataTable != null)
        {
            for (int i = 0; i < enemyGroupDataTable.DataList.Count; i++)
            {
                EnemyGroupData group = enemyGroupDataTable.DataList[i];
                DHEnemyGroupTemplate template = ConvertEnemyGroup(group);
                if (template == null) continue;
                if (enemyGroupLookup.ContainsKey(template.GroupKey))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] Duplicate enemy group index {group.EnemyIndex} skipped.", this);
                    continue;
                }
                enemyGroupLookup.Add(template.GroupKey, template);
            }
        }

        // Class skills
        if (classSkillDataTable != null)
        {
            for (int i = 0; i < classSkillDataTable.DataList.Count; i++)
            {
                ClassSkillData src = classSkillDataTable.DataList[i];
                DHClassSkillTemplate template = ConvertClassSkillTemplate(src);
                if (template == null) continue;

                if (classSkillTemplateLookup.ContainsKey(template.NumericSkillId))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] Duplicate class skill index {template.NumericSkillId} skipped.", this);
                    continue;
                }
                RegisterClassSkillTemplate(template);
                SkillData skill = ConvertClassSkill(template);
                skillTemplates.Add(skill.skillIndex, skill);
                if (!string.IsNullOrEmpty(skill.skillKey)) skillTemplatesByKey[skill.skillKey] = skill;   // §5-3 string 병렬

            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] classSkillDataTable is not assigned.", this);
        }

        // Weapons
        if (playerWeaponDataTable != null)
        {
            for (int i = 0; i < playerWeaponDataTable.DataList.Count; i++)
            {
                PlayerWeaponData src = playerWeaponDataTable.DataList[i];
                DHWeaponTemplate template = ConvertWeaponTemplate(src);
                if (template == null || template.NumericWeaponId <= 0) continue;

                if (weaponTemplateLookup.ContainsKey(template.NumericWeaponId))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] Duplicate weapon index {template.NumericWeaponId} skipped.", this);
                    continue;
                }
                RegisterWeaponTemplate(template);
                WeaponData weapon = ConvertWeapon(template);
                weaponLookup.Add(weapon.WeaponIndex, weapon);
                if (!string.IsNullOrEmpty(weapon.weaponKey)) weaponByKey[weapon.weaponKey] = weapon;   // §5-3 string 병렬
                cachedWeapons.Add(weapon);
            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] playerWeaponDataTable is not assigned.", this);
        }

        // Unit growth and class unlock skills
        if (unitGrowthExpDataTable != null)
        {
            for (int i = 0; i < unitGrowthExpDataTable.DataList.Count; i++)
            {
                UnitGrowthExpData src = unitGrowthExpDataTable.DataList[i];
                if (src == null) continue;

                DHUnitGrowthTemplate template = ConvertUnitGrowthTemplate(src);
                if (template == null) continue;

                unitGrowthTemplates.Add(template);

                LevelUpData row = ConvertLevelUp(template);
                if (row != null) levelUpTemplates.Add(row);

                foreach (var kvp in template.StudySkillByClassIndex)
                {
                    int classIndex = kvp.Key;
                    int skillIndex = kvp.Value;
                    if (skillIndex <= 0) continue;

                    if (!skillUnlockByClassIndex.TryGetValue(classIndex, out var map))
                        skillUnlockByClassIndex[classIndex] = map = new Dictionary<int, int>();

                    if (map.ContainsKey(template.Level))
                    {
                        Debug.LogWarning($"[DHCsvTemplateCatalog] Duplicate study skill unlock skipped. classIndex={classIndex}, level={template.Level}.", this);
                        continue;
                    }
                    map[template.Level] = skillIndex;
                }
            }
        }

        isLoaded = true;
        Debug.Log($"[DHCsvTemplateCatalog][SO] Loaded players={cachedPlayerTemplates.Count}, " +
                  $"enemies={cachedEnemyTemplates.Count}, " +
                  $"weapons={cachedWeapons.Count}, " +
                  $"skills={skillTemplates.Count}, " +
                  $"levelUps={levelUpTemplates.Count}.", this);
    }

    // SO conversion methods.

    private DHEnemyGroupTemplate ConvertEnemyGroup(EnemyGroupData src)
    {
        if (src == null)
        {
            return null;
        }

        string groupKey = NormalizeEnemyGroupKey(src.EnemyIndex);
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            Debug.LogWarning($"[DHCsvTemplateCatalog] Enemy group index '{src.EnemyIndex}' is invalid.", this);
            return null;
        }

        var template = new DHEnemyGroupTemplate
        {
            GroupKey = groupKey,
            GroupName = src.EnemyGroupName,
            MinLevel = src.MinLevel,
            MaxLevel = src.MaxLevel
        };

        if (!TryAddEnemyGroupMember(template, src.Enemy1, src.Enemy1Slot) ||
            !TryAddEnemyGroupMember(template, src.Enemy2, src.Enemy2Slot) ||
            !TryAddEnemyGroupMember(template, src.Enemy3, src.Enemy3Slot) ||
            !TryAddEnemyGroupMember(template, src.Enemy4, src.Enemy4Slot) ||
            !TryAddEnemyGroupMember(template, src.Enemy5, src.Enemy5Slot) ||
            !TryAddEnemyGroupMember(template, src.Enemy6, src.Enemy6Slot))
        {
            Debug.LogWarning($"[DHCsvTemplateCatalog] Enemy group '{src.EnemyIndex}' has invalid enemy member slot data.", this);
            return null;
        }

        return template.Members.Count > 0 ? template : null;
    }

    private static string NormalizeEnemyGroupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private static bool TryAddEnemyGroupMember(DHEnemyGroupTemplate template, string enemyUnitCode, int combatSlot)
    {
        if (template == null)
        {
            return false;
        }

        int enemyUnitIndex = ExtractNumericId(enemyUnitCode);
        if (enemyUnitIndex <= 0)
        {
            return true;
        }

        if (combatSlot < 1 || combatSlot > 6)
        {
            return false;
        }

        for (int i = 0; i < template.Members.Count; i++)
        {
            if (template.Members[i].CombatSlot == combatSlot)
            {
                return false;
            }
        }

        template.Members.Add(new DHEnemyGroupMember(enemyUnitIndex, combatSlot));
        return true;
    }

    private static DHPlayerUnitTemplate ConvertPlayerUnitTemplate(PlayerUnitData src)
    {
        if (src == null) return null;
        int classIndex = ExtractNumericId(src.ClassIndex);
        return new DHPlayerUnitTemplate(
            classIndex > 0 ? classIndex.ToString() : src.ClassIndex.ToString(),
            classIndex,
            src.UnitName,
            src.ClassName,
            src.ClassConcept,
            new StatBlock(
                hp:           src.UnitMaxHP,
                atk:          src.UnitATK,
                def:          src.UnitDEF,
                luck:         0f,
                speed:        src.Speed,
                criticalRate: src.CriticalRate,
                critMultiplier: 1.5f,
                counterRate:  src.CounterRate,
                avoidRate:    src.ReduceRate,
                influence:    DefaultPlayerBaseInfluence),
            new StatBlock(
                hp:  src.LevelGrowthMaxHP,
                atk: src.LevelGrowthMaxAtk,
                def: src.LevelGrowthMaxDef,
                luck: 0f, speed: 0f),
            ToIntIndexList(src.ClassSkillIndexList),
            ToIntIndexList(src.WeaponIndexList));
    }

    private static DHPlayerUnitTemplate ConvertPlayerUnitTemplate(UnitData src)
    {
        if (src == null) return null;
        int classIndex = ExtractNumericId(src.Index);
        return new DHPlayerUnitTemplate(
            classIndex > 0 ? classIndex.ToString() : src.Index,
            classIndex,
            src.Name,
            src.UnitType,
            string.Empty,
            src.baseStats,
            src.levelupStats,
            null,
            null);
    }

    private static UnitData ConvertPlayerUnit(DHPlayerUnitTemplate src)
    {
        if (src == null) return null;
        return new UnitData
        {
            Index = src.ClassIndex > 0 ? src.ClassIndex.ToString() : src.UnitKey,
            UnitType = src.ClassName,
            Name = src.UnitName,
            baseStats = src.BaseStats,
            levelupStats = src.LevelupStats,
            IsEnemyRow = false
        };
    }

    private void RegisterPlayerUnitIndexLists(DHPlayerUnitTemplate src)
    {
        if (src == null || src.ClassIndex <= 0)
        {
            return;
        }

        classSkillIndexListByClassIndex[src.ClassIndex] = new List<int>(src.ClassSkillIndices);
        weaponIndexListByClassIndex[src.ClassIndex] = new List<int>(src.WeaponIndices);
    }

    private static List<int> ToIntIndexList(IReadOnlyList<string> source)
    {
        var result = new List<int>();
        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            int index = ExtractNumericId(source[i]);
            if (index > 0)
            {
                result.Add(index);
            }
        }

        return result;
    }

    private static DHEnemyUnitTemplate ConvertEnemyUnitTemplate(EnemyUnitData src)
    {
        if (src == null) return null;
        int enemyIndex = ExtractNumericId(src.EnemyIndex);
        string enemyKey = enemyIndex > 0 ? enemyIndex.ToString() : src.EnemyIndex.ToString();
        return new DHEnemyUnitTemplate(
            enemyKey,
            enemyIndex,
            src.EnemyName,
            src.EnemyConcept,
            src.UnitAI,
            Mathf.RoundToInt(src.ExperiencePoint),
            new StatBlock(
                hp:           src.UnitMaxHP,
                atk:          src.UnitATK,
                def:          src.UnitDEF,
                luck:         0f,
                speed:        src.Speed,
                criticalRate: src.CriticalRate,
                critMultiplier: 1.5f,
                counterRate:  src.CounterRate,
                avoidRate:    src.ReduceRate),
            new StatBlock(
                hp:  src.LevelGrowthMaxHP,
                atk: src.LevelGrowthAtk,
                def: src.LevelGrowthDef,
                luck: 0f, speed: 0f));
    }

    private static DHEnemyUnitTemplate ConvertEnemyUnitTemplate(EnemyData src)
    {
        if (src == null) return null;
        int enemyIndex = ExtractNumericId(src.Index);
        string enemyKey = enemyIndex > 0 ? enemyIndex.ToString() : src.Index;
        return new DHEnemyUnitTemplate(
            enemyKey,
            enemyIndex,
            src.Name,
            string.Empty,
            src.UnitAI,
            Mathf.RoundToInt(src.ExperiencePoint),
            src.baseStats,
            src.levelupStats);
    }

    private static EnemyData ConvertEnemyUnit(DHEnemyUnitTemplate src)
    {
        if (src == null) return null;
        return new EnemyData
        {
            Index = !string.IsNullOrWhiteSpace(src.EnemyKey) ? src.EnemyKey : src.NumericEnemyId.ToString(),
            UnitType = string.Empty,
            Name = src.EnemyName,
            baseStats = src.BaseStats,
            levelupStats = src.LevelupStats,
            IsEnemyRow      = true,
            UnitAI          = src.UnitAI,
            ExperiencePoint = src.ExperiencePoint
        };
    }

    private void TryAddEnemySkill(EnemyUnitData src, int slot)
    {
        string skillName = slot == 1 ? src.EnemySkill1_Name : src.EnemySkill2_Name;
        if (string.IsNullOrWhiteSpace(skillName)) return;

        int enemyIndex = ExtractNumericId(src.EnemyIndex);
        if (enemyIndex <= 0) return;

        int skillIndex = (enemyIndex * 10) + slot;
        if (skillTemplates.ContainsKey(skillIndex))
        {
            Debug.LogWarning($"[DHCsvTemplateCatalog] Duplicate enemy skill index {skillIndex} skipped.", this);
            return;
        }

        SkillData skill;
        if (slot == 1)
        {
            skill = new SkillData
            {
                skillIndex      = skillIndex,   // [TEMP:STRKEY] 레거시 int 브리지
                skillKey        = EnemySkillKeyRules.Compose(src.EnemyIndex, slot),
                category        = SkillCategory.Enemy,
                slot            = slot,
                skillClass      = src.EnemyName,
                acquireLevel    = 1,
                skillName       = src.EnemySkill1_Name,
                description     = src.EnemySkill1_Description,
                ipCost          = 0,
                classSkillEffect  = src.EnemySkill1Effect,
                classSkillRange   = src.EnemySkill1Range,
                EnemySkill1Range  = src.EnemySkill1Range,
                EnemySkill2Range  = -1,
                classSkillRangeLine = src.EnemySkill1RangeLine,
                classSkillTarget  = src.EnemySkill1Target,
                boundary          = new List<int>(src.EnemySkill1MultiTarget ?? new List<int>()),
                multiTargetCount  = src.EnemySkill1_MultiTargetCount,
                skillValue        = src.EnemySkill1Value,
                AnimationTrigger  = "Attack"
            };
        }
        else
        {
            skill = new SkillData
            {
                skillIndex      = skillIndex,   // [TEMP:STRKEY] 레거시 int 브리지
                skillKey        = EnemySkillKeyRules.Compose(src.EnemyIndex, slot),
                category        = SkillCategory.Enemy,
                slot            = slot,
                skillClass      = src.EnemyName,
                acquireLevel    = 1,
                skillName       = src.EnemySkill2_Name,
                description     = src.EnemySkill2_Description,
                ipCost          = 0,
                classSkillEffect  = src.EnemySkill2Effect,
                classSkillRange   = src.EnemySkill2Range,
                EnemySkill1Range  = -1,
                EnemySkill2Range  = src.EnemySkill2Range,
                classSkillRangeLine = src.EnemySkill2RangeLine,
                classSkillTarget  = src.EnemySkill2Target,
                boundary          = new List<int>(src.EnemySkill2MultiTarget ?? new List<int>()),
                multiTargetCount  = src.EnemySkill2_MultiTargetCount,
                skillValue        = src.EnemySkill2Value,
                AnimationTrigger  = "Attack"
            };
        }

        skillTemplates.Add(skillIndex, skill);
        if (!string.IsNullOrEmpty(skill.skillKey)) skillTemplatesByKey[skill.skillKey] = skill;   // §5-3 string 병렬
    }

    private static DHWeaponTemplate ConvertWeaponTemplate(PlayerWeaponData src)
    {
        if (src == null) return null;
        int weaponIndex = ExtractNumericId(src.WeaponIndex);
        // §5-1 키 원본 보존: ExtractNumericId 손실 제거 (HC001 그대로 유지). NumericWeaponId는 하위호환용으로 병존.
        return new DHWeaponTemplate(
            string.IsNullOrWhiteSpace(src.WeaponIndex) ? string.Empty : src.WeaponIndex.Trim(),
            weaponIndex,
            ResolveWeaponClass(weaponIndex),
            src.WeaponName,
            src.WaeponDescription,
            src.BonusMaxHPLv1,
            src.BonusMaxHPLv2,
            src.BonusMaxHPLv3,
            src.BonusMaxHPLv4,
            src.BonusMaxHPLv5,
            src.BonusATKLv1,
            src.BonusATKLv2,
            src.BonusATKLv3,
            src.BonusATKLv4,
            src.BonusATKLv5,
            src.BonusDEFLv1,
            src.BonusDEFLv2,
            src.BonusDEFLv3,
            src.BonusDEFLv4,
            src.BonusDEFLv5,
            src.BonusCriticalRate,
            src.BonusCounterRate,
            src.BonusReduceRate,
            src.BonusSpeed,
            src.WeaponSkillIndex,
            src.WeaponSkillName,
            src.WeaponSkillDescription,
            src.IPCost,
            src.WeaponSkillEffect,
            src.WeaponSkillRange,
            src.WeaponSkillRangeLine,
            src.WeaponSkillTarget,
            src.WeaponSkillMultiTarget,
            src.WeaponSkill_MultiTargetType,
            src.WeaponSkillMultiTargetCount,
            src.WeaponSkillValueLv1,
            src.WeaponSkillSubValueLv1,
            src.WeaponSkillValueLv2,
            src.WeaponSkillSubValueLv2,
            src.WeaponSkillValueLv3,
            src.WeaponSkillSubValueLv3,
            src.WeaponSkillValueLv4,
            src.WeaponSkillSubValueLv4,
            src.WeaponSkillValueLv5,
            src.WeaponSkillSubValueLv5);
    }

    private static DHWeaponTemplate ConvertWeaponTemplate(WeaponData src)
    {
        if (src == null) return null;
        return new DHWeaponTemplate(
            src.WeaponIndex > 0 ? src.WeaponIndex.ToString() : string.Empty,
            src.WeaponIndex,
            src.weaponClass,
            src.WeaponName,
            src.WeaponDescription,
            src.BonusHP,
            src.BonusHP,
            src.BonusHP,
            src.BonusHP,
            src.BonusHP,
            src.BonusATK,
            src.BonusATK,
            src.BonusATK,
            src.BonusATK,
            src.BonusATK,
            src.BonusDEF,
            src.BonusDEF,
            src.BonusDEF,
            src.BonusDEF,
            src.BonusDEF,
            src.BonusCriticalRate,
            src.BonusCounterRate,
            src.BonusReduceRate,
            src.BonusSpeed,
            src.WeaponSkillIndex.ToString(),
            src.WeaponSkillName,
            src.WeaponSkillDescription,
            src.IPCost,
            src.WeaponSkillEffect,
            src.WeaponSkillRange,
            src.WeaponSkillRangeLine,
            src.WeaponSkillTarget,
            src.WeaponSkillMultiTarget,
            src.WeaponSkillMultiTargetType,
            src.WeaponSkillMultiTargetCount,
            src.WeaponSkillValue,
            src.WeaponSkillSubValue,
            src.WeaponSkillValue,
            src.WeaponSkillSubValue,
            src.WeaponSkillValue,
            src.WeaponSkillSubValue,
            src.WeaponSkillValue,
            src.WeaponSkillSubValue,
            src.WeaponSkillValue,
            src.WeaponSkillSubValue);
    }

    private static WeaponData ConvertWeapon(DHWeaponTemplate src)
    {
        if (src == null) return null;
        return new WeaponData
        {
            WeaponIndex          = src.NumericWeaponId,   // [TEMP:STRKEY] 레거시 int 브리지
            weaponKey            = src.WeaponKey,
            weaponClass          = src.WeaponClass,
            WeaponName           = src.WeaponName,
            WeaponDescription    = src.WeaponDescription,
            BonusHP              = src.BonusHPLv1,
            BonusATK             = src.BonusATKLv1,
            BonusDEF             = src.BonusDEFLv1,
            BonusCriticalRate    = src.BonusCriticalRate,
            BonusCounterRate     = src.BonusCounterRate,
            BonusReduceRate      = src.BonusReduceRate,
            BonusSpeed           = src.BonusSpeed,
            WeaponSkillIndex     = src.WeaponSkillIndex,   // [TEMP:STRKEY] 레거시 int 브리지
            weaponSkillKey       = src.WeaponSkillKey,
            WeaponSkillName      = src.WeaponSkillName,
            WeaponSkillDescription = src.WeaponSkillDescription,
            IPCost               = src.IpCost,
            WeaponSkillEffect    = src.WeaponSkillEffect,
            WeaponSkillRange     = src.WeaponSkillRange,
            WeaponSkillRangeLine = src.WeaponSkillRangeLine,
            WeaponSkillTarget    = src.WeaponSkillTarget,
            WeaponSkillMultiTarget = src.WeaponSkillMultiTarget != null ? new List<int>(src.WeaponSkillMultiTarget) : new List<int>(),
            WeaponSkillMultiTargetType  = src.WeaponSkillMultiTargetType,
            WeaponSkillMultiTargetCount = src.WeaponSkillMultiTargetCount,
            WeaponSkillValue     = src.WeaponSkillValueLv1,
            WeaponSkillSubValue  = src.WeaponSkillSubValueLv1
        };
    }

    /// <summary>Extracts the numeric part from sheet IDs such as "HS1010" or "FV20001".</summary>
    private static int ExtractNumericId(int code)
    {
        return code;
    }

    private static int ExtractNumericId(string code)
    {
        if (string.IsNullOrEmpty(code)) return 0;
        Match match = Regex.Match(code, @"\d+");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    private static string NormalizeNumericTemplateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        int numericId = ExtractNumericId(key);
        return numericId > 0 ? numericId.ToString() : key.Trim();
    }

    private void RegisterEnemyTemplate(EnemyData template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.Index))
        {
            return;
        }

        string normalizedKey = NormalizeNumericTemplateKey(template.Index);
        template.Index = normalizedKey;
        enemyTemplateLookup.Add(normalizedKey, template);
    }

    private void RegisterPlayerUnitTemplate(DHPlayerUnitTemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.UnitKey))
        {
            return;
        }

        string normalizedKey = NormalizeNumericTemplateKey(template.UnitKey);
        playerUnitTemplateLookup.Add(normalizedKey, template);
    }

    private void RegisterEnemyUnitTemplate(DHEnemyUnitTemplate template)
    {
        if (template == null || string.IsNullOrWhiteSpace(template.EnemyKey))
        {
            return;
        }

        string normalizedKey = NormalizeNumericTemplateKey(template.EnemyKey);
        enemyUnitTemplateLookup.Add(normalizedKey, template);
    }

    private void RegisterWeaponTemplate(DHWeaponTemplate template)
    {
        if (template == null || template.NumericWeaponId <= 0)
        {
            return;
        }

        weaponTemplateLookup.Add(template.NumericWeaponId, template);
        if (!string.IsNullOrEmpty(template.WeaponKey)) weaponTemplateByKey[template.WeaponKey] = template;   // §5-3 string 병렬
    }

    private void RegisterClassSkillTemplate(DHClassSkillTemplate template)
    {
        if (template == null || template.NumericSkillId <= 0)
        {
            return;
        }

        classSkillTemplateLookup.Add(template.NumericSkillId, template);
        if (!string.IsNullOrEmpty(template.SkillKey)) classSkillTemplateByKey[template.SkillKey] = template;   // §5-3 string 병렬
    }

    private static string ResolveWeaponClass(int weaponIndex)
    {
        int classCode = (weaponIndex / 100) % 100;
        switch (classCode)
        {
            case 1: return "Guardian";
            case 2: return "Blaster";
            case 3: return "Striker";
            case 4: return "Supporter";
            case 5: return "Fighter";
            default: return string.Empty;
        }
    }

    private static DHClassSkillTemplate ConvertClassSkillTemplate(ClassSkillData src)
    {
        if (src == null) return null;
        int classSkillIndex = ExtractNumericId(src.ClassSkillIndex);
        // §5-1 키 원본 보존: ExtractNumericId 손실 제거 (HS1010 그대로 유지). NumericSkillId는 하위호환용으로 병존.
        string skillKey = string.IsNullOrWhiteSpace(src.ClassSkillIndex) ? string.Empty : src.ClassSkillIndex.Trim();
        return new DHClassSkillTemplate(
            skillKey,
            classSkillIndex,
            src.Class,
            src.ClassSkill_AcquireRank,
            src.ClassSkillName,
            src.ClassSkillDescription,
            src.IPCost,
            src.ClassSkillEffect,
            src.ClassSkillRange,
            src.ClassSkillRangeLine,
            src.ClassSkillTarget,
            src.ClassSkillMultiTarget,
            src.ClassSkill_MultiTargetType,
            src.ClassSkill_MultiTargetCount,
            src.ClassSkillValueLv1,
            src.ClassSkillSubValueLv1,
            src.ClassSkillValueLv2,
            src.ClassSkillSubValueLv2,
            src.ClassSkillValueLv3,
            src.ClassSkillSubValueLv3,
            src.ClassSkillValueLv4,
            src.ClassSkillSubValueLv4,
            src.ClassSkillValueLv5,
            src.ClassSkillSubValueLv5,
            src.ReplaceSkillIndex,
            src.SkillRiskIndex,
            "Attack");
    }

    private static DHClassSkillTemplate ConvertClassSkillTemplate(SkillData src)
    {
        if (src == null) return null;
        return new DHClassSkillTemplate(
            src.skillIndex > 0 ? src.skillIndex.ToString() : string.Empty,
            src.skillIndex,
            src.skillClass,
            src.acquireLevel,
            src.skillName,
            src.description,
            src.ipCost,
            src.classSkillEffect,
            src.classSkillRange,
            src.classSkillRangeLine,
            src.classSkillTarget,
            src.boundary,
            src.multiTargetType,
            src.multiTargetCount,
            src.skillValue,
            src.skillSubValue,
            src.skillValue,
            src.skillSubValue,
            src.skillValue,
            src.skillSubValue,
            src.skillValue,
            src.skillSubValue,
            src.skillValue,
            src.skillSubValue,
            string.Empty,
            string.Empty,
            src.AnimationTrigger);
    }

    private static SkillData ConvertClassSkill(DHClassSkillTemplate src)
    {
        if (src == null) return null;
        return new SkillData
        {
            skillIndex          = src.NumericSkillId,   // [TEMP:STRKEY] 레거시 int 브리지
            skillKey            = src.SkillKey,
            category            = SkillCategory.Class,
            skillClass          = src.ClassName,
            acquireLevel        = src.AcquireLevel,
            skillName           = src.SkillName,
            description         = src.Description,
            ipCost              = src.IpCost,
            classSkillEffect    = src.Effect,
            classSkillRange     = src.Range,
            classSkillRangeLine = src.RangeLine,
            classSkillTarget    = src.Target,
            boundary            = src.Boundary != null ? new List<int>(src.Boundary) : new List<int>(),
            multiTargetType     = src.MultiTargetType,
            multiTargetCount    = src.MultiTargetCount,
            skillValue          = src.ValueLv1,
            skillSubValue       = src.SubValueLv1,
            AnimationTrigger    = src.AnimationTrigger
        };
    }

    private static DHUnitGrowthTemplate ConvertUnitGrowthTemplate(UnitGrowthExpData src)
    {
        if (src == null) return null;
        var studySkillByClassIndex = new Dictionary<int, int>(GrowthSkillClassIndices.Length);
        AddStudySkill(studySkillByClassIndex, GrowthSkillClassIndices[0], src.Character1StudySkill);
        AddStudySkill(studySkillByClassIndex, GrowthSkillClassIndices[1], src.Character2StudySkill);
        AddStudySkill(studySkillByClassIndex, GrowthSkillClassIndices[2], src.Character3StudySkill);
        AddStudySkill(studySkillByClassIndex, GrowthSkillClassIndices[3], src.Character4StudySkill);

        return new DHUnitGrowthTemplate(
            src.Level,
            src.Rank,
            src.NeedExpieriencePoint,
            src.AddIP,
            studySkillByClassIndex);
    }

    private static void AddStudySkill(Dictionary<int, int> destination, int classIndex, string rawSkillKey)
    {
        if (destination == null || classIndex <= 0)
            return;

        int skillIndex = ExtractNumericId(rawSkillKey);
        if (skillIndex > 0)
            destination[classIndex] = skillIndex;
    }

    private static LevelUpData ConvertLevelUp(DHUnitGrowthTemplate src)
    {
        if (src == null) return null;
        return new LevelUpData
        {
            level        = src.Level,
            Rank         = string.IsNullOrEmpty(src.Rank) ? '\0' : src.Rank[0],
            expPerLevel  = src.RequiredExperience,
            MaxIP        = src.AddInfluence
        };
    }

    private static DHUnitGrowthTemplate ConvertUnitGrowthTemplate(LevelUpData src)
    {
        if (src == null) return null;
        return new DHUnitGrowthTemplate(
            Mathf.RoundToInt(src.level),
            src.Rank == '\0' ? string.Empty : src.Rank.ToString(),
            Mathf.Max(0, src.expPerLevel),
            Mathf.RoundToInt(src.MaxIP),
            null);
    }

    // Common utilities.

    private void ClearCache()
    {
        playerTemplateLookup.Clear();
        playerUnitTemplateLookup.Clear();
        enemyTemplateLookup.Clear();
        enemyUnitTemplateLookup.Clear();
        weaponLookup.Clear();
        weaponByKey.Clear();
        weaponTemplateLookup.Clear();
        weaponTemplateByKey.Clear();
        skillTemplates.Clear();
        skillTemplatesByKey.Clear();
        classSkillTemplateLookup.Clear();
        classSkillTemplateByKey.Clear();
        levelUpTemplates.Clear();
        unitGrowthTemplates.Clear();
        cachedPlayerTemplates.Clear();
        cachedEnemyTemplates.Clear();
        cachedWeapons.Clear();
        classSkillIndexListByClassIndex.Clear();
        weaponIndexListByClassIndex.Clear();
        enemyGroupLookup.Clear();
        skillUnlockByClassIndex.Clear();
        isLoaded = false;
    }

    private void EnsureLoaded()
    {
        if (!isLoaded) ReloadTemplates();
    }

}
