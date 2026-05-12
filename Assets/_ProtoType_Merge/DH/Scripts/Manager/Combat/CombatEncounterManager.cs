using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatEncounterManager : MonoBehaviour
{
    public event Action<PartyGridMover, EnemyGridMover> CombatStarted;

    public bool IsCombatActive { get; private set; }
    public PartyGridMover ActiveParty { get; private set; }
    public EnemyGridMover ActiveEnemy { get; private set; }

    public bool BeginCombat(PartyGridMover party, EnemyGridMover enemy)
    {
        if (party == null || enemy == null)
            return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = enemy.EnemyId;

        if (!TryRegisterCombatParticipants(party, partyId, enemy, enemyId))
            return false;

        Debug.Log(
            $"Combat encounter requested between party '{partyId}' and enemy '{enemyId}'. " +
            "Combat flow is not implemented yet.",
            this);
        CombatStarted?.Invoke(party, enemy);
        return false;
    }

    public void ClearCombatState()
    {
        IsCombatActive = false;
        ActiveParty = null;
        ActiveEnemy = null;
    }

    private bool TryRegisterCombatParticipants(PartyGridMover party, string partyId, EnemyGridMover enemy, string enemyId)
    {
        CombatContext combatContext = CombatContext.Instance;
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;

        if (combatContext == null)
        {
            Debug.LogWarning("CombatContext is missing, so combat participants could not be registered.", this);
            return false;
        }

        IReadOnlyList<int> partyUnitIndices = ResolvePartyUnitIndices(partyRepository, party, partyId);
        IReadOnlyList<int> enemyUnitIndices = ResolveEnemyUnitIndices(enemyGroupRepository, enemy, enemyId);
        if (partyUnitIndices.Count == 0 || enemyUnitIndices.Count == 0)
        {
            Debug.LogWarning(
                $"Combat participant registration failed. partyId='{partyId}' units={partyUnitIndices.Count}, enemyId='{enemyId}' units={enemyUnitIndices.Count}.",
                this);
            combatContext.Clear();
            return false;
        }

        combatContext.RegisterCombatParty(partyId, partyUnitIndices);
        combatContext.RegisterCombatEnemy(enemyId, enemyUnitIndices);
        combatContext.SetCombatResult(CombatResult.None);
        return true;
    }

    private static IReadOnlyList<int> ResolvePartyUnitIndices(PartyPersistentRepository repository, PartyGridMover party, string partyId)
    {
        if (repository != null && repository.TryGetParty(partyId, out PartyPersistentData partyData))
        {
            IReadOnlyList<int> filteredRepositoryIndices = FilterValidUnitIndices(partyData.UnitIndices);
            if (filteredRepositoryIndices.Count > 0)
                return filteredRepositoryIndices;
        }

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        return FilterValidUnitIndices(composition != null ? composition.UnitIndices : Array.Empty<int>());
    }

    private static IReadOnlyList<int> ResolveEnemyUnitIndices(EnemyGroupPersistentRepository repository, EnemyGridMover enemy, string enemyId)
    {
        if (repository != null && repository.TryGetEnemy(enemyId, out EnemyPersistentData enemyData))
        {
            IReadOnlyList<int> filteredRepositoryIndices = FilterValidUnitIndices(enemyData.UnitIndices);
            if (filteredRepositoryIndices.Count > 0)
                return filteredRepositoryIndices;
        }

        EnemyComposition composition = enemy != null ? enemy.GetComponent<EnemyComposition>() : null;
        return FilterValidUnitIndices(composition != null ? composition.UnitIndices : Array.Empty<int>());
    }

    private static IReadOnlyList<int> FilterValidUnitIndices(IReadOnlyList<int> source)
    {
        List<int> validUnitIndices = new List<int>();
        if (source == null)
            return validUnitIndices;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] > 0)
                validUnitIndices.Add(source[i]);
        }

        return validUnitIndices;
    }
}
