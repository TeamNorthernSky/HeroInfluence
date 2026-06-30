using System.Collections.Generic;
using UnityEngine;

public static class PartyRepositorySync
{
    public static bool ApplyUnitToScene(int unitIndex)
    {
        if (unitIndex <= 0)
            return false;

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null || !repository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
            return false;

        bool appliedAny = false;
        PartyUnitState[] unitStates = Object.FindObjectsByType<PartyUnitState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < unitStates.Length; i++)
        {
            PartyUnitState state = unitStates[i];
            if (state == null || state.UnitIndex != unitIndex)
                continue;

            state.ApplyPersistentData(data);
            appliedAny = true;
        }

        return appliedAny;
    }

    public static int ApplyUnitsToScene(IReadOnlyList<int> unitIndices)
    {
        if (unitIndices == null || unitIndices.Count == 0)
            return 0;

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return 0;

        int appliedCount = 0;
        PartyUnitState[] unitStates = Object.FindObjectsByType<PartyUnitState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < unitStates.Length; i++)
        {
            PartyUnitState state = unitStates[i];
            if (state == null || state.UnitIndex <= 0 || !ContainsUnitIndex(unitIndices, state.UnitIndex))
                continue;

            if (!repository.TryGetUnit(state.UnitIndex, out UnitPersistentData data) || data == null)
                continue;

            state.ApplyPersistentData(data);
            appliedCount++;
        }

        return appliedCount;
    }

    public static bool ApplyPartyToScene(string partyId, bool rebuildVisuals = false)
    {
        if (string.IsNullOrWhiteSpace(partyId))
            return false;

        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        if (partyRepository == null || !partyRepository.TryGetParty(partyId, out PartyPersistentData partyData) || partyData == null)
            return false;

        bool appliedAny = false;
        PartyGridMover[] parties = Object.FindObjectsByType<PartyGridMover>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover party = parties[i];
            if (party == null)
                continue;

            PartyIdentity identity = party.GetComponent<PartyIdentity>();
            if (identity == null || !string.Equals(identity.PartyId, partyId, System.StringComparison.Ordinal))
                continue;

            ApplyPartyComposition(party, partyData.UnitIndices);

            if (rebuildVisuals)
            {
                PartyVisualCompositionController visualComposition = party.GetComponent<PartyVisualCompositionController>();
                if (visualComposition != null)
                    visualComposition.RebuildFromRepository();
            }

            ApplyUnitsToScene(partyData.UnitIndices);
            appliedAny = true;
        }

        return appliedAny;
    }

    public static int ApplyAllPartiesToScene(bool rebuildVisuals = false)
    {
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        if (partyRepository == null)
            return 0;

        int appliedCount = 0;
        IReadOnlyList<PartyPersistentData> parties = partyRepository.Parties;
        for (int i = 0; i < parties.Count; i++)
        {
            PartyPersistentData partyData = parties[i];
            if (partyData == null)
                continue;

            if (ApplyPartyToScene(partyData.PartyId, rebuildVisuals))
                appliedCount++;
        }

        return appliedCount;
    }

    public static bool SyncSceneUnitToRepository(PartyUnitState unitState)
    {
        return unitState != null && unitState.SyncToRepository();
    }

    public static bool SyncPartySceneToRepository(PartyGridMover party)
    {
        if (party == null)
            return false;

        PartyIdentity identity = party.GetComponent<PartyIdentity>();
        PartyComposition composition = party.GetComponent<PartyComposition>();
        PartyPersistentRepository repository = PartyPersistentRepository.Instance;
        if (identity == null || composition == null || repository == null || string.IsNullOrWhiteSpace(identity.PartyId))
            return false;

        IReadOnlyList<int> existingSlots = null;
        if (repository.TryGetParty(identity.PartyId, out PartyPersistentData existingData) && existingData != null)
            existingSlots = existingData.UnitSlots;

        repository.RegisterOrUpdateParty(identity.PartyId, composition.UnitIndices, existingSlots);
        return true;
    }

    private static void ApplyPartyComposition(PartyGridMover party, IReadOnlyList<int> unitIndices)
    {
        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        if (composition == null)
            return;

        int slotCount = Mathf.Max(composition.UnitIndices.Length, unitIndices != null ? unitIndices.Count : 0);
        composition.EnsureSlotCount(slotCount);
        for (int i = 0; i < slotCount; i++)
        {
            int unitIndex = unitIndices != null && i < unitIndices.Count ? unitIndices[i] : 0;
            composition.SetUnitIndexAt(i, Mathf.Max(0, unitIndex));
        }
    }

    private static bool ContainsUnitIndex(IReadOnlyList<int> unitIndices, int unitIndex)
    {
        if (unitIndices == null || unitIndex <= 0)
            return false;

        for (int i = 0; i < unitIndices.Count; i++)
        {
            if (unitIndices[i] == unitIndex)
                return true;
        }

        return false;
    }
}
