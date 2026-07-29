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

        EnemyEventEncounterBinding binding = enemy.GetComponent<EnemyEventEncounterBinding>();
        if (binding == null || !binding.HasEventEncounter)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null ||
            !catalog.TryGetChat(binding.EncounterChatZoneId, binding.EncounterChatId, out ChatDBEventData chat) ||
            chat == null)
        {
            return false;
        }

        DHEnemyEventEncounterRuntimeManager.EnsureInstance().BeginPendingEncounter(binding);
        ChatModalController.Show(
            binding.EncounterChatZoneId,
            binding.EncounterChatId,
            () =>
            {
                bool combatStarted = CombatContext.Instance != null &&
                    CombatContext.Instance.HasEventBattle &&
                    CombatContext.Instance.Result == CombatResult.None;
                DHEnemyEventEncounterRuntimeManager.Instance?.ClearPendingEncounterIfUnused();
                onClosed?.Invoke(combatStarted);
            });
        return true;
    }
}
