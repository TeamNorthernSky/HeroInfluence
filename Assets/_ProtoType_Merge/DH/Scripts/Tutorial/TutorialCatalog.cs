using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public class TutorialCatalog : MonoBehaviour
{
    private const float DefaultPlayerBaseInfluence = 100f;

    [Header("Tutorial SO DataTables")]
    [SerializeField] private PlayerUnitDataTable playerUnitDataTable;
    [SerializeField] private EnemyUnitDataTable enemyUnitDataTable;
    [SerializeField] private PlayerWeaponDataTable playerWeaponDataTable;
    [SerializeField] private ClassSkillDataTable classSkillDataTable;
    [SerializeField] private UnitGrowthExpDataTable unitGrowthExpDataTable;
    [SerializeField] private EnemyGroupDataTable enemyGroupDataTable;

    [Header("Settings")]
    [SerializeField] private bool loadOnAwake = true;

    [Header("Debug")]
    [SerializeField] private List<string> loadedPlayerKeys = new List<string>();
    [SerializeField] private List<string> loadedEnemyKeys = new List<string>();
    [SerializeField] private List<string> loadedWeaponKeys = new List<string>();
    [SerializeField] private List<string> loadedSkillKeys = new List<string>();
    [SerializeField] private List<string> loadedEnemyGroupKeys = new List<string>();

    private readonly Dictionary<string, DHPlayerUnitTemplate> playerUnitLookup = new Dictionary<string, DHPlayerUnitTemplate>();
    private readonly Dictionary<string, DHEnemyUnitTemplate> enemyUnitLookup = new Dictionary<string, DHEnemyUnitTemplate>();
    private readonly Dictionary<string, DHWeaponTemplate> weaponLookup = new Dictionary<string, DHWeaponTemplate>();
    private readonly Dictionary<string, DHClassSkillTemplate> classSkillLookup = new Dictionary<string, DHClassSkillTemplate>();
    private readonly Dictionary<int, DHClassSkillTemplate> classSkillByNumericId = new Dictionary<int, DHClassSkillTemplate>();
    private readonly Dictionary<string, SkillData> skillDataLookup = new Dictionary<string, SkillData>();
    private readonly Dictionary<int, SkillData> skillDataByNumericId = new Dictionary<int, SkillData>();
    private readonly Dictionary<string, DHEnemyGroupTemplate> enemyGroupLookup = new Dictionary<string, DHEnemyGroupTemplate>();
    private readonly List<DHUnitGrowthTemplate> unitGrowthTemplates = new List<DHUnitGrowthTemplate>();

    private static readonly int[] GrowthSkillClassIndices = { 10001, 10002, 10003, 10004 };
    private bool isLoaded;

    public bool IsLoaded => isLoaded;
    public IReadOnlyList<DHUnitGrowthTemplate> UnitGrowthTemplates => unitGrowthTemplates;

    private void Awake()
    {
        if (loadOnAwake)
            Reload();
    }

    [ContextMenu("Reload Tutorial Catalog")]
    public void Reload()
    {
        Clear();
        LoadPlayers();
        LoadEnemies();
        LoadClassSkills();
        LoadWeapons();
        LoadGrowth();
        LoadEnemyGroups();
        isLoaded = true;
    }

    public bool TryGetPlayerUnitTemplate(string unitKey, out DHPlayerUnitTemplate template)
    {
        EnsureLoaded();
        return playerUnitLookup.TryGetValue(NormalizeNumericTemplateKey(unitKey), out template) && template != null;
    }

    public bool TryGetEnemyUnitTemplate(string enemyKey, out DHEnemyUnitTemplate template)
    {
        EnsureLoaded();
        return enemyUnitLookup.TryGetValue(NormalizeNumericTemplateKey(enemyKey), out template) && template != null;
    }

    public bool TryGetWeaponTemplate(string weaponKey, out DHWeaponTemplate template)
    {
        EnsureLoaded();
        string key = string.IsNullOrWhiteSpace(weaponKey) ? string.Empty : weaponKey.Trim();
        return weaponLookup.TryGetValue(key, out template) && template != null;
    }

    public bool TryGetClassSkillTemplate(string skillKey, out DHClassSkillTemplate template)
    {
        EnsureLoaded();
        string key = string.IsNullOrWhiteSpace(skillKey) ? string.Empty : skillKey.Trim();
        return classSkillLookup.TryGetValue(key, out template) && template != null;
    }

    public bool TryGetClassSkillTemplate(int skillIndex, out DHClassSkillTemplate template)
    {
        EnsureLoaded();
        return classSkillByNumericId.TryGetValue(skillIndex, out template) && template != null;
    }

    public SkillData GetSkillTemplate(string skillKey)
    {
        EnsureLoaded();
        string key = string.IsNullOrWhiteSpace(skillKey) ? string.Empty : skillKey.Trim();
        return skillDataLookup.TryGetValue(key, out SkillData data) ? data : null;
    }

    public SkillData GetSkillTemplate(int skillIndex)
    {
        EnsureLoaded();
        return skillDataByNumericId.TryGetValue(skillIndex, out SkillData data) ? data : null;
    }

    public bool TryGetEnemyGroupTemplate(string groupKey, out DHEnemyGroupTemplate group)
    {
        EnsureLoaded();
        string key = string.IsNullOrWhiteSpace(groupKey) ? string.Empty : groupKey.Trim();
        return enemyGroupLookup.TryGetValue(key, out group) && group != null;
    }

    public IReadOnlyList<DHUnitGrowthTemplate> GetUnitGrowthTemplates()
    {
        EnsureLoaded();
        return unitGrowthTemplates;
    }

    private void LoadPlayers()
    {
        if (playerUnitDataTable == null)
            return;

        for (int i = 0; i < playerUnitDataTable.DataList.Count; i++)
        {
            DHPlayerUnitTemplate template = ConvertPlayerUnitTemplate(playerUnitDataTable.DataList[i]);
            if (template == null || string.IsNullOrWhiteSpace(template.UnitKey))
                continue;

            string key = NormalizeNumericTemplateKey(template.UnitKey);
            if (playerUnitLookup.ContainsKey(key))
                continue;

            playerUnitLookup.Add(key, template);
            loadedPlayerKeys.Add(key);
        }
    }

    private void LoadEnemies()
    {
        if (enemyUnitDataTable == null)
            return;

        for (int i = 0; i < enemyUnitDataTable.DataList.Count; i++)
        {
            EnemyUnitData row = enemyUnitDataTable.DataList[i];
            DHEnemyUnitTemplate template = ConvertEnemyUnitTemplate(row);
            if (template == null || string.IsNullOrWhiteSpace(template.EnemyKey))
                continue;

            string key = NormalizeNumericTemplateKey(template.EnemyKey);
            if (enemyUnitLookup.ContainsKey(key))
                continue;

            enemyUnitLookup.Add(key, template);
            loadedEnemyKeys.Add(key);
            TryAddEnemySkill(row, 1);
            TryAddEnemySkill(row, 2);
        }
    }

    private void LoadClassSkills()
    {
        if (classSkillDataTable == null)
            return;

        for (int i = 0; i < classSkillDataTable.DataList.Count; i++)
        {
            DHClassSkillTemplate template = ConvertClassSkillTemplate(classSkillDataTable.DataList[i]);
            if (template == null || string.IsNullOrWhiteSpace(template.SkillKey))
                continue;

            string key = template.SkillKey;
            if (classSkillLookup.ContainsKey(key))
                continue;

            classSkillLookup.Add(key, template);
            if (template.NumericSkillId > 0)
                classSkillByNumericId[template.NumericSkillId] = template;

            SkillData skill = ConvertClassSkill(template);
            if (skill != null)
            {
                if (!string.IsNullOrWhiteSpace(skill.skillKey))
                    skillDataLookup[skill.skillKey] = skill;

                if (skill.skillIndex > 0)
                    skillDataByNumericId[skill.skillIndex] = skill;
            }

            loadedSkillKeys.Add(key);
        }
    }

    private void LoadWeapons()
    {
        if (playerWeaponDataTable == null)
            return;

        for (int i = 0; i < playerWeaponDataTable.DataList.Count; i++)
        {
            DHWeaponTemplate template = ConvertWeaponTemplate(playerWeaponDataTable.DataList[i]);
            if (template == null || string.IsNullOrWhiteSpace(template.WeaponKey))
                continue;

            if (weaponLookup.ContainsKey(template.WeaponKey))
                continue;

            weaponLookup.Add(template.WeaponKey, template);
            loadedWeaponKeys.Add(template.WeaponKey);
        }
    }

    private void LoadGrowth()
    {
        if (unitGrowthExpDataTable == null)
            return;

        for (int i = 0; i < unitGrowthExpDataTable.DataList.Count; i++)
        {
            DHUnitGrowthTemplate template = ConvertUnitGrowthTemplate(unitGrowthExpDataTable.DataList[i]);
            if (template != null)
                unitGrowthTemplates.Add(template);
        }
    }

    private void LoadEnemyGroups()
    {
        if (enemyGroupDataTable == null)
            return;

        for (int i = 0; i < enemyGroupDataTable.DataList.Count; i++)
        {
            DHEnemyGroupTemplate template = ConvertEnemyGroup(enemyGroupDataTable.DataList[i]);
            if (template == null || string.IsNullOrWhiteSpace(template.GroupKey))
                continue;

            if (enemyGroupLookup.ContainsKey(template.GroupKey))
                continue;

            enemyGroupLookup.Add(template.GroupKey, template);
            loadedEnemyGroupKeys.Add(template.GroupKey);
        }
    }

    private void TryAddEnemySkill(EnemyUnitData src, int slot)
    {
        if (src == null)
            return;

        switch (slot)
        {
            case 1:
                TryAddEnemySkill(src.EnemyIndex, src.EnemyName, slot, src.EnemySkill1_Name, src.EnemySkill1_Description,
                    src.EnemySkill1Effect, src.EnemySkill1Range, src.EnemySkill1RangeLine, src.EnemySkill1Target,
                    src.EnemySkill1MultiTarget, src.EnemySkill1_MultiTargetType, src.EnemySkill1_MultiTargetCount,
                    src.EnemySkill1Value, src.EnemySkill1SubValue);
                break;
            case 2:
                TryAddEnemySkill(src.EnemyIndex, src.EnemyName, slot, src.EnemySkill2_Name, src.EnemySkill2_Description,
                    src.EnemySkill2Effect, src.EnemySkill2Range, src.EnemySkill2RangeLine, src.EnemySkill2Target,
                    src.EnemySkill2MultiTarget, src.EnemySkill2_MultiTargetType, src.EnemySkill2_MultiTargetCount,
                    src.EnemySkill2Value, src.EnemySkill2SubValue);
                break;
        }
    }

    private void TryAddEnemySkill(
        string enemyIndex,
        string enemyName,
        int slot,
        string skillName,
        string description,
        int effect,
        int range,
        int rangeLine,
        int target,
        List<int> multiTarget,
        int multiTargetType,
        int multiTargetCount,
        float value,
        float subValue)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return;

        int numericEnemyId = ExtractNumericId(enemyIndex);
        if (numericEnemyId <= 0)
            return;

        int skillIndex = (numericEnemyId * 10) + slot;
        string skillKey = EnemySkillKeyRules.Compose(enemyIndex, slot);
        if (skillDataByNumericId.ContainsKey(skillIndex) || skillDataLookup.ContainsKey(skillKey))
            return;

        SkillData skill = new SkillData
        {
            skillIndex = skillIndex,
            skillKey = skillKey,
            category = SkillCategory.Enemy,
            slot = slot,
            skillClass = enemyName,
            acquireLevel = 1,
            skillName = skillName,
            description = description,
            ipCost = 0,
            classSkillEffect = effect,
            classSkillRange = range,
            EnemySkill1Range = slot == 1 ? range : -1,
            EnemySkill2Range = slot == 2 ? range : -1,
            classSkillRangeLine = rangeLine,
            classSkillTarget = target,
            boundary = multiTarget != null ? new List<int>(multiTarget) : new List<int>(),
            multiTargetType = multiTargetType,
            multiTargetCount = multiTargetCount,
            skillValue = value,
            skillSubValue = subValue,
            AnimationTrigger = "Attack"
        };

        skillDataByNumericId.Add(skillIndex, skill);
        if (!string.IsNullOrWhiteSpace(skillKey))
        {
            skillDataLookup.Add(skillKey, skill);
            loadedSkillKeys.Add(skillKey);
        }
    }

    private void EnsureLoaded()
    {
        if (!isLoaded)
            Reload();
    }

    private void Clear()
    {
        playerUnitLookup.Clear();
        enemyUnitLookup.Clear();
        weaponLookup.Clear();
        classSkillLookup.Clear();
        classSkillByNumericId.Clear();
        skillDataLookup.Clear();
        skillDataByNumericId.Clear();
        enemyGroupLookup.Clear();
        unitGrowthTemplates.Clear();
        loadedPlayerKeys.Clear();
        loadedEnemyKeys.Clear();
        loadedWeaponKeys.Clear();
        loadedSkillKeys.Clear();
        loadedEnemyGroupKeys.Clear();
        isLoaded = false;
    }

    private static DHPlayerUnitTemplate ConvertPlayerUnitTemplate(PlayerUnitData src)
    {
        if (src == null)
            return null;

        int classIndex = ExtractNumericId(src.ClassIndex);
        return new DHPlayerUnitTemplate(
            classIndex > 0 ? classIndex.ToString() : src.ClassIndex.ToString(),
            classIndex,
            src.UnitName,
            src.ClassName,
            src.ClassConcept,
            new StatBlock(
                hp: src.UnitMaxHP,
                atk: src.UnitATK,
                def: src.UnitDEF,
                luck: 0f,
                speed: src.Speed,
                criticalRate: src.CriticalRate,
                critMultiplier: 1.5f,
                counterRate: src.CounterRate,
                avoidRate: src.ReduceRate,
                influence: DefaultPlayerBaseInfluence),
            new StatBlock(
                hp: src.LevelGrowthMaxHP,
                atk: src.LevelGrowthMaxAtk,
                def: src.LevelGrowthMaxDef,
                luck: 0f,
                speed: 0f),
            ToIntIndexList(src.ClassSkillIndexList),
            ToIntIndexList(src.WeaponIndexList));
    }

    private static DHEnemyUnitTemplate ConvertEnemyUnitTemplate(EnemyUnitData src)
    {
        if (src == null)
            return null;

        int enemyIndex = ExtractNumericId(src.EnemyIndex);
        string enemyKey = enemyIndex > 0 ? enemyIndex.ToString() : (src.EnemyIndex ?? string.Empty).Trim();
        return new DHEnemyUnitTemplate(
            enemyKey,
            enemyIndex,
            src.EnemyName,
            src.EnemyConcept,
            src.UnitAI,
            Mathf.RoundToInt(src.ExperiencePoint),
            new StatBlock(
                hp: src.UnitMaxHP,
                atk: src.UnitATK,
                def: src.UnitDEF,
                luck: 0f,
                speed: src.Speed,
                criticalRate: src.CriticalRate,
                critMultiplier: 1.5f,
                counterRate: src.CounterRate,
                avoidRate: src.ReduceRate),
            new StatBlock(
                hp: src.LevelGrowthMaxHP,
                atk: src.LevelGrowthAtk,
                def: src.LevelGrowthDef,
                luck: 0f,
                speed: 0f));
    }

    private static DHWeaponTemplate ConvertWeaponTemplate(PlayerWeaponData src)
    {
        if (src == null)
            return null;

        int weaponIndex = ExtractNumericId(src.WeaponIndex);
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

    private static DHClassSkillTemplate ConvertClassSkillTemplate(ClassSkillData src)
    {
        if (src == null)
            return null;

        int skillIndex = ExtractNumericId(src.ClassSkillIndex);
        string skillKey = string.IsNullOrWhiteSpace(src.ClassSkillIndex) ? string.Empty : src.ClassSkillIndex.Trim();
        return new DHClassSkillTemplate(
            skillKey,
            skillIndex,
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

    private static DHUnitGrowthTemplate ConvertUnitGrowthTemplate(UnitGrowthExpData src)
    {
        if (src == null)
            return null;

        Dictionary<int, int> studySkillByClassIndex = new Dictionary<int, int>(GrowthSkillClassIndices.Length);
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

    private static DHEnemyGroupTemplate ConvertEnemyGroup(EnemyGroupData src)
    {
        if (src == null)
            return null;

        string groupKey = string.IsNullOrWhiteSpace(src.EnemyIndex) ? string.Empty : src.EnemyIndex.Trim();
        if (string.IsNullOrEmpty(groupKey))
            return null;

        DHEnemyGroupTemplate template = new DHEnemyGroupTemplate
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
            return null;
        }

        return template.Members.Count > 0 ? template : null;
    }

    private static SkillData ConvertClassSkill(DHClassSkillTemplate src)
    {
        if (src == null)
            return null;

        return new SkillData
        {
            skillIndex = src.NumericSkillId,
            skillKey = src.SkillKey,
            category = SkillCategory.Class,
            skillClass = src.ClassName,
            acquireLevel = src.AcquireLevel,
            skillName = src.SkillName,
            description = src.Description,
            ipCost = src.IpCost,
            classSkillEffect = src.Effect,
            classSkillRange = src.Range,
            classSkillRangeLine = src.RangeLine,
            classSkillTarget = src.Target,
            boundary = src.Boundary != null ? new List<int>(src.Boundary) : new List<int>(),
            multiTargetType = src.MultiTargetType,
            multiTargetCount = src.MultiTargetCount,
            skillValue = src.ValueLv1,
            skillSubValue = src.SubValueLv1,
            AnimationTrigger = src.AnimationTrigger
        };
    }

    private static bool TryAddEnemyGroupMember(DHEnemyGroupTemplate template, string enemyUnitCode, int combatSlot)
    {
        int enemyUnitIndex = ExtractNumericId(enemyUnitCode);
        if (enemyUnitIndex <= 0)
            return true;

        if (combatSlot < 1 || combatSlot > 6)
            return false;

        for (int i = 0; i < template.Members.Count; i++)
        {
            if (template.Members[i].CombatSlot == combatSlot)
                return false;
        }

        template.Members.Add(new DHEnemyGroupMember(enemyUnitIndex, combatSlot));
        return true;
    }

    private static void AddStudySkill(Dictionary<int, int> destination, int classIndex, string rawSkillKey)
    {
        if (destination == null || classIndex <= 0)
            return;

        int skillIndex = ExtractNumericId(rawSkillKey);
        if (skillIndex > 0)
            destination[classIndex] = skillIndex;
    }

    private static List<int> ToIntIndexList(IReadOnlyList<string> source)
    {
        List<int> result = new List<int>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            int index = ExtractNumericId(source[i]);
            if (index > 0)
                result.Add(index);
        }

        return result;
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

    private static int ExtractNumericId(int code)
    {
        return code;
    }

    private static int ExtractNumericId(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return 0;

        Match match = Regex.Match(code, @"\d+");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    private static string NormalizeNumericTemplateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        int numericId = ExtractNumericId(key);
        return numericId > 0 ? numericId.ToString() : key.Trim();
    }
}
