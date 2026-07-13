using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHTurnStartSnapshotStore : MonoBehaviour
{
    private const string RootName = "[DH_TurnStartSnapshot]";

    public static DHTurnStartSnapshotStore Instance { get; private set; }
    public static bool IsCaptureSuppressed { get; private set; }

    [Header("Snapshot Metadata")]
    [SerializeField] private bool hasSnapshot;
    [SerializeField] private string capturedAt;
    [SerializeField] private int capturedDay = 1;

    private DHTurnStartSnapshotSection[] sections = Array.Empty<DHTurnStartSnapshotSection>();

    public bool HasSnapshot => hasSnapshot;
    public string CapturedAt => capturedAt;
    public int CapturedDay => Mathf.Max(1, capturedDay);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DHTurnStartSnapshotStore EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);

        AddSection<DHUnitSnapshotSection>(root.transform, "UnitSnapshot");
        AddSection<DHEnemyUnitSnapshotSection>(root.transform, "EnemyUnitSnapshot");
        AddSection<DHEnemyGroupSnapshotSection>(root.transform, "EnemyGroupSnapshot");
        AddSection<DHWeaponSnapshotSection>(root.transform, "WeaponSnapshot");
        AddSection<DHPartySnapshotSection>(root.transform, "PartySnapshot");
        AddSection<DHMapProgressSnapshotSection>(root.transform, "MapProgressSnapshot");
        AddSection<DHGameStateSnapshotSection>(root.transform, "GameStateSnapshot");
        AddSection<DHEconomySnapshotSection>(root.transform, "EconomySnapshot");
        AddSection<DHHQSnapshotSection>(root.transform, "HQSnapshot");
        AddSection<DHDepartmentSnapshotSection>(root.transform, "DepartmentSnapshot");
        AddSection<DHVisitSnapshotSection>(root.transform, "VisitSnapshot");

        return root.AddComponent<DHTurnStartSnapshotStore>();
    }

    private static void AddSection<T>(Transform root, string name) where T : DHTurnStartSnapshotSection
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(root, false);
        child.AddComponent<T>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RefreshSections();
    }

    [ContextMenu("Refresh Sections")]
    public void RefreshSections()
    {
        sections = GetComponentsInChildren<DHTurnStartSnapshotSection>(true);
    }

    [ContextMenu("Capture Turn Start Snapshot")]
    public bool CaptureFromRuntime()
    {
        if (IsCaptureSuppressed)
        {
            Debug.Log("[DH Snapshot] Turn-start snapshot capture skipped while restore suppression is active.");
            return false;
        }

        RefreshSections();

        for (int i = 0; i < sections.Length; i++)
            sections[i]?.CaptureFromRuntime();

        capturedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        capturedDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        hasSnapshot = true;
        return true;
    }

    public static bool CaptureTurnStartSnapshot()
    {
        return EnsureInstance().CaptureFromRuntime();
    }

    public static void SetCaptureSuppressed(bool suppressed)
    {
        IsCaptureSuppressed = suppressed;
    }

    public bool FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return false;

        if (!hasSnapshot)
        {
            Debug.LogWarning("[DH Snapshot] No turn-start snapshot is available for save data fill.");
            return false;
        }

        RefreshSections();

        data.hasData = true;
        data.savedAt = capturedAt;
        data.saveVersion = 1;

        for (int i = 0; i < sections.Length; i++)
            sections[i]?.FillGameSaveData(data);

        return true;
    }

    public void LoadFromGameSaveData(GameSaveData data)
    {
        if (data == null)
        {
            ClearSnapshot();
            return;
        }

        RefreshSections();

        for (int i = 0; i < sections.Length; i++)
            sections[i]?.LoadFromGameSaveData(data);

        capturedAt = data.savedAt ?? string.Empty;
        capturedDay = Mathf.Max(1, data.currentDay);
        hasSnapshot = data.hasData;
    }

    [ContextMenu("Clear Snapshot")]
    public void ClearSnapshot()
    {
        RefreshSections();

        for (int i = 0; i < sections.Length; i++)
            sections[i]?.ClearSnapshot();

        hasSnapshot = false;
        capturedAt = string.Empty;
        capturedDay = 1;
    }
}
