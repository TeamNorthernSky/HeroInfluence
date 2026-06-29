using System.Collections.Generic;
using UnityEngine;

public class PartyRecoveryAreaController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PartyRegistry partyRegistry;

    [Header("Recovery Sources")]
    [SerializeField] private bool enableRecoveryAreas = false;
    [SerializeField] private bool recoverAtCastle = false;
    [SerializeField] private bool recoverAtClaimedOutpost = false;

    [Header("Debug")]
    [SerializeField] private bool logRecovery;

    private readonly List<PartyGridMover> subscribedParties = new List<PartyGridMover>();

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeParties();
    }

    private void OnDisable()
    {
        UnsubscribeParties();
    }

    [ContextMenu("Refresh Party Subscriptions")]
    public void RefreshPartySubscriptions()
    {
        UnsubscribeParties();
        ResolveReferences();
        SubscribeParties();
    }

    private void SubscribeParties()
    {
        PartyGridMover[] parties = ResolveParties();
        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover party = parties[i];
            if (party == null || subscribedParties.Contains(party))
                continue;

            party.GridEntered += HandlePartyGridEntered;
            subscribedParties.Add(party);
        }
    }

    private void UnsubscribeParties()
    {
        for (int i = 0; i < subscribedParties.Count; i++)
        {
            PartyGridMover party = subscribedParties[i];
            if (party != null)
                party.GridEntered -= HandlePartyGridEntered;
        }

        subscribedParties.Clear();
    }

    private PartyGridMover[] ResolveParties()
    {
        if (partyRegistry != null && partyRegistry.PartyMovers.Length > 0)
            return partyRegistry.PartyMovers;

        return FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);
    }

    private void HandlePartyGridEntered(Vector2Int enteredGrid)
    {
        if (!enableRecoveryAreas)
            return;

        if (!IsRecoveryCell(enteredGrid))
            return;

        for (int i = 0; i < subscribedParties.Count; i++)
        {
            PartyGridMover party = subscribedParties[i];
            if (party == null || party.GetCurrentGrid() != enteredGrid)
                continue;

            RecoverParty(party);
        }
    }

    private bool IsRecoveryCell(Vector2Int grid)
    {
        if (gridManager == null)
            return false;

        if (recoverAtCastle && gridManager.TryGetAdjacentCastleObject(grid, out _))
            return true;

        if (!recoverAtClaimedOutpost)
            return false;

        if (!gridManager.TryGetAdjacentOutpostGrid(grid, out Vector2Int outpostGrid))
            return false;

        if (!gridManager.TryGetOutpostObjectAtGrid(outpostGrid, out Outpost outpost))
            return false;

        return outpost != null && outpost.IsPlayerClaimed;
    }

    private void RecoverParty(PartyGridMover party)
    {
        if (party == null)
            return;

        IReadOnlyList<int> unitIndices = ResolvePartyUnitIndices(party);
        if (unitIndices == null || unitIndices.Count == 0)
            return;

        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        if (unitRepository == null)
            return;

        int recoveredCount = 0;
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0)
                continue;

            if (!unitRepository.HealUnitToIngameMaxHp(unitIndex, out float healedHp))
                continue;

            ApplySceneUnitHp(party, unitIndex, healedHp);
            recoveredCount++;
        }

        if (logRecovery && recoveredCount > 0)
            Debug.Log($"[PartyRecoveryAreaController] Recovered {recoveredCount} unit(s) for party '{party.name}'.", party);
    }

    private static IReadOnlyList<int> ResolvePartyUnitIndices(PartyGridMover party)
    {
        PartyIdentity identity = party.GetComponent<PartyIdentity>();
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;

        if (identity != null &&
            partyRepository != null &&
            partyRepository.TryGetParty(identity.PartyId, out PartyPersistentData partyData) &&
            partyData != null &&
            partyData.UnitIndices.Count > 0)
        {
            return partyData.UnitIndices;
        }

        PartyComposition composition = party.GetComponent<PartyComposition>();
        return composition != null ? composition.UnitIndices : System.Array.Empty<int>();
    }

    private static void ApplySceneUnitHp(PartyGridMover party, int unitIndex, float healedHp)
    {
        PartyUnitState[] unitStates = party.GetComponentsInChildren<PartyUnitState>(true);
        for (int i = 0; i < unitStates.Length; i++)
        {
            PartyUnitState unitState = unitStates[i];
            if (unitState == null || unitState.UnitIndex != unitIndex)
                continue;

            unitState.SetCurrentHp(healedHp);
            unitState.SetIncapacitated(false);
        }
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
    }
}
