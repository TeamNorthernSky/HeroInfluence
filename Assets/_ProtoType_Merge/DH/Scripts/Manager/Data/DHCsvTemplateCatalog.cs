using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DHCsvTemplateCatalog : MonoBehaviour
{
    public static DHCsvTemplateCatalog Instance { get; private set; }

    [Header("CSV Source (기존)")]
    [Tooltip("씬의 CSVDataLoad. 비어 있으면 같은 GameObject 또는 씬에서 자동 탐색합니다.")]
    [SerializeField] private CSVDataLoad csvDataLoad;

    [Header("SO DataTables (Excel Importer)")]
    [SerializeField] private PlayerUnitDataTable    playerUnitTable;
    [SerializeField] private EnemyUnitDataTable     enemyUnitTable;
    [SerializeField] private PlayerWeaponDataTable  weaponTable;
    [SerializeField] private ClassSkillDataTable    classSkillTable;
    [SerializeField] private UnitGrowthPerLevelDataTable levelUpTable;

    [Header("Settings")]
    [SerializeField] private bool useSOTables      = false;
    [SerializeField] private bool loadOnAwake      = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private List<UnitData>   cachedPlayerTemplates = new List<UnitData>();
    [SerializeField] private List<EnemyData>  cachedEnemyTemplates  = new List<EnemyData>();
    [SerializeField] private List<WeaponData> cachedWeapons         = new List<WeaponData>();

    private readonly Dictionary<string, UnitData>   playerTemplateLookup = new Dictionary<string, UnitData>();
    private readonly Dictionary<string, EnemyData>  enemyTemplateLookup  = new Dictionary<string, EnemyData>();
    private readonly Dictionary<int,    WeaponData> weaponLookup         = new Dictionary<int, WeaponData>();
    private readonly Dictionary<int,    SkillData>  skillTemplates       = new Dictionary<int, SkillData>();
    private readonly List<LevelUpData>              levelUpTemplates     = new List<LevelUpData>();
    private bool isLoaded;

    public bool IsLoaded => isLoaded;

    // ─────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────

    [ContextMenu("Reload Templates")]
    public void ReloadTemplates()
    {
        ClearCache();

        if (useSOTables)
            ReloadFromSOTables();
        else
            ReloadFromCSV();
    }

    public bool TryGetPlayerTemplate(string unitTemplateKey, out UnitData template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey)) { template = null; return false; }
        return playerTemplateLookup.TryGetValue(unitTemplateKey, out template);
    }

    public bool TryGetEnemyTemplate(string unitTemplateKey, out EnemyData template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey)) { template = null; return false; }
        return enemyTemplateLookup.TryGetValue(unitTemplateKey, out template);
    }

    public bool TryGetWeapon(int weaponIndex, out WeaponData weaponData)
    {
        EnsureLoaded();
        if (weaponIndex <= 0) { weaponData = null; return false; }
        return weaponLookup.TryGetValue(weaponIndex, out weaponData);
    }

    public bool TryGetWeaponStats(int weaponIndex, out EquipmentStatBlock equipmentStats)
    {
        if (TryGetWeapon(weaponIndex, out WeaponData weaponData))
        {
            equipmentStats = EquipmentStatBlock.FromWeaponData(weaponData);
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

    public IReadOnlyList<LevelUpData> GetLevelUpTemplates()
    {
        EnsureLoaded();
        return levelUpTemplates;
    }

    // ─────────────────────────────────────────────────────────
    // SO DataTable 로드 경로
    // ─────────────────────────────────────────────────────────

    private void ReloadFromSOTables()
    {
        // ── 플레이어 유닛 ──────────────────────────────────────
        if (playerUnitTable != null)
        {
            for (int i = 0; i < playerUnitTable.DataList.Count; i++)
            {
                UnitData unit = ConvertPlayerUnit(playerUnitTable.DataList[i]);
                if (unit == null || string.IsNullOrWhiteSpace(unit.Index)) continue;

                if (playerTemplateLookup.ContainsKey(unit.Index))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 플레이어 키 '{unit.Index}' 건너뜀.", this);
                    continue;
                }
                playerTemplateLookup.Add(unit.Index, unit);
                cachedPlayerTemplates.Add(unit);
            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] playerUnitTable이 할당되지 않았습니다.", this);
        }

        // ── 적 유닛 + 적 스킬 ─────────────────────────────────
        if (enemyUnitTable != null)
        {
            for (int i = 0; i < enemyUnitTable.DataList.Count; i++)
            {
                EnemyUnitData src = enemyUnitTable.DataList[i];
                EnemyData enemy = ConvertEnemyUnit(src);
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.Index)) continue;

                if (enemyTemplateLookup.ContainsKey(enemy.Index))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 적 키 '{enemy.Index}' 건너뜀.", this);
                    continue;
                }
                enemyTemplateLookup.Add(enemy.Index, enemy);
                cachedEnemyTemplates.Add(enemy);

                // 적 내장 스킬 추출
                TryAddEnemySkill(src, slot: 1);
                TryAddEnemySkill(src, slot: 2);
            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] enemyUnitTable이 할당되지 않았습니다.", this);
        }

        // ── 직업 스킬 ──────────────────────────────────────────
        if (classSkillTable != null)
        {
            for (int i = 0; i < classSkillTable.DataList.Count; i++)
            {
                SkillData skill = ConvertClassSkill(classSkillTable.DataList[i]);
                if (skill == null) continue;

                if (skillTemplates.ContainsKey(skill.skillIndex))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 스킬 인덱스 {skill.skillIndex} 건너뜀.", this);
                    continue;
                }
                skillTemplates.Add(skill.skillIndex, skill);
            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] classSkillTable이 할당되지 않았습니다.", this);
        }

        // ── 무기 ───────────────────────────────────────────────
        if (weaponTable != null)
        {
            for (int i = 0; i < weaponTable.DataList.Count; i++)
            {
                WeaponData weapon = ConvertWeapon(weaponTable.DataList[i]);
                if (weapon == null || weapon.WeaponIndex <= 0) continue;

                if (weaponLookup.ContainsKey(weapon.WeaponIndex))
                {
                    Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 무기 인덱스 {weapon.WeaponIndex} 건너뜀.", this);
                    continue;
                }
                weaponLookup.Add(weapon.WeaponIndex, weapon);
                cachedWeapons.Add(weapon);
            }
        }
        else
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] weaponTable이 할당되지 않았습니다.", this);
        }

        // ── 레벨업 ─────────────────────────────────────────────
        if (levelUpTable != null)
        {
            for (int i = 0; i < levelUpTable.DataList.Count; i++)
            {
                LevelUpData row = ConvertLevelUp(levelUpTable.DataList[i]);
                if (row != null) levelUpTemplates.Add(row);
            }
        }

        isLoaded = true;
        Debug.Log($"[DHCsvTemplateCatalog][SO] 플레이어 {cachedPlayerTemplates.Count}건, " +
                  $"적 {cachedEnemyTemplates.Count}건, " +
                  $"무기 {cachedWeapons.Count}건, " +
                  $"스킬 {skillTemplates.Count}건, " +
                  $"레벨업 {levelUpTemplates.Count}건 로드 완료.", this);
    }

    // ─────────────────────────────────────────────────────────
    // SO → 기존 타입 변환 메서드
    // ─────────────────────────────────────────────────────────

    private static UnitData ConvertPlayerUnit(PlayerUnitData src)
    {
        if (src == null) return null;
        return new UnitData
        {
            Index    = src.ClassIndex.ToString(),
            UnitType = src.ClassName,
            Name     = src.UnitName,
            baseStats = new StatBlock(
                hp:           src.UnitMaxHP,
                atk:          src.UnitATK,
                def:          src.UnitDEF,
                luck:         0f,
                speed:        src.Speed,
                criticalRate: src.CriticalRate,
                critMultiplier: 1.5f,
                counterRate:  src.CounterRate,
                avoidRate:    src.ReduceRate),
            levelupStats = new StatBlock(
                hp:  src.LevelGrowthMaxHP,
                atk: src.LevelGrowthMaxAtk,
                def: src.LevelGrowthMaxDef,
                luck: 0f, speed: 0f),
            IsEnemyRow = false
        };
    }

    private static EnemyData ConvertEnemyUnit(EnemyUnitData src)
    {
        if (src == null) return null;
        return new EnemyData
        {
            Index    = src.EnemyIndex.ToString(),
            UnitType = string.Empty,
            Name     = src.EnemyName,
            baseStats = new StatBlock(
                hp:           src.UnitMaxHP,
                atk:          src.UnitATK,
                def:          src.UnitDEF,
                luck:         0f,
                speed:        src.Speed,
                criticalRate: src.CriticalRate,
                critMultiplier: 1.5f,
                counterRate:  src.CounterRate,
                avoidRate:    src.ReduceRate),
            IsEnemyRow      = true,
            UnitAI          = src.UnitAI,
            ExperiencePoint = src.ExperiencePoint
        };
    }

    private void TryAddEnemySkill(EnemyUnitData src, int slot)
    {
        string skillName = slot == 1 ? src.EnemySkill1_Name : src.EnemySkill2_Name;
        if (string.IsNullOrWhiteSpace(skillName)) return;

        int skillIndex = (src.EnemyIndex * 10) + slot;
        if (skillTemplates.ContainsKey(skillIndex))
        {
            Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 적 스킬 인덱스 {skillIndex} 건너뜀.", this);
            return;
        }

        SkillData skill;
        if (slot == 1)
        {
            skill = new SkillData
            {
                skillIndex      = skillIndex,
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
                skillIndex      = skillIndex,
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
    }

    private static WeaponData ConvertWeapon(PlayerWeaponData src)
    {
        if (src == null) return null;
        return new WeaponData
        {
            WeaponIndex          = src.WeaponIndex,
            weaponClass          = src.Class,
            WeaponName           = src.WeaponName,
            WeaponDescription    = src.WaeponDescription,
            // Lv1 스탯을 기본값으로 사용
            BonusHP              = src.BonusMaxHPLv1,
            BonusATK             = src.BonusATKLv1,
            BonusDEF             = src.BonusDEFLv1,
            BonusCriticalRate    = src.BonusCriticalRate,
            BonusCounterRate     = src.BonusCounterRate,
            BonusReduceRate      = src.BonusReduceRate,
            BonusSpeed           = src.BonusSpeed,
            WeaponSkillIndex     = src.WeaponSkillIndex,
            WeaponSkillName      = src.WeaponSkillName,
            WeaponSkillDescription = src.WeaponSkillDescription,
            IPCost               = src.IPCost,
            WeaponSkillEffect    = src.WeaponSkillEffect,
            WeaponSkillRange     = src.WeaponSkillRange,
            WeaponSkillRangeLine = src.WeaponSkillRangeLine,
            WeaponSkillTarget    = src.WeaponSkillTarget,
            WeaponSkillMultiTarget    = new List<int>(src.WeaponSkillMultiTarget ?? new List<int>()),
            WeaponSkillMultiTargetType  = src.WeaponSkill_MultiTargetType,
            WeaponSkillMultiTargetCount = src.WeaponSkillMultiTargetCount,
            WeaponSkillValue     = src.WeaponSkillValueLv1,
            WeaponSkillSubValue  = src.WeaponSkillSubValueLv1
        };
    }

    private static SkillData ConvertClassSkill(ClassSkillData src)
    {
        if (src == null) return null;
        return new SkillData
        {
            skillIndex          = src.ClassSkillIndex,
            skillClass          = src.Class,
            acquireLevel        = src.ClassSkill_AcquireRank,
            skillName           = src.ClassSkillName,
            description         = src.ClassSkillDescription,
            ipCost              = src.IPCost,
            classSkillEffect    = src.ClassSkillEffect,
            classSkillRange     = src.ClassSkillRange,
            classSkillRangeLine = src.ClassSkillRangeLine,
            classSkillTarget    = src.ClassSkillTarget,
            boundary            = new List<int>(src.ClassSkillMultiTarget ?? new List<int>()),
            multiTargetCount    = src.ClassSkill_MultiTargetCount,
            skillValue          = src.ClassSkillValueLv1,
            skillSubValue       = src.ClassSkillSubValueLv1,
            AnimationTrigger    = "Attack"
        };
    }

    private static LevelUpData ConvertLevelUp(UnitGrowthPerLevelData src)
    {
        if (src == null) return null;
        return new LevelUpData
        {
            level        = src.Level,
            Rank         = string.IsNullOrEmpty(src.Rank) ? '\0' : src.Rank[0],
            expPerLevel  = src.NeedExpieriencePoint,
            MaxIP        = src.AddIP
        };
    }

    // ─────────────────────────────────────────────────────────
    // CSV 로드 경로 (기존 코드 그대로 유지)
    // ─────────────────────────────────────────────────────────

    private void ReloadFromCSV()
    {
        if (!TryResolveCsvDataLoad())
        {
            Debug.LogError("[DHCsvTemplateCatalog] CSVDataLoad를 찾을 수 없습니다. 씬에 CSVDataLoad를 두거나 인스펙터에 할당하세요.", this);
            return;
        }

        TextAsset effectivePlayer = csvDataLoad.GetEffectivePlayerUnitCsv();
        TextAsset enemyTa  = csvDataLoad.GetEnemyUnitCsv();
        TextAsset weaponTa = csvDataLoad.GetWeaponSheetCsv();

        // 플레이어
        List<UnitData> playerTemplates = LoadPlayerUnitsForCatalog(effectivePlayer, enemyTa);
        for (int i = 0; i < playerTemplates.Count; i++)
        {
            UnitData template = playerTemplates[i];
            if (template == null || string.IsNullOrWhiteSpace(template.Index)) continue;
            if (playerTemplateLookup.ContainsKey(template.Index))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 플레이어 템플릿 키 '{template.Index}'를 건너뜁니다.", this);
                continue;
            }
            playerTemplateLookup.Add(template.Index, template);
            cachedPlayerTemplates.Add(template);
        }

        // 적
        EnemyCsvLoadResult enemyLoadResult = LoadEnemyCsvForCatalog(effectivePlayer, enemyTa);
        List<EnemyData> enemyTemplates = enemyLoadResult != null ? enemyLoadResult.Enemies : new List<EnemyData>();
        for (int i = 0; i < enemyTemplates.Count; i++)
        {
            EnemyData template = enemyTemplates[i];
            if (template == null || string.IsNullOrWhiteSpace(template.Index)) continue;
            if (enemyTemplateLookup.ContainsKey(template.Index))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 적 템플릿 키 '{template.Index}'를 건너뜁니다.", this);
                continue;
            }
            enemyTemplateLookup.Add(template.Index, template);
            cachedEnemyTemplates.Add(template);
        }

        CacheClassSkillsIntoSkillTemplates(csvDataLoad.GetClassSkillSheetCsv());
        MergeEnemySkillsIntoSkillTemplates(enemyLoadResult);

        TextAsset levelUpTa = csvDataLoad.GetLevelUpSheetCsv();
        if (levelUpTa != null)
            levelUpTemplates.AddRange(CSVLoader.LoadLevelUpData(levelUpTa.text));

        List<WeaponData> weapons = LoadWeaponsForCatalog(weaponTa);
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponData weapon = weapons[i];
            if (weapon == null || weapon.WeaponIndex <= 0) continue;
            if (weaponLookup.ContainsKey(weapon.WeaponIndex))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 장비 템플릿 키 '{weapon.WeaponIndex}'를 건너뜁니다.", this);
                continue;
            }
            weaponLookup.Add(weapon.WeaponIndex, weapon);
            cachedWeapons.Add(weapon);
        }

        isLoaded = true;
        Debug.Log($"[DHCsvTemplateCatalog][CSV] 플레이어 {cachedPlayerTemplates.Count}건, " +
                  $"적 {cachedEnemyTemplates.Count}건, " +
                  $"무기 {cachedWeapons.Count}건, " +
                  $"스킬 {skillTemplates.Count}건, " +
                  $"레벨업 {levelUpTemplates.Count}건 로드 완료.", this);
    }

    // ─────────────────────────────────────────────────────────
    // 공통 유틸
    // ─────────────────────────────────────────────────────────

    private void ClearCache()
    {
        playerTemplateLookup.Clear();
        enemyTemplateLookup.Clear();
        weaponLookup.Clear();
        skillTemplates.Clear();
        levelUpTemplates.Clear();
        cachedPlayerTemplates.Clear();
        cachedEnemyTemplates.Clear();
        cachedWeapons.Clear();
        isLoaded = false;
    }

    private void EnsureLoaded()
    {
        if (!isLoaded) ReloadTemplates();
    }

    private bool TryResolveCsvDataLoad()
    {
        if (csvDataLoad != null) return true;
        csvDataLoad = GetComponent<CSVDataLoad>();
        if (csvDataLoad != null) return true;
        csvDataLoad = Object.FindObjectOfType<CSVDataLoad>(true);
        return csvDataLoad != null;
    }

    private void CacheClassSkillsIntoSkillTemplates(TextAsset classSkillSheet)
    {
        if (classSkillSheet == null) return;
        List<SkillData> rows = CSVLoader.LoadClassSkillData(classSkillSheet.text);
        for (int i = 0; i < rows.Count; i++)
        {
            SkillData skill = rows[i];
            if (skill == null) continue;
            if (skillTemplates.ContainsKey(skill.skillIndex))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 직업 스킬 중복 skillIndex={skill.skillIndex}를 건너뜁니다.", this);
                continue;
            }
            skillTemplates.Add(skill.skillIndex, skill);
        }
    }

    private void MergeEnemySkillsIntoSkillTemplates(EnemyCsvLoadResult enemyLoadResult)
    {
        if (enemyLoadResult?.Skills == null) return;
        List<SkillData> skills = enemyLoadResult.Skills;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill == null) continue;
            if (skillTemplates.ContainsKey(skill.skillIndex))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 적 스킬 skillIndex={skill.skillIndex}는 이미 등록되어 있어 병합에서 건너뜁니다.", this);
                continue;
            }
            skillTemplates.Add(skill.skillIndex, skill);
        }
    }

    private List<UnitData> LoadPlayerUnitsForCatalog(TextAsset playerUnitCsvAsset, TextAsset enemyUnitCsvAsset)
    {
        if (playerUnitCsvAsset == null)
        {
            Debug.LogError("[DHCsvTemplateCatalog] 유효 플레이어 유닛 CSV가 비어 있습니다.", this);
            return new List<UnitData>();
        }
        List<UnitData> allUnits = CSVLoader.LoadUnitData(playerUnitCsvAsset.text);
        if (enemyUnitCsvAsset != null) return allUnits;

        List<UnitData> players = new List<UnitData>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            if (allUnits[i] == null || CSVLoader.IsEnemyUnitRow(allUnits[i])) continue;
            players.Add(allUnits[i]);
        }
        return players;
    }

    private EnemyCsvLoadResult LoadEnemyCsvForCatalog(TextAsset playerUnitCsvAsset, TextAsset enemyUnitCsvAsset)
    {
        if (enemyUnitCsvAsset != null)
            return CSVLoader.LoadEnemyDataAndSkills(enemyUnitCsvAsset.text);

        if (playerUnitCsvAsset == null)
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] enemyUnitCsv와 유효 플레이어 유닛 CSV가 모두 비어 있습니다.", this);
            return new EnemyCsvLoadResult();
        }

        List<UnitData> allUnits = CSVLoader.LoadUnitData(playerUnitCsvAsset.text);
        List<EnemyData> enemies = new List<EnemyData>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            if (allUnits[i] == null || !CSVLoader.IsEnemyUnitRow(allUnits[i])) continue;
            enemies.Add(CSVLoader.UnitDataToEnemyData(allUnits[i], string.Empty));
        }
        return new EnemyCsvLoadResult { Enemies = enemies, Skills = new List<SkillData>() };
    }

    private List<WeaponData> LoadWeaponsForCatalog(TextAsset weaponSheetCsvAsset)
    {
        if (weaponSheetCsvAsset == null)
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] weaponSheetCsv가 비어 있습니다.", this);
            return new List<WeaponData>();
        }
        return CSVLoader.LoadWeaponData(weaponSheetCsvAsset.text);
    }
}
