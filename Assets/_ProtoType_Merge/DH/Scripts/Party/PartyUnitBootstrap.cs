using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyComposition))]
[RequireComponent(typeof(PartyIdentity))]
public class PartyUnitBootstrap : MonoBehaviour
{
    [Header("Bootstrap")]
    [SerializeField] private bool populateOnStart = true;
    [SerializeField] private List<PartyHeroUnitSeed> heroUnitSeeds = new List<PartyHeroUnitSeed>();
    [SerializeField] private bool onlyWhenAllSlotsEmpty = true;

    private PartyComposition partyComposition;
    private PartyIdentity partyIdentity;

    private void Awake()
    {
        partyComposition = GetComponent<PartyComposition>();
        partyIdentity = GetComponent<PartyIdentity>();
    }

    private void Start()
    {
        if (!Application.isPlaying || !populateOnStart)
            return;

        InitializeStartingUnits();
    }

    [ContextMenu("Initialize Starting Units")]
    public void InitializeStartingUnits()
    {
        if (partyComposition == null)
            partyComposition = GetComponent<PartyComposition>();

        if (partyIdentity == null)
            partyIdentity = GetComponent<PartyIdentity>();

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null || partyComposition == null)
            return;

        if (!HasConfiguredHeroSeeds())
            return;

        InitializeFromHeroSeeds(repository);
    }

    [ContextMenu("Collect Hero Unit Seeds From Children")]
    public void CollectHeroUnitSeedsFromChildren()
    {
        heroUnitSeeds.Clear();
        heroUnitSeeds.AddRange(GetComponentsInChildren<PartyHeroUnitSeed>(true));
    }

    private bool HasConfiguredHeroSeeds()
    {
        for (int i = 0; i < heroUnitSeeds.Count; i++)
        {
            if (heroUnitSeeds[i] != null)
                return true;
        }

        return false;
    }

    private void InitializeFromHeroSeeds(PersistentUnitRepository repository)
    {
        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("PartyUnitBootstrap could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

        int slotCount = heroUnitSeeds.Count;
        partyComposition.EnsureSlotCount(slotCount);

        if (onlyWhenAllSlotsEmpty && !AreAllHeroSeedSlotsEmpty(slotCount))
            return;

        int registeredCount = 0;
        for (int i = 0; i < heroUnitSeeds.Count; i++)
        {
            PartyHeroUnitSeed seed = heroUnitSeeds[i];
            if (seed == null)
                continue;

            if (partyComposition.GetUnitIndexAt(i) > 0)
                continue;

            if (string.IsNullOrWhiteSpace(seed.UnitTemplateKey))
            {
                Debug.LogWarning($"Party hero seed on '{seed.name}' is missing a unitTemplateKey.", seed);
                continue;
            }

            if (!templateCatalog.TryGetPlayerTemplate(seed.UnitTemplateKey, out UnitData template))
            {
                Debug.LogWarning($"Party hero seed on '{seed.name}' could not resolve CSV template '{seed.UnitTemplateKey}'.", seed);
                continue;
            }

            EquipmentStatBlock currentWeaponStats = default;
            if (seed.InitialWeaponIndex > 0)
                templateCatalog.TryGetWeaponStats(seed.InitialWeaponIndex, out currentWeaponStats);

            int unitIndex = repository.CreateUnit(
                seed.UnitTemplateKey,
                seed.Level,
                seed.Favorability,
                template.baseStats,
                seed.InitialSkillIndex,
                seed.InitialWeaponIndex,
                currentWeaponStats);
            partyComposition.SetUnitIndexAt(i, unitIndex);
            registeredCount++;
        }

        string partyId = partyIdentity != null ? partyIdentity.PartyId : gameObject.name;
        repository.RegisterOrUpdateParty(partyId, partyComposition.UnitIndices);
        Debug.Log($"Party '{partyId}' initialized from {registeredCount} hero unit seed(s).", this);
    }

    private bool AreAllHeroSeedSlotsEmpty(int slotCount)
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (partyComposition.GetUnitIndexAt(i) > 0)
                return false;
        }

        return true;
    }
}
