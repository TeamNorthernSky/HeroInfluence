using System;
using System.Collections.Generic;
using UnityEngine;

public class SubEventRegistry : MonoBehaviour
{
    private readonly List<SubEventObject> subEvents = new List<SubEventObject>();

    public IReadOnlyList<SubEventObject> SubEvents => subEvents;

    public event Action<SubEventObject> SubEventRegistered;
    public event Action<SubEventObject> SubEventUnregistered;

    private void Awake()
    {
        RegisterExistingSubEvents();
    }

    public void Register(SubEventObject subEvent)
    {
        if (subEvent == null || subEvents.Contains(subEvent))
            return;

        subEvents.Add(subEvent);
        SubEventRegistered?.Invoke(subEvent);
    }

    public void Unregister(SubEventObject subEvent)
    {
        if (subEvent == null)
            return;

        if (!subEvents.Remove(subEvent))
            return;

        SubEventUnregistered?.Invoke(subEvent);
    }

    [ContextMenu("Rebuild SubEvent Registry")]
    public void RegisterExistingSubEvents()
    {
        subEvents.Clear();

        SubEventObject[] sceneEvents = FindObjectsByType<SubEventObject>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneEvents.Length; i++)
            Register(sceneEvents[i]);
    }
}
