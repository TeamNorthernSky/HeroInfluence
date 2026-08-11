using System;
using System.Collections.Generic;
using UnityEngine;

public class DecorativeObjectRegistry : MonoBehaviour
{
    private readonly List<DecorativeObjectPlacement> decorativeObjects = new List<DecorativeObjectPlacement>();

    public IReadOnlyList<DecorativeObjectPlacement> DecorativeObjects => decorativeObjects;

    public event Action<DecorativeObjectPlacement> DecorativeObjectRegistered;
    public event Action<DecorativeObjectPlacement> DecorativeObjectUnregistered;

    private void Awake()
    {
        RefreshSceneDecorativeObjects();
    }

    public void Register(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject == null || decorativeObjects.Contains(decorativeObject))
            return;

        decorativeObjects.Add(decorativeObject);
        DecorativeObjectRegistered?.Invoke(decorativeObject);
    }

    public void Unregister(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject == null)
            return;

        if (!decorativeObjects.Remove(decorativeObject))
            return;

        DecorativeObjectUnregistered?.Invoke(decorativeObject);
    }

    [ContextMenu("Refresh Scene Decorative Objects")]
    public void RefreshSceneDecorativeObjects()
    {
        decorativeObjects.Clear();

        DecorativeObjectPlacement[] sceneObjects = FindObjectsByType<DecorativeObjectPlacement>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            DecorativeObjectPlacement decorativeObject = sceneObjects[i];
            if (decorativeObject == null)
                continue;

            Register(decorativeObject);
        }
    }
}
