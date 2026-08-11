using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentUnitRepository : MonoBehaviour
{
    private const float DefaultPlayerCurrentInfluence = 100f;

    public static PersistentUnitRepository Instance { get; private set; }

    [Header("Persistent Units")]
    [SerializeField] private int nextUnitIndex = 1;
    [SerializeField] private List<UnitPersistentData> units = new List<UnitPersistentData>();

    private readonly Dictionary<int, UnitPersistentData> unitLookup = new Dictionary<int, UnitPersistentData>();

    public IReadOnlyList<UnitPersistentData> Units => units;

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

    private void Start()
    {
        RepairAllMaxExpIfNeeded();
        EnsureDefaultWeaponInstances();
    }

    public int CreateUnit()
    {
        return CreateUnit(string.Empty, 1, default, default, 0, 0, default, default, 0f, 0, ResolveMaxExp(1));
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats)
    {
        return CreateUnit(unitTemplateKey, level, baseStats, default, 0, 0, default, default, 0f);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex)
    {
        return CreateUnit(unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, default, default, 0f);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats)
    {
        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        StatBlock ingameStats = UnitStatCalculator.CalculateIngameStats(
            baseStats,
            levelupStats,
            level,
            currentWeaponStats,
            default,
            unitGrowthTemplates);
        return CreateUnit(unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats, ingameStats, ingameStats.HP, 0, ResolveMaxExp(level, unitGrowthTemplates), DefaultPlayerCurrentInfluence);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, string currentWeaponKey, EquipmentStatBlock currentWeaponStats)
    {
        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        StatBlock ingameStats = UnitStatCalculator.CalculateIngameStats(
            baseStats,
            levelupStats,
            level,
            currentWeaponStats,
            default,
            unitGrowthTemplates);
        return CreateUnit(unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponKey, currentWeaponStats, ingameStats, ingameStats.HP, 0, ResolveMaxExp(level, unitGrowthTemplates), DefaultPlayerCurrentInfluence);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = 0, int maxExp = 0, float currentInfluence = -1f)
    {
        string currentWeaponKey = ResolveWeaponTemplateKey(currentWeaponIndex);
        return CreateUnit(unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponKey, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp, exp, maxExp, currentInfluence);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, string currentWeaponKey, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = 0, int maxExp = 0, float currentInfluence = -1f)
    {
        return CreateUnit(unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponKey, ResolveLegacyNumericWeaponTemplateKey(currentWeaponKey), currentWeaponStats, ingameStats, currentHp, exp, maxExp, currentInfluence);
    }

    public int CreateUnit(string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, string currentWeaponKey, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = 0, int maxExp = 0, float currentInfluence = -1f)
    {
        int unitIndex = Mathf.Max(1, nextUnitIndex);
        nextUnitIndex = unitIndex + 1;

        if (maxExp <= 0)
            maxExp = ResolveMaxExp(level);

        float effectiveCurrentInfluence = currentInfluence >= 0f ? currentInfluence : DefaultPlayerCurrentInfluence;
        var data = new UnitPersistentData(unitIndex, unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponKey, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp, exp, maxExp, currentInfluence: effectiveCurrentInfluence);
        units.Add(data);
        unitLookup[unitIndex] = data;
        Debug.Log($"[DHWeaponInit] Created unitIndex={unitIndex}, unitTemplateKey='{unitTemplateKey}', currentWeaponKey='{data.CurrentWeaponKey}', legacyWeaponIndex={data.CurrentWeaponIndex}.", this);
        EnsureDefaultWeaponInstance(unitIndex);
        return unitIndex;
    }

    public bool ContainsUnit(int unitIndex)
    {
        return unitLookup.ContainsKey(unitIndex);
    }

    public bool TryGetUnit(int unitIndex, out UnitPersistentData data)
    {
        if (!unitLookup.TryGetValue(unitIndex, out data))
            return false;

        RepairMaxExpIfNeeded(data);
        return true;
    }

    public bool RemoveUnit(int unitIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        unitLookup.Remove(unitIndex);
        units.Remove(data);
        return true;
    }

    public bool UpdateUnitRuntimeState(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = -1, int maxExp = -1, int skillLevel = -1, int equippedWeaponInstanceIndex = -1, float currentInfluence = -1f, bool? isIncapacitated = null)
    {
        string currentWeaponKey = ResolveWeaponTemplateKey(currentWeaponIndex);
        return UpdateUnitRuntimeState(unitIndex, unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponKey, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp, exp, maxExp, skillLevel, equippedWeaponInstanceIndex, currentInfluence, isIncapacitated);
    }

    public bool UpdateUnitRuntimeState(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, string currentWeaponKey, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = -1, int maxExp = -1, int skillLevel = -1, int equippedWeaponInstanceIndex = -1, float currentInfluence = -1f, bool? isIncapacitated = null)
    {
        return UpdateUnitRuntimeState(unitIndex, unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponKey, ResolveLegacyNumericWeaponTemplateKey(currentWeaponKey), currentWeaponStats, ingameStats, currentHp, exp, maxExp, skillLevel, equippedWeaponInstanceIndex, currentInfluence, isIncapacitated);
    }

    public bool UpdateUnitRuntimeState(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, string currentWeaponKey, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = -1, int maxExp = -1, int skillLevel = -1, int equippedWeaponInstanceIndex = -1, float currentInfluence = -1f, bool? isIncapacitated = null)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        int effectiveMaxExp = maxExp;
        if (effectiveMaxExp < 0 && data.MaxExp <= 0)
            effectiveMaxExp = ResolveMaxExp(level);

        data.ApplyRuntimeState(unitTemplateKey, level, baseStats, levelupStats, currentSkillIndex, currentWeaponKey, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp, exp, effectiveMaxExp, skillLevel, equippedWeaponInstanceIndex, currentInfluence, isIncapacitated);
        return true;
    }

    public bool SetIncapacitated(int unitIndex, bool isIncapacitated)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        float nextHp = isIncapacitated ? 0f : data.CurrentHp;
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            data.IngameStats,
            nextHp,
            data.Exp,
            data.MaxExp,
            data.SkillLevel,
            data.EquippedWeaponInstanceIndex,
            data.CurrentInfluence,
            isIncapacitated);
        return true;
    }

    public bool EquipWeaponInstance(int unitIndex, int weaponInstanceIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        WeaponPersistentRepository weaponRepository = WeaponPersistentRepository.Instance;
        if (weaponRepository == null || !weaponRepository.TryGetWeaponTemplateKey(weaponInstanceIndex, out string weaponTemplateKey))
            return false;

        if (!weaponRepository.TryGetWeaponStats(weaponInstanceIndex, out EquipmentStatBlock weaponStats))
            weaponStats = default;

        bool applied = ApplyWeaponState(data, weaponInstanceIndex, weaponTemplateKey, weaponStats);
        Debug.Log($"[DHWeaponInit] EquipWeaponInstance unitIndex={unitIndex}, weaponInstanceIndex={weaponInstanceIndex}, weaponTemplateKey='{weaponTemplateKey}', applied={applied}.", this);
        return applied;
    }

    public bool UnequipWeaponInstance(int unitIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        return ApplyWeaponState(data, 0, string.Empty, default);
    }

    public bool RefreshEquippedWeaponStats(int unitIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        int weaponInstanceIndex = data.EquippedWeaponInstanceIndex;
        if (weaponInstanceIndex <= 0)
            return ApplyWeaponState(data, 0, string.Empty, default);

        WeaponPersistentRepository weaponRepository = WeaponPersistentRepository.Instance;
        if (weaponRepository == null || !weaponRepository.TryGetWeaponTemplateKey(weaponInstanceIndex, out string weaponTemplateKey))
            return false;

        if (!weaponRepository.RefreshWeaponStats(weaponInstanceIndex))
            return false;

        if (!weaponRepository.TryGetWeaponStats(weaponInstanceIndex, out EquipmentStatBlock weaponStats))
            weaponStats = default;

        return ApplyWeaponState(data, weaponInstanceIndex, weaponTemplateKey, weaponStats);
    }

    public void RefreshUnitsEquippedWithWeapon(int weaponInstanceIndex)
    {
        if (weaponInstanceIndex <= 0 || units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData data = units[i];
            if (data == null || data.EquippedWeaponInstanceIndex != weaponInstanceIndex)
                continue;

            RefreshEquippedWeaponStats(data.UnitIndex);
        }
    }

    public bool EnsureDefaultWeaponInstance(int unitIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data) || data == null)
            return false;

        if (data.EquippedWeaponInstanceIndex > 0)
            return true;

        if (!TryResolveDefaultWeaponTemplateKey(data, out string weaponTemplateKey))
            return false;

        WeaponPersistentRepository weaponRepository = WeaponPersistentRepositoryBootstrap.EnsureInstance();
        if (weaponRepository == null)
            return false;

        int weaponInstanceIndex = weaponRepository.CreateWeapon(weaponTemplateKey);
        if (weaponInstanceIndex <= 0)
            return false;

        Debug.Log($"[DHWeaponInit] EnsureDefaultWeaponInstance unitIndex={unitIndex}, weaponTemplateKey='{weaponTemplateKey}', weaponInstanceIndex={weaponInstanceIndex}.", this);
        return EquipWeaponInstance(unitIndex, weaponInstanceIndex);
    }

    public void EnsureDefaultWeaponInstances()
    {
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData data = units[i];
            if (data == null)
                continue;

            EnsureDefaultWeaponInstance(data.UnitIndex);
        }
    }

    public bool SetSkillLevel(int unitIndex, int skillLevel)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            data.IngameStats,
            data.CurrentHp,
            data.Exp,
            data.MaxExp,
            skillLevel);
        return true;
    }

    public bool HealUnitToIngameMaxHp(int unitIndex, out float healedHp)
    {
        healedHp = 0f;

        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        float maxHp = Mathf.Max(0f, data.IngameStats.HP);
        healedHp = maxHp;

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            data.IngameStats,
            maxHp,
            data.Exp,
            data.MaxExp,
            data.SkillLevel,
            data.EquippedWeaponInstanceIndex,
            data.CurrentInfluence,
            false);
        return true;
    }

    // [KJ 260701] 의무실(Infirmary) 회복/부활 공용. percent = 최대HP 대비 회복 비율.
    // reviveIfDown=false → 순수 회복(incapacitated 유지) / true → 부활(incapacitated 해제).
    public bool HealUnitByPercent(int unitIndex, int percent, bool reviveIfDown, out float newHp)
    {
        newHp = 0f;
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        float maxHp = Mathf.Max(0f, data.IngameStats.HP);
        float heal = maxHp * Mathf.Max(0, percent) / 100f;
        newHp = Mathf.Clamp(data.CurrentHp + heal, 0f, maxHp);
        bool nextIncapacitated = reviveIfDown ? false : data.IsIncapacitated;

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            data.IngameStats,
            newHp,
            data.Exp,
            data.MaxExp,
            data.SkillLevel,
            data.EquippedWeaponInstanceIndex,
            data.CurrentInfluence,
            nextIncapacitated);
        return true;
    }

    public bool AddEventBonusStats(int unitIndex, float hpBonus, float atkBonus)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        StatBlock nextEventBonusStats = data.EventBonusStats;
        nextEventBonusStats.HP += hpBonus;
        nextEventBonusStats.Atk += atkBonus;

        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        StatBlock nextIngameStats = UnitStatCalculator.CalculateIngameStats(
            data.BaseStats,
            data.LevelupStats,
            data.Level,
            data.CurrentWeaponStats,
            nextEventBonusStats,
            unitGrowthTemplates);

        float hpRecovery = Mathf.Max(0f, hpBonus);
        float nextCurrentHp = Mathf.Clamp(data.CurrentHp + hpRecovery, 0f, Mathf.Max(0f, nextIngameStats.HP));
        data.SetEventBonusStats(nextEventBonusStats);
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            nextIngameStats,
            nextCurrentHp,
            data.Exp,
            data.MaxExp);
        return true;
    }

    public bool ApplyEventRewardStats(
        int unitIndex,
        float hpBonus,
        float atkBonus,
        float defBonus,
        float influenceDelta,
        float healAmount)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        StatBlock nextEventBonusStats = data.EventBonusStats;
        nextEventBonusStats.HP += hpBonus;
        nextEventBonusStats.Atk += atkBonus;
        nextEventBonusStats.DEF += defBonus;

        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        StatBlock nextIngameStats = UnitStatCalculator.CalculateIngameStats(
            data.BaseStats,
            data.LevelupStats,
            data.Level,
            data.CurrentWeaponStats,
            nextEventBonusStats,
            unitGrowthTemplates);

        float previousMaxHp = Mathf.Max(0f, data.IngameStats.HP);
        float nextMaxHp = Mathf.Max(0f, nextIngameStats.HP);
        float maxHpDelta = Mathf.Max(0f, nextMaxHp - previousMaxHp);
        float nextCurrentHp = Mathf.Clamp(data.CurrentHp + maxHpDelta + Mathf.Max(0f, healAmount), 0f, nextMaxHp);
        float nextCurrentInfluence = Mathf.Clamp(data.CurrentInfluence + influenceDelta, 0f, Mathf.Max(0f, nextIngameStats.Influence));

        data.SetEventBonusStats(nextEventBonusStats);
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            nextIngameStats,
            nextCurrentHp,
            data.Exp,
            data.MaxExp,
            data.SkillLevel,
            data.EquippedWeaponInstanceIndex,
            nextCurrentInfluence,
            nextCurrentHp <= 0f);
        return true;
    }

    public bool AddExp(int unitIndex, int amount)
    {
        if (amount <= 0)
            return false;

        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        RepairMaxExpIfNeeded(data);
        ApplyExpWithLevelUps(data, amount);
        return true;
    }

    public bool ApplyLevelUp(int unitIndex, int amount = 1)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return true;

        int nextLevel = Mathf.Max(1, data.Level + safeAmount);
        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        StatBlock nextIngameStats = UnitStatCalculator.CalculateIngameStats(
            data.BaseStats,
            data.LevelupStats,
            nextLevel,
            data.CurrentWeaponStats,
            data.EventBonusStats,
            unitGrowthTemplates);
        int nextMaxExp = ResolveMaxExp(nextLevel, unitGrowthTemplates);
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            nextLevel,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            nextIngameStats,
            nextIngameStats.HP,
            0,
            nextMaxExp);
        return true;
    }

    private static IReadOnlyList<DHUnitGrowthTemplate> ResolveUnitGrowthTemplates()
    {
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        return catalog != null ? catalog.GetUnitGrowthTemplates() : null;
    }

    private bool ApplyWeaponState(UnitPersistentData data, int weaponInstanceIndex, string weaponTemplateKey, EquipmentStatBlock weaponStats)
    {
        if (data == null)
            return false;

        int legacyWeaponTemplateKey = ResolveLegacyNumericWeaponTemplateKey(weaponTemplateKey);
        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        StatBlock nextIngameStats = UnitStatCalculator.CalculateIngameStats(
            data.BaseStats,
            data.LevelupStats,
            data.Level,
            weaponStats,
            data.EventBonusStats,
            unitGrowthTemplates);

        float nextCurrentHp = Mathf.Clamp(data.CurrentHp, 0f, Mathf.Max(0f, nextIngameStats.HP));
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            weaponTemplateKey,
            legacyWeaponTemplateKey,
            weaponStats,
            nextIngameStats,
            nextCurrentHp,
            data.Exp,
            data.MaxExp,
            data.SkillLevel,
            weaponInstanceIndex);
        return true;
    }

    private static bool TryResolveDefaultWeaponTemplateKey(UnitPersistentData data, out string weaponTemplateKey)
    {
        weaponTemplateKey = string.Empty;

        if (data == null)
            return false;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (!string.IsNullOrWhiteSpace(data.CurrentWeaponKey))
        {
            string currentWeaponKey = data.CurrentWeaponKey.Trim();
            if (catalog == null || HasWeaponTemplate(currentWeaponKey))
            {
                weaponTemplateKey = currentWeaponKey;
                return true;
            }
        }

        if (catalog == null)
        {
            weaponTemplateKey = "HC001";
            return true;
        }

        int classIndex = 0;
        if (catalog.TryGetPlayerUnitTemplate(data.UnitTemplateKey, out DHPlayerUnitTemplate playerTemplate) &&
            playerTemplate != null)
        {
            classIndex = playerTemplate.ClassIndex;
        }
        else
        {
            TryExtractNumericId(data.UnitTemplateKey, out classIndex);
        }

        List<DHWeaponTemplate> weapons = catalog.GetWeaponTemplatesByClassIndex(classIndex);
        DHWeaponTemplate bestWeapon = null;

        for (int i = 0; i < weapons.Count; i++)
        {
            DHWeaponTemplate weapon = weapons[i];
            if (weapon == null || weapon.NumericWeaponId <= 0)
                continue;

            if (GetWeaponTier(weapon.NumericWeaponId) != WeaponPersistentRepository.BaseWeaponLevel)
                continue;

            if (bestWeapon == null || weapon.NumericWeaponId < bestWeapon.NumericWeaponId)
                bestWeapon = weapon;
        }

        if (bestWeapon != null && !string.IsNullOrWhiteSpace(bestWeapon.WeaponKey))
        {
            weaponTemplateKey = bestWeapon.WeaponKey.Trim();
            return true;
        }

        weaponTemplateKey = "HC001";
        return true;
    }

    private static bool TryExtractNumericId(string rawKey, out int numericId)
    {
        numericId = 0;
        if (string.IsNullOrWhiteSpace(rawKey))
            return false;

        Match match = Regex.Match(rawKey.Trim(), @"\d+");
        return match.Success && int.TryParse(match.Value, out numericId) && numericId > 0;
    }

    private static int GetWeaponTier(int weaponIndex)
    {
        return Mathf.Abs(weaponIndex % 100);
    }

    private static bool HasWeaponTemplate(string weaponTemplateKey)
    {
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        return catalog != null &&
               !string.IsNullOrWhiteSpace(weaponTemplateKey) &&
               catalog.TryGetWeaponTemplate(weaponTemplateKey.Trim(), out DHWeaponTemplate template) &&
               template != null;
    }

    private static string ResolveWeaponTemplateKey(int legacyWeaponTemplateKey)
    {
        if (legacyWeaponTemplateKey <= 0)
            return string.Empty;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null &&
            catalog.TryGetWeaponTemplate(legacyWeaponTemplateKey, out DHWeaponTemplate template) &&
            template != null &&
            !string.IsNullOrWhiteSpace(template.WeaponKey))
        {
            return template.WeaponKey.Trim();
        }

        if (legacyWeaponTemplateKey >= 1 && legacyWeaponTemplateKey <= 999)
            return $"HC{legacyWeaponTemplateKey:000}";

        return string.Empty;
    }

    private static int ResolveLegacyNumericWeaponTemplateKey(string weaponTemplateKey)
    {
        if (string.IsNullOrWhiteSpace(weaponTemplateKey))
            return 0;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null &&
            catalog.TryGetWeaponTemplate(weaponTemplateKey.Trim(), out DHWeaponTemplate template) &&
            template != null)
        {
            return Mathf.Max(0, template.NumericWeaponId);
        }

        Match match = Regex.Match(weaponTemplateKey.Trim(), @"\d+");
        return match.Success && int.TryParse(match.Value, out int numericId) ? Mathf.Max(0, numericId) : 0;
    }

    private static int ResolveMaxExp(int level)
    {
        return ResolveMaxExp(level, ResolveUnitGrowthTemplates());
    }

    private static int ResolveMaxExp(int level, IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTable)
    {
        if (unitGrowthTable == null || unitGrowthTable.Count == 0)
            return 0;

        int safeLevel = Mathf.Max(1, level);
        int nextLevel = int.MaxValue;
        int nextExp = 0;

        for (int i = 0; i < unitGrowthTable.Count; i++)
        {
            DHUnitGrowthTemplate row = unitGrowthTable[i];
            if (row == null)
                continue;

            int rowLevel = row.Level;
            if (rowLevel <= safeLevel || rowLevel >= nextLevel)
                continue;

            nextLevel = rowLevel;
            nextExp = Mathf.Max(0, row.RequiredExperience);
        }

        return nextExp;
    }

    /// <summary>Repository 상태를 변경하지 않고 expAmount를 더했을 때 도달하는 레벨을 반환합니다.</summary>
    public static int SimulateFinalLevel(UnitPersistentData data, int expAmount)
    {
        if (data == null || expAmount <= 0)
            return data?.Level ?? 1;

        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        int level = Mathf.Max(1, data.Level);
        int exp = Mathf.Max(0, data.Exp) + expAmount;
        int maxExp = data.MaxExp > 0 ? data.MaxExp : ResolveMaxExp(level, unitGrowthTemplates);

        while (maxExp > 0 && exp >= maxExp)
        {
            exp -= maxExp;
            level++;
            maxExp = ResolveMaxExp(level, unitGrowthTemplates);
        }

        return level;
    }

    private static void ApplyExpWithLevelUps(UnitPersistentData data, int amount)
    {
        if (data == null || amount <= 0)
            return;

        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTemplates = ResolveUnitGrowthTemplates();
        int nextLevel = Mathf.Max(1, data.Level);
        int nextExp = Mathf.Max(0, data.Exp) + amount;
        int nextMaxExp = data.MaxExp > 0 ? data.MaxExp : ResolveMaxExp(nextLevel, unitGrowthTemplates);
        StatBlock nextIngameStats = data.IngameStats;
        float nextCurrentHp = data.CurrentHp;

        while (nextMaxExp > 0 && nextExp >= nextMaxExp)
        {
            nextExp -= nextMaxExp;
            nextLevel++;
            float previousMaxHp = Mathf.Max(0f, nextIngameStats.HP);
            nextIngameStats = UnitStatCalculator.CalculateIngameStats(
                data.BaseStats,
                data.LevelupStats,
                nextLevel,
                data.CurrentWeaponStats,
                data.EventBonusStats,
                unitGrowthTemplates);
            float nextMaxHp = Mathf.Max(0f, nextIngameStats.HP);
            float maxHpDelta = Mathf.Max(0f, nextMaxHp - previousMaxHp);
            nextCurrentHp = Mathf.Clamp(nextCurrentHp + maxHpDelta, 0f, nextMaxHp);
            nextMaxExp = ResolveMaxExp(nextLevel, unitGrowthTemplates);
        }

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            nextLevel,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            nextIngameStats,
            nextCurrentHp,
            nextExp,
            nextMaxExp);
    }

    private static void RepairMaxExpIfNeeded(UnitPersistentData data)
    {
        if (data == null || data.MaxExp > 0)
            return;

        int maxExp = ResolveMaxExp(data.Level);
        if (maxExp <= 0)
            return;

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponStats,
            data.IngameStats,
            data.CurrentHp,
            data.Exp,
            maxExp);
    }

    private void RepairAllMaxExpIfNeeded()
    {
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
            RepairMaxExpIfNeeded(units[i]);
    }

    public void RestoreFromSave(int restoredNextUnitIndex, IReadOnlyList<UnitPersistentDataDiskRow> savedUnits)
    {
        units.Clear();
        unitLookup.Clear();

        if (savedUnits != null)
        {
            for (int i = 0; i < savedUnits.Count; i++)
            {
                UnitPersistentDataDiskRow row = savedUnits[i];
                if (row == null)
                    continue;

                UnitPersistentData data = row.ToUnitPersistentData();
                if (data == null || data.UnitIndex <= 0 || unitLookup.ContainsKey(data.UnitIndex))
                    continue;

                units.Add(data);
                unitLookup.Add(data.UnitIndex, data);
            }
        }

        nextUnitIndex = Mathf.Max(1, restoredNextUnitIndex);
        RebuildLookup();
        EnsureDefaultWeaponInstances();
    }
    public void ClearAllUnits()
    {
        units.Clear();
        unitLookup.Clear();
        nextUnitIndex = 1;
    }

    private void RebuildLookup()
    {
        unitLookup.Clear();

        int highestIndex = 0;
        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData data = units[i];
            if (data == null)
                continue;

            int unitIndex = data.UnitIndex;
            if (unitIndex <= 0)
                continue;

            if (unitLookup.ContainsKey(unitIndex))
            {
                Debug.LogWarning($"PersistentUnitRepository has duplicate unitIndex '{unitIndex}'.", this);
                continue;
            }

            RepairMaxExpIfNeeded(data);
            unitLookup.Add(unitIndex, data);
            if (unitIndex > highestIndex)
                highestIndex = unitIndex;
        }

        if (nextUnitIndex <= highestIndex)
            nextUnitIndex = highestIndex + 1;
    }

    private const string DiskFileName = "persistent_units_repo.json";

    [System.Serializable]
    private class UnitRepositoryDiskPayload
    {
        public int nextUnitIndex;
        public UnitPersistentDataDiskRow[] units;
    }

    /// <summary>메모리상 유닛 목록을 Application.persistentDataPath JSON으로 저장합니다.</summary>
    public void SaveRuntimeStateToDisk()
    {
        try
        {
            UnitPersistentDataDiskRow[] rows;
            if (units == null || units.Count == 0)
                rows = System.Array.Empty<UnitPersistentDataDiskRow>();
            else
            {
                rows = new UnitPersistentDataDiskRow[units.Count];
                for (int i = 0; i < units.Count; i++)
                    rows[i] = UnitPersistentDataDiskRow.From(units[i]);
            }

            var payload = new UnitRepositoryDiskPayload
            {
                nextUnitIndex = nextUnitIndex,
                units = rows
            };

            string path = Path.Combine(Application.persistentDataPath, DiskFileName);
            File.WriteAllText(path, JsonUtility.ToJson(payload));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PersistentUnitRepository] SaveRuntimeStateToDisk 실패: {ex.Message}", this);
        }
    }
}
