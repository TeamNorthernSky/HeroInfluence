using System;
using UnityEngine;

public class CombatPromptService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatPromptPanelController promptPrefab;
    [SerializeField] private Transform promptRoot;

    private CombatPromptPanelController promptInstance;
    private Action pendingStartBattle;
    private Action<bool> pendingClosed;

    public bool IsOpen => promptInstance != null && promptInstance.gameObject.activeInHierarchy;

    public bool TryOpenEnemyCombatPrompt(
        PartyGridMover party,
        EnemyGridMover enemy,
        CombatEncounterManager combatEncounterManager,
        Action<bool> onClosed)
    {
        if (party == null || enemy == null || combatEncounterManager == null)
            return false;

        if (!EnsurePromptInstance())
            return false;

        if (IsOpen)
            return true;

        pendingClosed = onClosed;
        pendingStartBattle = () =>
        {
            bool combatStarted = combatEncounterManager.BeginCombat(party, enemy);
            ClosePrompt(combatStarted);
        };

        promptInstance.Open(HandleStartBattleClicked);
        return true;
    }

    private bool EnsurePromptInstance()
    {
        if (promptInstance != null)
            return true;

        if (promptPrefab == null)
            return false;

        Transform parent = promptRoot != null ? promptRoot : transform;
        promptInstance = Instantiate(promptPrefab, parent);
        promptInstance.gameObject.SetActive(false);
        return true;
    }

    private void HandleStartBattleClicked()
    {
        Action startBattle = pendingStartBattle;
        pendingStartBattle = null;
        startBattle?.Invoke();
    }

    private void ClosePrompt(bool startedCombat)
    {
        if (promptInstance != null)
            promptInstance.Close();

        Action<bool> closed = pendingClosed;
        pendingClosed = null;
        pendingStartBattle = null;
        closed?.Invoke(startedCombat);
    }
}
