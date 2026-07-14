using UnityEngine;

public abstract class DHTurnStartSnapshotSection : MonoBehaviour
{
    public abstract void CaptureFromRuntime();
    public abstract void FillGameSaveData(GameSaveData data);
    public abstract void LoadFromGameSaveData(GameSaveData data);
    public abstract void ClearSnapshot();
}
