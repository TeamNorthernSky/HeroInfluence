using System;
using System.Collections.Generic;
using UnityEngine;

public class MainEventRegistry : MonoBehaviour
{
    private readonly List<MainEventObject> mainEvents = new List<MainEventObject>();

    public IReadOnlyList<MainEventObject> MainEvents => mainEvents;

    public event Action<MainEventObject> MainEventRegistered;
    public event Action<MainEventObject> MainEventUnregistered;

    private void Awake()
    {
        RegisterExistingMainEvents();
    }

    public void Register(MainEventObject mainEvent)
    {
        if (mainEvent == null || mainEvents.Contains(mainEvent))
            return;

        mainEvents.Add(mainEvent);
        MainEventRegistered?.Invoke(mainEvent);
    }

    public void Unregister(MainEventObject mainEvent)
    {
        if (mainEvent == null)
            return;

        if (!mainEvents.Remove(mainEvent))
            return;

        MainEventUnregistered?.Invoke(mainEvent);
    }

    [ContextMenu("Rebuild MainEvent Registry")]
    public void RegisterExistingMainEvents()
    {
        mainEvents.Clear();

        MainEventObject[] sceneEvents = FindObjectsByType<MainEventObject>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneEvents.Length; i++)
            Register(sceneEvents[i]);
    }

    public bool TryGetEventAtInteractionCell(
        Vector2Int grid,
        GridManager gridManager,
        out MainEventObject mainEvent)
    {
        mainEvent = null;

        for (int i = 0; i < mainEvents.Count; i++)
        {
            MainEventObject candidate = mainEvents[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!candidate.IsInteractionCell(grid, gridManager))
                continue;

            mainEvent = candidate;
            return true;
        }

        return false;
    }
}
