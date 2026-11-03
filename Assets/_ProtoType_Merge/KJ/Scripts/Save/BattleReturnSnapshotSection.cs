using UnityEngine;

/// <summary>Keeps dialogue replay metadata with the same checkpoint as the saved world state.</summary>
public sealed class BattleReturnSnapshotSection : DHTurnStartSnapshotSection
{
    private GameSaveData.BattleReturnCheckpoint checkpoint;

    public static BattleReturnSnapshotSection Ensure(DHTurnStartSnapshotStore store)
    {
        var section = store.GetComponentInChildren<BattleReturnSnapshotSection>(true);
        if (section != null) return section;
        var host = new GameObject("BattleReturnSnapshot");
        host.transform.SetParent(store.transform, false);
        return host.AddComponent<BattleReturnSnapshotSection>();
    }

    public void SetCheckpoint(GameSaveData.BattleReturnCheckpoint value)
    {
        checkpoint = value == null ? null
            : JsonUtility.FromJson<GameSaveData.BattleReturnCheckpoint>(JsonUtility.ToJson(value));
    }

    public override void CaptureFromRuntime() => checkpoint = null;
    public override void ClearSnapshot() => checkpoint = null;
    public override void LoadFromGameSaveData(GameSaveData data) => SetCheckpoint(data?.battleReturnCheckpoint);
    public override void FillGameSaveData(GameSaveData data)
    {
        if (data != null)
            data.battleReturnCheckpoint = checkpoint == null ? null
                : JsonUtility.FromJson<GameSaveData.BattleReturnCheckpoint>(JsonUtility.ToJson(checkpoint));
    }
}
