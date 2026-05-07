using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatEncounterManager : MonoBehaviour
{
    private const string BattleSceneName = "BattleScene";

    public event Action<PartyGridMover, EnemyGridMover> CombatStarted;

    public bool IsCombatActive { get; private set; }
    public PartyGridMover ActiveParty { get; private set; }
    public EnemyGridMover ActiveEnemy { get; private set; }

    public bool BeginCombat(PartyGridMover party, EnemyGridMover enemy)
    {
        if (party == null || enemy == null)
            return false;

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
}
