using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DHCsvTemplateCatalog : MonoBehaviour
{
    public static DHCsvTemplateCatalog Instance { get; private set; }

    [Header("Catalog")]
    [Tooltip("씬의 CSVDataLoad. 비어 있으면 같은 GameObject 또는 씬에서 자동 탐색합니다.")]
    [SerializeField] private CSVDataLoad csvDataLoad;
    [SerializeField] private bool loadOnAwake = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private List<UnitData> cachedPlayerTemplates = new List<UnitData>();
    [SerializeField] private List<EnemyData> cachedEnemyTemplates = new List<EnemyData>();
    [SerializeField] private List<WeaponData> cachedWeapons = new List<WeaponData>();

    private readonly Dictionary<string, UnitData> playerTemplateLookup = new Dictionary<string, UnitData>();
    private readonly Dictionary<string, EnemyData> enemyTemplateLookup = new Dictionary<string, EnemyData>();
    private readonly Dictionary<int, WeaponData> weaponLookup = new Dictionary<int, WeaponData>();
    private readonly Dictionary<int, SkillData> skillTemplates = new Dictionary<int, SkillData>();
    private readonly List<LevelUpData> levelUpTemplates = new List<LevelUpData>();
    private bool isLoaded;

    public bool IsLoaded => isLoaded;

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

    [ContextMenu("Reload Templates")]
    public void ReloadTemplates()
    {
        if (!TryResolveCsvDataLoad())
        {
            Debug.LogError("[DHCsvTemplateCatalog] CSVDataLoad를 찾을 수 없습니다. 씬에 CSVDataLoad를 두거나 인스펙터에 할당하세요.", this);
            return;
        }

        playerTemplateLookup.Clear();
        enemyTemplateLookup.Clear();
        weaponLookup.Clear();
        skillTemplates.Clear();
        levelUpTemplates.Clear();
        cachedPlayerTemplates.Clear();
        cachedEnemyTemplates.Clear();
        cachedWeapons.Clear();
        isLoaded = false;

        TextAsset effectivePlayer = csvDataLoad.GetEffectivePlayerUnitCsv();
        TextAsset enemyTa = csvDataLoad.GetEnemyUnitCsv();
        TextAsset weaponTa = csvDataLoad.GetWeaponSheetCsv();

        List<UnitData> playerTemplates = LoadPlayerUnitsForCatalog(effectivePlayer, enemyTa);
        for (int i = 0; i < playerTemplates.Count; i++)
        {
            UnitData template = playerTemplates[i];
            if (template == null || string.IsNullOrWhiteSpace(template.Index))
                continue;

            if (playerTemplateLookup.ContainsKey(template.Index))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 플레이어 템플릿 키 '{template.Index}'를 건너뜁니다.", this);
                continue;
            }

            playerTemplateLookup.Add(template.Index, template);
            cachedPlayerTemplates.Add(template);
        }

        EnemyCsvLoadResult enemyLoadResult = LoadEnemyCsvForCatalog(effectivePlayer, enemyTa);
        List<EnemyData> enemyTemplates = enemyLoadResult != null ? enemyLoadResult.Enemies : new List<EnemyData>();
        for (int i = 0; i < enemyTemplates.Count; i++)
        {
            EnemyData template = enemyTemplates[i];
            if (template == null || string.IsNullOrWhiteSpace(template.Index))
                continue;

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
            if (weapon == null || weapon.WeaponIndex <= 0)
                continue;

            if (weaponLookup.ContainsKey(weapon.WeaponIndex))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 중복 장비 템플릿 키 '{weapon.WeaponIndex}'를 건너뜁니다.", this);
                continue;
            }

            weaponLookup.Add(weapon.WeaponIndex, weapon);
            cachedWeapons.Add(weapon);
        }

        isLoaded = true;
        Debug.Log($"[DHCsvTemplateCatalog] 플레이어 {cachedPlayerTemplates.Count}건, 적 {cachedEnemyTemplates.Count}건, 장비 {cachedWeapons.Count}건, 스킬 {skillTemplates.Count}건, 레벨업 {levelUpTemplates.Count}건을 로드했습니다.", this);
    }

    private bool TryResolveCsvDataLoad()
    {
        if (csvDataLoad != null)
            return true;

        csvDataLoad = GetComponent<CSVDataLoad>();
        if (csvDataLoad != null)
            return true;

        csvDataLoad = Object.FindObjectOfType<CSVDataLoad>(true);
        return csvDataLoad != null;
    }

    private void CacheClassSkillsIntoSkillTemplates(TextAsset classSkillSheet)
    {
        if (classSkillSheet == null)
            return;

        List<SkillData> rows = CSVLoader.LoadClassSkillData(classSkillSheet.text);
        for (int i = 0; i < rows.Count; i++)
        {
            SkillData skill = rows[i];
            if (skill == null)
                continue;

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
        if (enemyLoadResult == null || enemyLoadResult.Skills == null)
            return;

        List<SkillData> skills = enemyLoadResult.Skills;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill == null)
                continue;

            if (skillTemplates.ContainsKey(skill.skillIndex))
            {
                Debug.LogWarning($"[DHCsvTemplateCatalog] 적 스킬 skillIndex={skill.skillIndex}는 이미 등록되어 있어 병합에서 건너뜁니다.", this);
                continue;
            }

            skillTemplates.Add(skill.skillIndex, skill);
        }
    }

    /// <summary>CSVDataLoad.LoadPlayerUnits와 동일한 규칙으로 CSVLoader를 호출합니다.</summary>
    private List<UnitData> LoadPlayerUnitsForCatalog(TextAsset playerUnitCsvAsset, TextAsset enemyUnitCsvAsset)
    {
        if (playerUnitCsvAsset == null)
        {
            Debug.LogError("[DHCsvTemplateCatalog] 유효 플레이어 유닛 CSV(playerUnitCsv 또는 unitCsv)가 비어 있습니다.", this);
            return new List<UnitData>();
        }

        List<UnitData> allUnits = CSVLoader.LoadUnitData(playerUnitCsvAsset.text);
        if (enemyUnitCsvAsset != null)
            return allUnits;

        List<UnitData> players = new List<UnitData>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitData unit = allUnits[i];
            if (unit == null || CSVLoader.IsEnemyUnitRow(unit))
                continue;

            players.Add(unit);
        }

        return players;
    }

    /// <summary>CSVDataLoad.LoadEnemyCsv와 동일한 규칙으로 CSVLoader를 호출합니다.</summary>
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
            UnitData unit = allUnits[i];
            if (unit == null || !CSVLoader.IsEnemyUnitRow(unit))
                continue;

            enemies.Add(CSVLoader.UnitDataToEnemyData(unit, string.Empty));
        }

        return new EnemyCsvLoadResult
        {
            Enemies = enemies,
            Skills = new List<SkillData>()
        };
    }

    /// <summary>CSVDataLoad.LoadWeapons와 동일하게 CSVLoader를 호출합니다.</summary>
    private List<WeaponData> LoadWeaponsForCatalog(TextAsset weaponSheetCsvAsset)
    {
        if (weaponSheetCsvAsset == null)
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] weaponSheetCsv가 비어 있습니다.", this);
            return new List<WeaponData>();
        }

        return CSVLoader.LoadWeaponData(weaponSheetCsvAsset.text);
    }

    public bool TryGetPlayerTemplate(string unitTemplateKey, out UnitData template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey))
        {
            template = null;
            return false;
        }

        return playerTemplateLookup.TryGetValue(unitTemplateKey, out template);
    }

    public bool TryGetEnemyTemplate(string unitTemplateKey, out EnemyData template)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(unitTemplateKey))
        {
            template = null;
            return false;
        }

        return enemyTemplateLookup.TryGetValue(unitTemplateKey, out template);
    }

    public bool TryGetWeapon(int weaponIndex, out WeaponData weaponData)
    {
        EnsureLoaded();
        if (weaponIndex <= 0)
        {
            weaponData = null;
            return false;
        }

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

    /// <summary>skillIndex로 직업/적 병합 스킬 템플릿을 조회합니다. 없으면 null.</summary>
    public SkillData GetSkillTemplate(int index)
    {
        EnsureLoaded();
        return skillTemplates.TryGetValue(index, out SkillData data) ? data : null;
    }

    /// <summary>레벨업 시트에서 로드한 행 목록(복사본이 아닌 내부 리스트 읽기 전용 뷰).</summary>
    public IReadOnlyList<LevelUpData> GetLevelUpTemplates()
    {
        EnsureLoaded();
        return levelUpTemplates;
    }

    private void EnsureLoaded()
    {
        if (!isLoaded)
            ReloadTemplates();
    }
}
