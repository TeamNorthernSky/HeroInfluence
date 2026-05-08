using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DHCsvTemplateCatalog : MonoBehaviour
{
    public static DHCsvTemplateCatalog Instance { get; private set; }

    [Header("Catalog")]
    [SerializeField] private DHCsvDataLoad csvDataLoad;
    [SerializeField] private bool loadOnAwake = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private List<UnitData> cachedPlayerTemplates = new List<UnitData>();
    [SerializeField] private List<EnemyData> cachedEnemyTemplates = new List<EnemyData>();
    [SerializeField] private List<WeaponData> cachedWeapons = new List<WeaponData>();

    private readonly Dictionary<string, UnitData> playerTemplateLookup = new Dictionary<string, UnitData>();
    private readonly Dictionary<string, EnemyData> enemyTemplateLookup = new Dictionary<string, EnemyData>();
    private readonly Dictionary<int, WeaponData> weaponLookup = new Dictionary<int, WeaponData>();
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
        if (csvDataLoad == null)
            csvDataLoad = GetComponent<DHCsvDataLoad>();

        playerTemplateLookup.Clear();
        enemyTemplateLookup.Clear();
        weaponLookup.Clear();
        cachedPlayerTemplates.Clear();
        cachedEnemyTemplates.Clear();
        cachedWeapons.Clear();
        isLoaded = false;

        if (csvDataLoad == null)
        {
            Debug.LogWarning("[DHCsvTemplateCatalog] DHCsvDataLoad 참조가 없습니다.", this);
            return;
        }

        List<UnitData> playerTemplates = csvDataLoad.LoadPlayerUnits();
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

        EnemyCsvLoadResult enemyLoadResult = csvDataLoad.LoadEnemyCsv();
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

        List<WeaponData> weapons = csvDataLoad.LoadWeapons();
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
        Debug.Log($"[DHCsvTemplateCatalog] 플레이어 템플릿 {cachedPlayerTemplates.Count}건, 적 템플릿 {cachedEnemyTemplates.Count}건, 장비 템플릿 {cachedWeapons.Count}건을 로드했습니다.", this);
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

    private void EnsureLoaded()
    {
        if (!isLoaded)
            ReloadTemplates();
    }
}
