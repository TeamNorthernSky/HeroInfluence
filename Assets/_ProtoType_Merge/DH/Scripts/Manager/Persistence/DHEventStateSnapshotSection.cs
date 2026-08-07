using System.Collections.Generic;
using UnityEngine;

public sealed class DHEventStateSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private List<DHEventFlagState> chatFlags = new List<DHEventFlagState>();
    [SerializeField] private List<DHEventNumericState> eventNumericStates = new List<DHEventNumericState>();

    public override void CaptureFromRuntime()
    {
        chatFlags.Clear();
        eventNumericStates.Clear();

        // Persistent event state only. Active effect execution context and CombatContext are runtime-only.
        DHEventStateRepository repository = DHEventStateRepository.Instance;
        if (repository == null)
            return;

        chatFlags.AddRange(repository.CaptureFlagSnapshot());
        eventNumericStates.AddRange(repository.CaptureNumericSnapshot());
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        // KJ SaveService writes these lists directly into JSON through GameSaveData.
        data.chatFlags.Clear();
        data.eventNumericStates.Clear();
        CopyFlags(chatFlags, data.chatFlags);
        CopyNumericStates(eventNumericStates, data.eventNumericStates);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data == null)
            return;

        CopyFlags(data.chatFlags, chatFlags);
        CopyNumericStates(data.eventNumericStates, eventNumericStates);
    }

    public override void ClearSnapshot()
    {
        chatFlags.Clear();
        eventNumericStates.Clear();
    }

    private static void CopyFlags(IReadOnlyList<DHEventFlagState> source, List<DHEventFlagState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            DHEventFlagState state = source[i];
            if (state == null || string.IsNullOrWhiteSpace(state.flagName))
                continue;

            target.Add(new DHEventFlagState(
                DHEventStateRepository.NormalizeFlagName(state.flagName),
                state.value));
        }
    }

    private static void CopyNumericStates(IReadOnlyList<DHEventNumericState> source, List<DHEventNumericState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            DHEventNumericState state = source[i];
            if (state == null || string.IsNullOrWhiteSpace(state.key))
                continue;

            target.Add(new DHEventNumericState(
                DHEventStateRepository.NormalizeKey(state.key),
                state.value));
        }
    }
}
