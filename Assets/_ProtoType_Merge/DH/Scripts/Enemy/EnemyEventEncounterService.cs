using System;
using UnityEngine;

public static class EnemyEventEncounterService
{
    public static bool TryOpenEncounterChat(
        PartyGridMover party,
        EnemyGridMover enemy,
        Action<bool> onClosed)
    {
        if (party == null || enemy == null)
            return false;

        if (ChatManager.Instance == null || ChatManager.Instance.IsRunning)
            return false;

        EnemyEventEncounterBinding binding = enemy.GetComponent<EnemyEventEncounterBinding>();
        if (binding == null || !binding.HasEventEncounter)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null ||
            !catalog.TryGetChatTemplate(binding.EncounterChatZoneId, binding.EncounterChatId, out DHEventChatTemplate chat) ||
            chat == null)
        {
            return false;
        }

        ExplorationChatCameraFocus focus = null;
        bool completed = false;
        bool pendingEncounterStarted = false;
        Action<bool> finish = combatStarted =>
        {
            if (completed) return;
            completed = true;
            ExplorationChatCameraFocus ownedFocus = focus;
            focus = null;
            if (ownedFocus != null) ownedFocus.Finish();
            if (pendingEncounterStarted)
                DHEnemyEventEncounterRuntimeManager.Instance?.ClearPendingEncounterIfUnused();
            onClosed?.Invoke(combatStarted);
        };

        return ExplorationChatCameraFocus.TryBegin(enemy.transform, party.transform,
            () =>
            {
                if (party == null || enemy == null || binding == null || !binding.HasEventEncounter ||
                    ChatManager.Instance == null || ChatManager.Instance.IsRunning)
                    return false;

                DHEnemyEventEncounterRuntimeManager.EnsureInstance().BeginPendingEncounter(binding);
                pendingEncounterStarted = true;
                ChatModalController.Show(binding.EncounterChatZoneId, binding.EncounterChatId, () =>
                {
                    bool combatStarted = CombatContext.Instance != null &&
                        CombatContext.Instance.HasEventBattle && CombatContext.Instance.Result == CombatResult.None;
                    finish(combatStarted);
                });
                return ChatModalController.IsOpen && ChatManager.Instance != null && ChatManager.Instance.IsRunning;
            }, () => finish(false), out focus);
    }
}
