using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRegistry : MonoBehaviour
{
    private readonly List<EnemyGridMover> enemies = new List<EnemyGridMover>();

    public IReadOnlyList<EnemyGridMover> Enemies => enemies;
    public event Action<EnemyGridMover> EnemyRegistered;
    public event Action<EnemyGridMover> EnemyUnregistered;

    private void Awake()
    {
        RefreshSceneEnemies();
    }

    public void Register(EnemyGridMover enemy)
    {
        if (enemy == null || enemies.Contains(enemy))
            return;

        enemies.Add(enemy);
        EnemyRegistered?.Invoke(enemy);
    }

    public void Unregister(EnemyGridMover enemy)
    {
        if (enemy == null)
            return;

        if (!enemies.Remove(enemy))
            return;

        EnemyUnregistered?.Invoke(enemy);
    }

    [ContextMenu("Refresh Scene Enemies")]
    public void RefreshSceneEnemies()
    {
        enemies.Clear();

        EnemyGridMover[] sceneEnemies = FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneEnemies.Length; i++)
        {
            EnemyGridMover enemy = sceneEnemies[i];
            if (enemy == null)
                continue;

            Register(enemy);
        }
    }

    public EnemyGridMover GetClosestEnemy(Vector2Int grid)
    {
        EnemyGridMover closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            int distance = GridManager.GridDistance(grid, enemy.GetCurrentGrid());
            if (distance >= closestDistance)
                continue;

            closest = enemy;
            closestDistance = distance;
        }

        return closest;
    }
}
