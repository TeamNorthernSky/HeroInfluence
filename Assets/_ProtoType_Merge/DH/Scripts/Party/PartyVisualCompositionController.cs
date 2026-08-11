using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyIdentity))]
[RequireComponent(typeof(PartyComposition))]
public class PartyVisualCompositionController : MonoBehaviour
{
    private const int ExplorationSlotCount = 4;
    private const string JusticeTemplateKey = "10001";
    private const string RuminaTemplateKey = "10002";
    private const string BlackBulletTemplateKey = "10003";
    private const string NekomingTemplateKey = "10004";

    private static readonly string[] ExplorationTemplateOrder =
    {
        JusticeTemplateKey,
        RuminaTemplateKey,
        BlackBulletTemplateKey,
        NekomingTemplateKey
    };

    [Header("References")]
    [SerializeField] private Transform unitRoot;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;

    [Header("Startup")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private bool createInitialPartyWhenMissing = true;
    [SerializeField] private string[] initialUnitTemplateKeys = new string[ExplorationSlotCount];
    [SerializeField, Min(0f)] private float initialCurrentInfluence = 0f;

    [Header("Debug")]
    [SerializeField] private bool logWarnings = true;

    private PartyIdentity partyIdentity;
    private PartyComposition partyComposition;

    private void Awake()
    {
        ResolveLocalReferences();
    }

    private void Start()
    {
        if (!Application.isPlaying || !rebuildOnStart)
            return;

        RebuildFromRepository();
    }

    [ContextMenu("Rebuild From Repository")]
    public void RebuildFromRepository()
    {
        ResolveLocalReferences();

        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        if (partyIdentity == null || partyComposition == null || unitRepository == null || partyRepository == null)
            return;

        string partyId = string.IsNullOrWhiteSpace(partyIdentity.PartyId) ? gameObject.name : partyIdentity.PartyId;
        EnsureInitialPartyData(partyId, unitRepository, partyRepository);
        unitRepository.EnsureDefaultWeaponInstances();

        if (!partyRepository.TryGetParty(partyId, out PartyPersistentData partyData) || partyData == null)
        {
            ClearVisualUnits();
            SetCompositionSlots(new int[ExplorationSlotCount]);
            return;
        }

        int[] explorationIndices = BuildExplorationIndices(partyData, unitRepository);
        SetCompositionSlots(partyData.UnitIndices);
        RebuildVisualUnits(explorationIndices, unitRepository);
        LogInitialEquipmentSnapshot(partyData, unitRepository);
    }

    private void ResolveLocalReferences()
    {
        if (partyIdentity == null)
            partyIdentity = GetComponent<PartyIdentity>();

        if (partyComposition == null)
            partyComposition = GetComponent<PartyComposition>();

        if (unitRoot == null)
            unitRoot = transform.Find("Units");

        if (unitRoot == null)
        {
            GameObject units = new GameObject("Units");
            unitRoot = units.transform;
            unitRoot.SetParent(transform, false);
        }

        if (prefabRegistry == null)
            prefabRegistry = FindObjectOfType<LevelPrefabRegistry>(true);
    }

    private void EnsureInitialPartyData(string partyId, PersistentUnitRepository unitRepository, PartyPersistentRepository partyRepository)
    {
        if (!createInitialPartyWhenMissing || partyRepository.ContainsParty(partyId))
            return;

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
            return;

        int[] unitIndices = new int[ExplorationSlotCount];
        for (int i = 0; i < ExplorationSlotCount; i++)
        {
            string templateKey = GetInitialTemplateKey(i);
            if (string.IsNullOrWhiteSpace(templateKey))
                continue;

            if (!templateCatalog.TryGetPlayerUnitTemplate(templateKey, out DHPlayerUnitTemplate template) || template == null)
            {
                Warn($"Could not resolve initial player template '{templateKey}'.");
                continue;
            }

            string weaponKey = ResolveInitialCurrentWeaponKey(template.UnitKey);
            EquipmentStatBlock weaponStats = ResolveInitialCurrentWeaponStats(weaponKey);
            Debug.Log($"[DHWeaponInit] Initial unit '{template.UnitKey}' resolved weaponKey='{weaponKey}'.", this);
            unitIndices[i] = unitRepository.CreateUnit(
                template.UnitKey,
                1,
                template.BaseStats,
                template.LevelupStats,
                ResolveInitialCurrentSkillIndex(template.UnitKey),
                weaponKey,
                weaponStats,
                initialCurrentInfluence);
        }

        // [JC 260628] 기본 진형 명시. 전투 슬롯 규약: 1-3=후열, 4-6=전열.
        // initialUnitTemplateKeys = [10001,10002,10003,10004]이고 10004=아쿠아블루(후열) 이므로
        // 10001/02/03 → 전열(슬롯4·5·6), 10004(아쿠아) → 후열(슬롯1). (슬롯 미전달 시 자동 [1,2,3,4]가
        // 전열3+후열1로 어긋나 출전/전력평가 진형이 잘못 표시되던 문제 교정.)
        int[] unitSlots = { 5, 1, 3, 2 };
        partyRepository.RegisterOrUpdateParty(partyId, unitIndices, unitSlots);
    }

    private int ResolveInitialCurrentSkillIndex(string unitTemplateKey)
    {
        return TryResolvePlayerUnitPrefab(unitTemplateKey, out PartyUnitState prefab) && prefab != null
            ? prefab.CurrentSkillIndex
            : 0;
    }

    private string ResolveInitialCurrentWeaponKey(string unitTemplateKey)
    {
        if (TryResolvePlayerUnitPrefab(unitTemplateKey, out PartyUnitState prefab) &&
            !string.IsNullOrWhiteSpace(prefab.CurrentWeaponKey))
        {
            return prefab.CurrentWeaponKey;
        }

        return "HC001";
    }

    private bool TryResolvePlayerUnitPrefab(string unitTemplateKey, out PartyUnitState prefab)
    {
        if (prefabRegistry != null &&
            prefabRegistry.TryGetPlayerUnitPrefab(unitTemplateKey, out prefab) &&
            prefab != null)
        {
            return true;
        }

        LevelPrefabRegistry[] registries = FindObjectsByType<LevelPrefabRegistry>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < registries.Length; i++)
        {
            LevelPrefabRegistry candidate = registries[i];
            if (candidate == null ||
                !candidate.TryGetPlayerUnitPrefab(unitTemplateKey, out prefab) ||
                prefab == null)
            {
                continue;
            }

            prefabRegistry = candidate;
            return true;
        }

        prefab = null;
        return false;
    }

