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

    private PartyGridMover subscribedParty;

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
        PartyGridMover party = ResolveParty();
        if (party == null || subscribedParty == party)
            return;

        UnsubscribeParties();
        subscribedParty = party;
        subscribedParty.GridEntered += HandlePartyGridEntered;
    }

    private void UnsubscribeParties()
    {
        if (subscribedParty != null)
            subscribedParty.GridEntered -= HandlePartyGridEntered;

        subscribedParty = null;
    }

    private PartyGridMover ResolveParty()
    {
        return partyRegistry != null ? partyRegistry.PlayerParty : null;
    }

    private void HandlePartyGridEntered(Vector2Int enteredGrid)
    {
        if (!enableRecoveryAreas)
            return;

        if (!IsRecoveryCell(enteredGrid))
            return;

        if (subscribedParty != null && subscribedParty.GetCurrentGrid() == enteredGrid)
            RecoverParty(subscribedParty);
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
