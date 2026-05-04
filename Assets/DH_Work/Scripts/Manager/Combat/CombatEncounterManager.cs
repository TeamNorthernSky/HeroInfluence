using System;
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
        string partyLabel = partyIdentity != null ? partyIdentity.PartyId : party.name;

        Debug.Log(
            $"Combat encounter requested between party '{partyLabel}' and enemy '{enemy.EnemyId}'. " +
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
}