    private static EquipmentStatBlock ResolveInitialCurrentWeaponStats(string weaponKey)
    {
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null &&
            !string.IsNullOrWhiteSpace(weaponKey) &&
            catalog.TryGetWeaponTemplate(weaponKey, out DHWeaponTemplate template) &&
            template != null)
        {
            return EquipmentStatBlock.FromStatBlock(template.GetBonusStatsAtLevel(WeaponPersistentRepository.BaseWeaponLevel));
        }

        return default;
    }

    private string GetInitialTemplateKey(int index)
    {
        if (initialUnitTemplateKeys == null || index < 0 || index >= initialUnitTemplateKeys.Length)
            return string.Empty;

        return string.IsNullOrWhiteSpace(initialUnitTemplateKeys[index]) ? string.Empty : initialUnitTemplateKeys[index].Trim();
    }

    private void LogInitialEquipmentSnapshot(PartyPersistentData partyData, PersistentUnitRepository unitRepository)
    {
        if (partyData == null || unitRepository == null)
            return;

        WeaponPersistentRepository weaponRepository = WeaponPersistentRepository.Instance;
        IReadOnlyList<int> unitIndices = partyData.UnitIndices;
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (!unitRepository.TryGetUnit(unitIndex, out UnitPersistentData unitData) || unitData == null)
                continue;

            string equippedWeaponKey = string.Empty;
            int equippedWeaponIndex = unitData.EquippedWeaponInstanceIndex;
            if (weaponRepository != null &&
                equippedWeaponIndex > 0 &&
                weaponRepository.TryGetWeaponTemplateKey(equippedWeaponIndex, out string resolvedWeaponKey))
            {
                equippedWeaponKey = resolvedWeaponKey;
            }

            Debug.Log(
                $"[DHWeaponInit] Snapshot unitIndex={unitIndex}, unitTemplateKey='{unitData.UnitTemplateKey}', " +
                $"currentWeaponKey='{unitData.CurrentWeaponKey}', equippedWeaponInstanceIndex={equippedWeaponIndex}, " +
                $"equippedWeaponKey='{equippedWeaponKey}'.",
                this);
        }
    }

    private int[] BuildExplorationIndices(PartyPersistentData partyData, PersistentUnitRepository unitRepository)
    {
        int[] result = new int[ExplorationSlotCount];
        HashSet<int> seenUnitIndices = new HashSet<int>();

        for (int i = 0; i < ExplorationSlotCount; i++)
        {
            int unitIndex = FindPartyUnitByTemplateKey(partyData, unitRepository, ExplorationTemplateOrder[i], seenUnitIndices);
            if (unitIndex <= 0)
                continue;

            result[i] = unitIndex;
        }

        return result;
    }

    private void SetCompositionSlots(IReadOnlyList<int> unitIndices)
    {
        partyComposition.EnsureSlotCount(ExplorationSlotCount);
        for (int i = 0; i < ExplorationSlotCount; i++)
            partyComposition.SetUnitIndexAt(i, unitIndices != null && i < unitIndices.Count ? Mathf.Max(0, unitIndices[i]) : 0);
    }

    private void RebuildVisualUnits(IReadOnlyList<int> unitIndices, PersistentUnitRepository unitRepository)
    {
        ClearVisualUnits();

        if (prefabRegistry == null)
        {
            Warn("LevelPrefabRegistry was not found. Player unit visuals cannot be rebuilt.");
            return;
        }

        for (int i = 0; i < ExplorationSlotCount; i++)
        {
            int unitIndex = unitIndices != null && i < unitIndices.Count ? unitIndices[i] : 0;
            if (unitIndex <= 0)
                continue;

            if (!unitRepository.TryGetUnit(unitIndex, out UnitPersistentData unitData) || unitData == null)
                continue;

            if (!prefabRegistry.TryGetPlayerUnitPrefab(unitData.UnitTemplateKey, out PartyUnitState prefab))
            {
                Warn($"No player unit prefab registered for template key '{unitData.UnitTemplateKey}'.");
                continue;
            }

            PartyUnitState instance = Instantiate(prefab, unitRoot, false);
            instance.name = $"{unitData.UnitTemplateKey}_{unitData.UnitIndex}";
            instance.transform.localPosition = GetExplorationLocalPosition(unitData.UnitTemplateKey);
            instance.transform.localRotation = Quaternion.identity;
            instance.ApplyPersistentData(unitData);
        }

        PartyMovementVisualController visualController = GetComponent<PartyMovementVisualController>();
        if (visualController != null)
            visualController.CollectUnitTurnControllers();
    }

    private void ClearVisualUnits()
    {
        if (unitRoot == null)
            return;

        for (int i = unitRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = unitRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static int FindPartyUnitByTemplateKey(
        PartyPersistentData partyData,
        PersistentUnitRepository unitRepository,
        string templateKey,
        HashSet<int> seenUnitIndices)
    {
        if (partyData == null || partyData.UnitIndices == null || unitRepository == null || string.IsNullOrWhiteSpace(templateKey))
            return 0;

        for (int i = 0; i < partyData.UnitIndices.Count; i++)
        {
            int unitIndex = partyData.UnitIndices[i];
            if (unitIndex <= 0 || seenUnitIndices.Contains(unitIndex))
                continue;

            if (!unitRepository.TryGetUnit(unitIndex, out UnitPersistentData unitData) || unitData == null)
                continue;

            if (!string.Equals(unitData.UnitTemplateKey, templateKey, System.StringComparison.Ordinal))
                continue;

            seenUnitIndices.Add(unitIndex);
            return unitIndex;
        }

        return 0;
    }

    private static Vector3 GetExplorationLocalPosition(string unitTemplateKey)
    {
        switch (unitTemplateKey)
        {
            case JusticeTemplateKey:
                return new Vector3(0f, 0f, 0.3f);
            case RuminaTemplateKey:
                return new Vector3(0f, 0f, -0.3f);
            case BlackBulletTemplateKey:
                return new Vector3(-0.3f, 0f, 0f);
            case NekomingTemplateKey:
                return new Vector3(0.3f, 0f, 0f);
            default:
                return Vector3.zero;
        }
    }

    private void Warn(string message)
    {
        if (logWarnings)
            Debug.LogWarning($"[PartyVisualCompositionController] {message}", this);
    }
}
