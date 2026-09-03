using System.Collections.Generic;
using UnityEngine;

public sealed class WorldEventRegistry : MonoBehaviour
{
    private readonly List<WorldEventObject> worldEvents = new List<WorldEventObject>();

    public IReadOnlyList<WorldEventObject> WorldEvents => worldEvents;

    private void Awake()
    {
        RegisterExistingEvents();
    }

    public void Register(WorldEventObject worldEvent)
    {
        if (worldEvent == null || worldEvents.Contains(worldEvent))
            return;

        worldEvents.Add(worldEvent);
    }

    public void Unregister(WorldEventObject worldEvent)
    {
        if (worldEvent == null)
            return;

        worldEvents.Remove(worldEvent);
    }

    [ContextMenu("Rebuild Registry")]
    public void RegisterExistingEvents()
    {
        worldEvents.Clear();

        WorldEventObject[] sceneEvents = FindObjectsByType<WorldEventObject>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneEvents.Length; i++)
        {
            WorldEventObject worldEvent = sceneEvents[i];
            if (worldEvent == null)
                continue;

            Register(worldEvent);
        }
    }
}

