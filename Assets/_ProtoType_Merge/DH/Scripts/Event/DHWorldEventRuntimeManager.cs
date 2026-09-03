using System;
using UnityEngine;

public sealed class DHWorldEventRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_WorldEventRuntime]";

    public static DHWorldEventRuntimeManager Instance { get; private set; }

    [SerializeField] private bool completeImmediatelyUntilUiIsReady = true;
    [SerializeField] private bool logEvents = true;

    private bool isRunning;

    public bool IsRunning => isRunning;

    public static DHWorldEventRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = GameObject.Find(RootName);
        if (root == null)
            root = new GameObject(RootName);

        return root.AddComponent<DHWorldEventRuntimeManager>();
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
    }

    public bool TryStartEvent(WorldEventObject source, PartyGridMover party, Action closedCallback = null)
    {
        if (isRunning || source == null)
            return false;

        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance;
        if (catalog == null || !catalog.TryGetEvent(source.WorldEventId, out DHWorldEventTemplate template) || template == null)
        {
            Debug.LogWarning($"[DHWorldEventRuntime] World event data was not found: {source.WorldEventId}", source);
            return false;
        }

        isRunning = true;

        if (logEvents)
        {
            Debug.Log(
                $"[DHWorldEventRuntime] Start {template.EventType} event '{template.WorldEventId}' zone={template.ZoneNo}.",
                source);
        }

        if (completeImmediatelyUntilUiIsReady)
        {
            source.Complete();
            isRunning = false;
            closedCallback?.Invoke();
            return true;
        }

        isRunning = false;
        closedCallback?.Invoke();
        return true;
    }
}

