using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialEnemyRegistry : MonoBehaviour
{
    private const string RuntimeRegistryName = "[DH_TutorialEnemyRegistry]";

    public static TutorialEnemyRegistry Instance { get; private set; }

    private readonly List<TutorialEnemyObject> enemies = new List<TutorialEnemyObject>();

    public IReadOnlyList<TutorialEnemyObject> Enemies => enemies;

    public event Action<TutorialEnemyObject> EnemyRegistered;
    public event Action<TutorialEnemyObject> EnemyUnregistered;

    public static TutorialEnemyRegistry EnsureSceneRegistry()
    {
        if (Instance != null)
            return Instance;

        TutorialEnemyRegistry registry = FindFirstObjectByType<TutorialEnemyRegistry>();
        if (registry != null)
            return registry;

        if (!Application.isPlaying)
            return null;

        GameObject root = new GameObject(RuntimeRegistryName);
        return root.AddComponent<TutorialEnemyRegistry>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TutorialEnemyRegistry] Multiple tutorial enemy registries were found. The latest one will be used.", this);
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Register(TutorialEnemyObject enemy)
    {
        if (enemy == null || enemies.Contains(enemy))
            return;

        enemies.Add(enemy);
        EnemyRegistered?.Invoke(enemy);
    }

    public void Unregister(TutorialEnemyObject enemy)
    {
        if (enemy == null || !enemies.Remove(enemy))
            return;

        EnemyUnregistered?.Invoke(enemy);
    }

    public void RefreshSceneEnemies()
    {
        enemies.Clear();
        TutorialEnemyObject[] sceneEnemies = FindObjectsByType<TutorialEnemyObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < sceneEnemies.Length; i++)
            Register(sceneEnemies[i]);
    }

    public TutorialEnemyObject GetEnemyAtGrid(Vector2Int grid)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            TutorialEnemyObject enemy = enemies[i];
            if (enemy != null && enemy.GetCurrentGrid() == grid)
                return enemy;
        }

        return null;
    }

    public TutorialEnemyObject GetEnemyByInteractionCell(Vector2Int grid)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            TutorialEnemyObject enemy = enemies[i];
            if (enemy != null && enemy.IsInteractionCell(grid))
                return enemy;
        }

        return null;
    }
}
