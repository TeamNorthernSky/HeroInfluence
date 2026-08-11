using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the association resource SO tables and exposes immutable templates for read-only lookup.
/// Player state changes (resource spending, room levels, crafting) intentionally do not belong here.
/// </summary>
[DisallowMultipleComponent]
public sealed class AssociationResourceCatalog : MonoBehaviour
{
    private const char CompositeKeySeparator = '\u001f';

    public static AssociationResourceCatalog Instance { get; private set; }

    [Header("Association Resource Tables (V1.7)")]
    [SerializeField] private AssociationBuildDataTable buildTable;
    [SerializeField] private AssociationWorkshopDataTable workshopTable;
    [SerializeField] private AssociationWorkshopEnforceDataTable workshopEnforceTable;
    [SerializeField] private AssociationExchangeSheetDataTable exchangeTable;
    [SerializeField] private AssociationLabDataTable labTable;
    [SerializeField] private AssociationFundDataTable fundTable;
    [SerializeField] private AssociationPromotionDataTable promotionTable;
    [SerializeField] private AssociationTrainingDataTable trainingTable;

    [Header("Settings")]
    [SerializeField] private bool loadOnAwake = true;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly Dictionary<string, DHAssociationBuildTemplate> buildByExactKey
        = new Dictionary<string, DHAssociationBuildTemplate>();
    private readonly Dictionary<string, List<DHAssociationBuildTemplate>> buildByRoomAndLevel
        = new Dictionary<string, List<DHAssociationBuildTemplate>>();
    private readonly Dictionary<string, DHAssociationCraftTemplate> craftByWeaponKey
        = new Dictionary<string, DHAssociationCraftTemplate>();
    private readonly Dictionary<string, DHAssociationEnforceTemplate> enforceByWeaponAndLevel
        = new Dictionary<string, DHAssociationEnforceTemplate>();
    private readonly Dictionary<int, List<DHAssociationExchangeTemplate>> exchangesByRoomLevel
        = new Dictionary<int, List<DHAssociationExchangeTemplate>>();
    private readonly Dictionary<string, DHAssociationLabTemplate> labBySkillGroupAndLevel
        = new Dictionary<string, DHAssociationLabTemplate>();
    private readonly Dictionary<int, List<DHAssociationFundTemplate>> fundsByMainBaseLevel
        = new Dictionary<int, List<DHAssociationFundTemplate>>();
    private readonly Dictionary<int, List<DHAssociationPromotionTemplate>> promotionsByPublicityLevel
        = new Dictionary<int, List<DHAssociationPromotionTemplate>>();
    private readonly Dictionary<int, List<DHAssociationTrainingTemplate>> trainingsByRoomLevel
        = new Dictionary<int, List<DHAssociationTrainingTemplate>>();

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
            Reload();
    }

    [ContextMenu("Reload")]
    public void Reload()
    {
        ClearCache();

        LoadBuildTable();
        LoadWorkshopTable();
        LoadWorkshopEnforceTable();
        LoadExchangeTable();
        LoadLabTable();
        LoadFundTable();
        LoadPromotionTable();
        LoadTrainingTable();

        isLoaded = true;
        LogCatalogSummary();
    }

    public bool TryGetBuildTemplate(
        string roomType,
        int targetRoomLevel,
        int actionType,
        out DHAssociationBuildTemplate template)
    {
        EnsureLoaded();
        return buildByExactKey.TryGetValue(BuildExactKey(roomType, targetRoomLevel, actionType), out template);
    }

    public IReadOnlyList<DHAssociationBuildTemplate> GetBuildTemplates(string roomType, int targetRoomLevel)
    {
        EnsureLoaded();
        return buildByRoomAndLevel.TryGetValue(BuildRoomLevelKey(roomType, targetRoomLevel), out List<DHAssociationBuildTemplate> templates)
            ? new List<DHAssociationBuildTemplate>(templates)
            : new List<DHAssociationBuildTemplate>();
    }

    public bool TryGetCraftTemplate(string weaponKey, out DHAssociationCraftTemplate template)
    {
        EnsureLoaded();
        return craftByWeaponKey.TryGetValue(NormalizeKey(weaponKey), out template);
    }

    public bool TryGetEnforceTemplate(string weaponKey, int targetEnchantLevel, out DHAssociationEnforceTemplate template)
    {
        EnsureLoaded();
        return enforceByWeaponAndLevel.TryGetValue(WeaponLevelKey(weaponKey, targetEnchantLevel), out template);
    }

    public IReadOnlyList<DHAssociationExchangeTemplate> GetExchangeTemplates(int exchangeRoomLevel)
    {
        EnsureLoaded();
        return exchangesByRoomLevel.TryGetValue(exchangeRoomLevel, out List<DHAssociationExchangeTemplate> templates)
            ? new List<DHAssociationExchangeTemplate>(templates)
            : new List<DHAssociationExchangeTemplate>();
    }

    public bool TryGetLabTemplate(string skillGroup, int targetSkillLevel, out DHAssociationLabTemplate template)
    {
        EnsureLoaded();
        return labBySkillGroupAndLevel.TryGetValue(SkillLevelKey(skillGroup, targetSkillLevel), out template);
    }

    public IReadOnlyList<DHAssociationFundTemplate> GetFundTemplates(int mainBaseLevel)
    {
        EnsureLoaded();
        return fundsByMainBaseLevel.TryGetValue(mainBaseLevel, out List<DHAssociationFundTemplate> templates)
            ? new List<DHAssociationFundTemplate>(templates)
            : new List<DHAssociationFundTemplate>();
    }

    public IReadOnlyList<DHAssociationPromotionTemplate> GetPromotionTemplates(int publicityLevel)
    {
        EnsureLoaded();
        return promotionsByPublicityLevel.TryGetValue(publicityLevel, out List<DHAssociationPromotionTemplate> templates)
            ? new List<DHAssociationPromotionTemplate>(templates)
            : new List<DHAssociationPromotionTemplate>();
    }

    public IReadOnlyList<DHAssociationTrainingTemplate> GetTrainingTemplates(int trainingRoomLevel)
    {
        EnsureLoaded();
        return trainingsByRoomLevel.TryGetValue(trainingRoomLevel, out List<DHAssociationTrainingTemplate> templates)
            ? new List<DHAssociationTrainingTemplate>(templates)
            : new List<DHAssociationTrainingTemplate>();
    }

    private void LoadBuildTable()
    {
        if (buildTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Build table is not assigned.", this);
            return;
        }

        for (int i = 0; i < buildTable.DataList.Count; i++)
        {
            AssociationBuildData row = buildTable.DataList[i];
            if (row == null || string.IsNullOrWhiteSpace(row.room_type) || row.to_room_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid build row at index {i} skipped.", this);
                continue;
            }

            var template = new DHAssociationBuildTemplate(
                row.room_type,
                row.action_type,
                row.to_room_level,
                CreateCosts(row.cost_resource_1_type, row.cost_resource_1_amount, row.cost_resource_2_type, row.cost_resource_2_amount),
                row.room_action_condition,
                row.note);

            string exactKey = BuildExactKey(template.RoomType, template.TargetRoomLevel, template.ActionType);
            if (buildByExactKey.ContainsKey(exactKey))
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Duplicate build key '{exactKey}' skipped.", this);
                continue;
            }

            buildByExactKey.Add(exactKey, template);
            AddToList(buildByRoomAndLevel, BuildRoomLevelKey(template.RoomType, template.TargetRoomLevel), template);
        }
    }

    private void LoadWorkshopTable()
    {
        if (workshopTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Workshop table is not assigned.", this);
            return;
        }

        for (int i = 0; i < workshopTable.DataList.Count; i++)
        {
            AssociationWorkshopData row = workshopTable.DataList[i];
            if (row == null || row.required_workshop_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid workshop row at index {i} skipped.", this);
                continue;
            }

            List<string> weaponKeys = NormalizeWeaponKeys(row.weapon_index_list);
            if (weaponKeys.Count == 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Workshop row at index {i} has no weapon key and was skipped.", this);
                continue;
            }

            var template = new DHAssociationCraftTemplate(
                row.required_workshop_level,
                row.producible_equipment_group,
                weaponKeys,
                CreateCosts(row.cost_resource_1_type, row.cost_resource_1_amount, row.cost_resource_2_type, row.cost_resource_2_amount),
                row.note);

            for (int weaponIndex = 0; weaponIndex < weaponKeys.Count; weaponIndex++)
            {
                string weaponKey = weaponKeys[weaponIndex];
                if (craftByWeaponKey.ContainsKey(weaponKey))
                {
                    Debug.LogWarning($"[AssociationResourceCatalog] Duplicate craft weapon key '{weaponKey}' skipped.", this);
                    continue;
                }

                craftByWeaponKey.Add(weaponKey, template);
            }
        }
    }

    private void LoadWorkshopEnforceTable()
    {
        if (workshopEnforceTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Workshop enforce table is not assigned.", this);
            return;
        }

        for (int i = 0; i < workshopEnforceTable.DataList.Count; i++)
        {
            AssociationWorkshopEnforceData row = workshopEnforceTable.DataList[i];
            if (row == null || row.required_workshop_level < 0 || row.to_enchant_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid workshop enforce row at index {i} skipped.", this);
                continue;
            }

            List<string> weaponKeys = NormalizeWeaponKeys(row.weapon_index_list);
            if (weaponKeys.Count == 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Workshop enforce row at index {i} has no weapon key and was skipped.", this);
                continue;
            }

            var template = new DHAssociationEnforceTemplate(
                row.required_workshop_level,
                row.enchantable_equipment_group,
                weaponKeys,
                row.to_enchant_level,
                CreateCosts(row.cost_resource_1_type, row.cost_resource_1_amount, row.cost_resource_2_type, row.cost_resource_2_amount),
                row.enchant_condition,
                row.note);

            for (int weaponIndex = 0; weaponIndex < weaponKeys.Count; weaponIndex++)
            {
                string key = WeaponLevelKey(weaponKeys[weaponIndex], template.TargetEnchantLevel);
                if (enforceByWeaponAndLevel.ContainsKey(key))
                {
                    Debug.LogWarning($"[AssociationResourceCatalog] Duplicate enforce key '{key}' skipped.", this);
                    continue;
                }

                enforceByWeaponAndLevel.Add(key, template);
            }
        }
    }

    private void LoadExchangeTable()
    {
        if (exchangeTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Exchange table is not assigned.", this);
            return;
        }

        for (int i = 0; i < exchangeTable.DataList.Count; i++)
        {
            AssociationExchangeSheetData row = exchangeTable.DataList[i];
            if (row == null || row.exchange_room_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid exchange row at index {i} skipped.", this);
                continue;
            }

            var template = new DHAssociationExchangeTemplate(
                row.exchange_room_level,
                CreateSingleResource(row.cost_resource_type, row.cost_resource_amount, "exchange cost", i),
                CreateSingleResource(row.get_resource_type, row.get_resource_amount, "exchange reward", i),
                row.note);
            AddToList(exchangesByRoomLevel, template.ExchangeRoomLevel, template);
        }
    }

    private void LoadLabTable()
    {
        if (labTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Lab table is not assigned.", this);
            return;
        }

        for (int i = 0; i < labTable.DataList.Count; i++)
        {
            AssociationLabData row = labTable.DataList[i];
            if (row == null || string.IsNullOrWhiteSpace(row.upgradable_skill_group) ||
                row.required_lab_level < 0 || row.to_skill_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid lab row at index {i} skipped.", this);
                continue;
            }

            var template = new DHAssociationLabTemplate(
                row.required_lab_level,
                row.upgradable_skill_group,
                row.to_skill_level,
                CreateCosts(row.cost_resource_1_type, row.cost_resource_1_amount, row.cost_resource_2_type, row.cost_resource_2_amount),
                row.skill_upgrade_condition,
                row.note);

            string key = SkillLevelKey(template.UpgradableSkillGroup, template.TargetSkillLevel);
            if (labBySkillGroupAndLevel.ContainsKey(key))
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Duplicate lab key '{key}' skipped.", this);
                continue;
            }

            labBySkillGroupAndLevel.Add(key, template);
        }
    }

    private void LoadFundTable()
    {
        if (fundTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Fund table is not assigned.", this);
            return;
        }

        for (int i = 0; i < fundTable.DataList.Count; i++)
        {
            AssociationFundData row = fundTable.DataList[i];
            if (row == null || row.main_base_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid fund row at index {i} skipped.", this);
                continue;
            }

            var template = new DHAssociationFundTemplate(
                row.main_base_level,
                row.resource_type,
                row.resource_amount,
                row.resource_cycle,
                row.note);
            AddToList(fundsByMainBaseLevel, template.MainBaseLevel, template);
        }
    }

    private void LoadPromotionTable()
    {
        if (promotionTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Promotion table is not assigned.", this);
            return;
        }

        for (int i = 0; i < promotionTable.DataList.Count; i++)
        {
            AssociationPromotionData row = promotionTable.DataList[i];
            if (row == null || row.publicity_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid promotion row at index {i} skipped.", this);
                continue;
            }

            var template = new DHAssociationPromotionTemplate(
                row.publicity_level,
                CreateSingleResource(row.cost_resource_type, row.cost_resource_amount, "promotion cost", i),
                row.status_type,
                row.ip_amount_per_charge,
                row.ip_charge_limit,
                row.ip_charge_cycle,
                row.note);
            AddToList(promotionsByPublicityLevel, template.PublicityLevel, template);
        }
    }

    private void LoadTrainingTable()
    {
        if (trainingTable == null)
        {
            Debug.LogWarning("[AssociationResourceCatalog] Training table is not assigned.", this);
            return;
        }

        for (int i = 0; i < trainingTable.DataList.Count; i++)
        {
            AssociationTrainingData row = trainingTable.DataList[i];
            if (row == null || row.required_training_room_level < 0)
            {
                Debug.LogWarning($"[AssociationResourceCatalog] Invalid training row at index {i} skipped.", this);
                continue;
            }

            var template = new DHAssociationTrainingTemplate(
                row.required_training_room_level,
                row.training_name,
                CreateSingleResource(row.cost_resource_type, row.cost_resource_amount, "training cost", i),
                row.status_type,
                row.status_amount_per_training,
                row.training_count_per_unit,
                row.training_condition,
                row.note);
            AddToList(trainingsByRoomLevel, template.RequiredTrainingRoomLevel, template);
        }
    }

    private List<DHAssociationResourceCost> CreateCosts(
        int resource1Type,
        int resource1Amount,
        int resource2Type,
        int resource2Amount)
    {
        var costs = new List<DHAssociationResourceCost>(2);
        AddCost(costs, resource1Type, resource1Amount);
        AddCost(costs, resource2Type, resource2Amount);
        return costs;
    }

    private void AddCost(List<DHAssociationResourceCost> costs, int resourceType, int amount)
    {
        if (amount <= 0)
            return;

        if (resourceType < 0)
        {
            Debug.LogWarning($"[AssociationResourceCatalog] Invalid negative resource type {resourceType} skipped.", this);
            return;
        }

        costs.Add(new DHAssociationResourceCost(resourceType, amount));
    }

    private DHAssociationResourceCost CreateSingleResource(int resourceType, int amount, string fieldName, int rowIndex)
    {
        if (resourceType < 0)
        {
            Debug.LogWarning($"[AssociationResourceCatalog] Invalid {fieldName} resource type at row {rowIndex}.", this);
            return null;
        }

        if (amount < 0)
        {
            Debug.LogWarning($"[AssociationResourceCatalog] Invalid {fieldName} amount at row {rowIndex}.", this);
            return null;
        }

        return new DHAssociationResourceCost(resourceType, amount);
    }

    private static List<string> NormalizeWeaponKeys(IEnumerable<string> rawKeys)
    {
        var keys = new List<string>();
        if (rawKeys == null)
            return keys;

        foreach (string rawKey in rawKeys)
        {
            string key = NormalizeKey(rawKey);
            if (string.IsNullOrEmpty(key) || keys.Contains(key))
                continue;

            keys.Add(key);
        }

        return keys;
    }

    private static void AddToList<T>(Dictionary<int, List<T>> lookup, int key, T value)
    {
        if (!lookup.TryGetValue(key, out List<T> list))
            lookup.Add(key, list = new List<T>());

        list.Add(value);
    }

    private static void AddToList<T>(Dictionary<string, List<T>> lookup, string key, T value)
    {
        if (!lookup.TryGetValue(key, out List<T> list))
            lookup.Add(key, list = new List<T>());

        list.Add(value);
    }

    private void ClearCache()
    {
        buildByExactKey.Clear();
        buildByRoomAndLevel.Clear();
        craftByWeaponKey.Clear();
        enforceByWeaponAndLevel.Clear();
        exchangesByRoomLevel.Clear();
        labBySkillGroupAndLevel.Clear();
        fundsByMainBaseLevel.Clear();
        promotionsByPublicityLevel.Clear();
        trainingsByRoomLevel.Clear();
        isLoaded = false;
    }

    private void EnsureLoaded()
    {
        if (!isLoaded)
            Reload();
    }

    private void LogCatalogSummary()
    {
        Debug.Log(
            $"[AssociationResourceCatalog] Loaded " +
            $"Build {buildByExactKey.Count}, " +
            $"Craft {craftByWeaponKey.Count}, " +
            $"Enforce {enforceByWeaponAndLevel.Count}, " +
            $"Exchange {CountEntries(exchangesByRoomLevel)}, " +
            $"Lab {labBySkillGroupAndLevel.Count}, " +
            $"Fund {CountEntries(fundsByMainBaseLevel)}, " +
            $"Promotion {CountEntries(promotionsByPublicityLevel)}, " +
            $"Training {CountEntries(trainingsByRoomLevel)}.",
            this);
    }

    private static int CountEntries<T>(Dictionary<int, List<T>> lookup)
    {
        int count = 0;
        foreach (KeyValuePair<int, List<T>> pair in lookup)
            count += pair.Value.Count;
        return count;
    }

    private static string BuildExactKey(string roomType, int targetRoomLevel, int actionType)
    {
        return BuildRoomLevelKey(roomType, targetRoomLevel) + CompositeKeySeparator + actionType;
    }

    private static string BuildRoomLevelKey(string roomType, int targetRoomLevel)
    {
        return NormalizeKey(roomType) + CompositeKeySeparator + targetRoomLevel;
    }

    private static string WeaponLevelKey(string weaponKey, int targetLevel)
    {
        return NormalizeKey(weaponKey) + CompositeKeySeparator + targetLevel;
    }

    private static string SkillLevelKey(string skillGroup, int targetLevel)
    {
        return NormalizeKey(skillGroup) + CompositeKeySeparator + targetLevel;
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
