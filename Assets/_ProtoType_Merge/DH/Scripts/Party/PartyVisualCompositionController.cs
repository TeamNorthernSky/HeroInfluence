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

        if (!partyRepository.TryGetParty(partyId, out PartyPersistentData partyData) || partyData == null)
        {
            ClearVisualUnits();
            SetCompositionSlots(new int[ExplorationSlotCount]);
            return;
        }

        int[] explorationIndices = BuildExplorationIndices(partyData, unitRepository);
        SetCompositionSlots(partyData.UnitIndices);
        RebuildVisualUnits(explorationIndices, unitRepository);
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

            unitIndices[i] = unitRepository.CreateUnit(
                template.UnitKey,
                1,
                template.BaseStats,
                template.LevelupStats,
                ResolveInitialCurrentSkillIndex(template.UnitKey),
                0,
                default);
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
        if (prefabRegistry == null)
            return 0;

        return prefabRegistry.TryGetPlayerUnitPrefab(unitTemplateKey, out PartyUnitState prefab) && prefab != null
            ? prefab.CurrentSkillIndex
            : 0;
    }

    private string GetInitialTemplateKey(int index)
    {
        if (initialUnitTemplateKeys == null || index < 0 || index >= initialUnitTemplateKeys.Length)
            return string.Empty;

        return string.IsNullOrWhiteSpace(initialUnitTemplateKeys[index]) ? string.Empty : initialUnitTemplateKeys[index].Trim();
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
