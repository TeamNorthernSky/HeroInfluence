using System.Collections.Generic;
using UnityEngine;

public class EnemyFogVisibilitySystem : MonoBehaviour
{
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private bool hideChildCanvases = true;
    [SerializeField] private bool hideColliders = false;

    private readonly Dictionary<EnemyGridMover, EnemyVisibilityBinding> bindings = new Dictionary<EnemyGridMover, EnemyVisibilityBinding>();

    private sealed class EnemyVisibilityBinding
    {
        public EnemyGridMover Enemy;
        public Renderer[] Renderers;
        public Canvas[] Canvases;
        public Collider[] Colliders;
    }

    private void OnEnable()
    {
        if (enemyRegistry != null)
        {
            enemyRegistry.EnemyRegistered += HandleEnemyRegistered;
            enemyRegistry.EnemyUnregistered += HandleEnemyUnregistered;
        }

        if (fogGridManager != null)
            fogGridManager.FogChanged += HandleFogChanged;

        RegisterExistingEnemies();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (enemyRegistry != null)
        {
            enemyRegistry.EnemyRegistered -= HandleEnemyRegistered;
            enemyRegistry.EnemyUnregistered -= HandleEnemyUnregistered;
        }

        if (fogGridManager != null)
            fogGridManager.FogChanged -= HandleFogChanged;

        foreach (KeyValuePair<EnemyGridMover, EnemyVisibilityBinding> pair in bindings)
        {
            if (pair.Key == null)
                continue;

            pair.Key.GridChanged -= HandleEnemyGridChanged;
            pair.Key.MoveStepStarted -= HandleEnemyMoveStepStarted;
        }

        bindings.Clear();
    }

    [ContextMenu("Refresh Enemy Visibility")]
    public void RefreshAll()
    {
        if (fogGridManager == null)
            return;

        foreach (KeyValuePair<EnemyGridMover, EnemyVisibilityBinding> pair in bindings)
        {
            EnemyGridMover enemy = pair.Key;
            if (enemy == null)
                continue;

            ApplyVisibility(pair.Value, fogGridManager.IsVisible(enemy.GetCurrentGrid()));
        }
    }

    private void RegisterExistingEnemies()
    {
        if (enemyRegistry == null)
            return;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
            RegisterEnemy(enemies[i]);
    }

    private void HandleEnemyRegistered(EnemyGridMover enemy)
    {
        RegisterEnemy(enemy);
        RefreshEnemy(enemy);
    }

    private void HandleEnemyUnregistered(EnemyGridMover enemy)
    {
        if (enemy == null)
            return;

        enemy.GridChanged -= HandleEnemyGridChanged;
        bindings.Remove(enemy);
    }

    private void HandleFogChanged()
    {
        RefreshAll();
    }

    private void HandleEnemyGridChanged(EnemyGridMover enemy, Vector2Int _)
    {
        RefreshEnemy(enemy);
    }

    private void HandleEnemyMoveStepStarted(EnemyGridMover enemy, Vector2Int nextGrid)
    {
        if (enemy == null || fogGridManager == null)
            return;

        if (!bindings.TryGetValue(enemy, out EnemyVisibilityBinding binding))
            return;

        if (fogGridManager.IsVisible(nextGrid))
            ApplyVisibility(binding, true);
    }

    private void RefreshEnemy(EnemyGridMover enemy)
    {
        if (enemy == null || fogGridManager == null)
            return;

        if (!bindings.TryGetValue(enemy, out EnemyVisibilityBinding binding))
            return;

        ApplyVisibility(binding, fogGridManager.IsVisible(enemy.GetCurrentGrid()));
    }

    private void RegisterEnemy(EnemyGridMover enemy)
    {
        if (enemy == null || bindings.ContainsKey(enemy))
            return;

        var binding = new EnemyVisibilityBinding
        {
            Enemy = enemy,
            Renderers = enemy.GetComponentsInChildren<Renderer>(true),
            Canvases = hideChildCanvases ? enemy.GetComponentsInChildren<Canvas>(true) : System.Array.Empty<Canvas>(),
            Colliders = hideColliders ? enemy.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>()
        };

        bindings.Add(enemy, binding);
        enemy.GridChanged += HandleEnemyGridChanged;
        enemy.MoveStepStarted += HandleEnemyMoveStepStarted;
    }

    private static void ApplyVisibility(EnemyVisibilityBinding binding, bool isVisible)
    {
        if (binding == null)
            return;

        for (int i = 0; i < binding.Renderers.Length; i++)
        {
            Renderer renderer = binding.Renderers[i];
            if (renderer != null)
                renderer.enabled = isVisible;
        }

        for (int i = 0; i < binding.Canvases.Length; i++)
        {
            Canvas canvas = binding.Canvases[i];
            if (canvas != null)
                canvas.enabled = isVisible;
        }

        for (int i = 0; i < binding.Colliders.Length; i++)
        {
            Collider collider = binding.Colliders[i];
            if (collider != null)
                collider.enabled = isVisible;
        }
    }
}
