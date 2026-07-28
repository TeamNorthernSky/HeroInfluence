using System.Collections.Generic;
using UnityEngine;

public sealed class DHEventStateSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private List<DHEventFlagState> chatFlags = new List<DHEventFlagState>();

    public override void CaptureFromRuntime()
    {
        chatFlags.Clear();

        DHEventStateRepository repository = DHEventStateRepository.Instance;
        if (repository == null)
            return;

        chatFlags.AddRange(repository.CaptureFlagSnapshot());
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.chatFlags.Clear();
        CopyFlags(chatFlags, data.chatFlags);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data == null)
            return;

        CopyFlags(data.chatFlags, chatFlags);
    }

    public override void ClearSnapshot()
    {
        chatFlags.Clear();
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
}
