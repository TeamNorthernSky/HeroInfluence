using System.Collections.Generic;
using UnityEngine;

public class MapEventRegistry : MonoBehaviour
{
    private readonly List<MapEventObject> mapEvents = new List<MapEventObject>();

    public IReadOnlyList<MapEventObject> MapEvents => mapEvents;

    private void Awake()
    {
        RegisterExistingEvents();
    }

    public void Register(MapEventObject mapEvent)
    {
        if (mapEvent == null || mapEvents.Contains(mapEvent))
            return;

        mapEvents.Add(mapEvent);
    }

    public void Unregister(MapEventObject mapEvent)
    {
        if (mapEvent == null)
            return;

        mapEvents.Remove(mapEvent);
    }

    [ContextMenu("Rebuild Registry")]
    public void RegisterExistingEvents()
    {
        mapEvents.Clear();

        MapEventObject[] sceneEvents = FindObjectsByType<MapEventObject>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneEvents.Length; i++)
        {
            MapEventObject mapEvent = sceneEvents[i];
            if (mapEvent == null)
                continue;

            Register(mapEvent);
        }
    }
}
