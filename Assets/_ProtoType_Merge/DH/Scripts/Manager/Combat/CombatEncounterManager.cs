using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatEncounterManager : MonoBehaviour
{
    private const string BattleSceneName = "TmpBattleScene";

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

        RegisterCombatParticipants(party, partyId, enemy, enemyId);

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = enemy;

        CombatStarted?.Invoke(party, enemy);
        SceneManager.LoadScene(BattleSceneName);
        return true;
    }

    public void ClearCombatState()
    {
        IsCombatActive = false;
        ActiveParty = null;
        ActiveEnemy = null;
    }

    private void RegisterCombatParticipants(PartyGridMover party, string partyId, EnemyGridMover enemy, string enemyId)
    {
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;

        if (unitRepository != null)
            unitRepository.RegisterCombatParty(partyId, ResolvePartyUnitIndices(unitRepository, party, partyId));

        if (enemyRepository != null)
            enemyRepository.RegisterCombatEnemy(enemyId, ResolveEnemyUnitIndices(enemyRepository, enemy, enemyId));
    }

    private static IReadOnlyList<int> ResolvePartyUnitIndices(PersistentUnitRepository repository, PartyGridMover party, string partyId)
    {
        if (repository != null && repository.TryGetParty(partyId, out PartyPersistentData partyData))
            return partyData.UnitIndices;

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        return FilterValidUnitIndices(composition != null ? composition.UnitIndices : Array.Empty<int>());
    }

    private static IReadOnlyList<int> ResolveEnemyUnitIndices(PersistentEnemyRepository repository, EnemyGridMover enemy, string enemyId)
    {
        if (repository != null && repository.TryGetEnemy(enemyId, out EnemyPersistentData enemyData))
            return enemyData.UnitIndices;

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
