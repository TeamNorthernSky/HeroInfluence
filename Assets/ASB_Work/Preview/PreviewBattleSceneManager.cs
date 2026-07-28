using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;

using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// PreViewsScene bootstrapper. It prepares a fixed 1:1 sandbox without starting
/// the normal battle flow, reward, save, or scene transition path.
/// </summary>
public class PreviewBattleSceneManager : MonoBehaviour
{
    [System.Serializable]
    private sealed class PreviewEnemy
    {
        [SerializeField] private string unitId;
        [SerializeField] private int gridNumber;

        public string UnitId => unitId;
        public int GridNumber => gridNumber;

        public PreviewEnemy(string unitId, int gridNumber)
        {
            this.unitId = unitId;
            this.gridNumber = gridNumber;
        }
    }
    [Header("Scene Roots")]
    [SerializeField] private Transform playerPlace;
    [SerializeField] private Transform enemyPlace;

        // Fixed design anchors for editor-only presentation-path editing.
            // Runtime battle logic continues to use the actual spawned units.
            public Transform PlayerPreviewAnchor => playerPlace;
            public Transform EnemyPreviewAnchor => enemyPlace;
        
            [Header("Spawners")]
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Preview Units")]
    [SerializeField] private string playerUnitId = "10001";
    [SerializeField] private int playerGridNumber = 0;
    [SerializeField] private string enemyUnitId = "20001";
    [SerializeField] private int enemyGridNumber = 200;
    [SerializeField] private List<PreviewEnemy> previewEnemies = new List<PreviewEnemy>
    {
        new PreviewEnemy("20001", 200),
        new PreviewEnemy("20001", 201),
        new PreviewEnemy("20001", 202),
    };

    [Header("Preview Controller")]
    [SerializeField] private SkillPresentationPreviewController previewController;

    [Header("Cleanup")]
    [SerializeField] private bool removeExistingPlacedUnits = true;

    private void Start()
    {
        ResolveReferences();

        if (DHCsvTemplateCatalog.Instance == null)
        {
            Debug.LogWarning("[PreviewBattleSceneManager] DHCsvTemplateCatalog.Instance is not ready; preview spawn skipped.");
            return;
        }

        playerSpawner?.SetSpawnOnStart(false);
        enemySpawner?.SetSpawnOnStart(false);

        if (removeExistingPlacedUnits)
        {
            RemovePlacedUnits(playerPlace);
            RemovePlacedUnits(enemyPlace);
        }

        BattleCharactor actor = SpawnPlayerPreviewUnit();
        List<BattleCharactor> targets = SpawnEnemyPreviewUnits();
        BattleCharactor target = targets.Count > 0 ? targets[0] : null;

        BattleGridManager.Instance?.RebuildCache();

        if (previewController != null)
        {
            previewController.SetUnits(actor, target);
        }
        else
        {
            Debug.LogWarning("[PreviewBattleSceneManager] previewController is missing.");
        }
    }

    private void ResolveReferences()
    {
        if (playerSpawner == null)
        {
            playerSpawner = FindFirstObjectByType<PlayerSpawner>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
        }

        if (previewController == null)
        {
            previewController = FindFirstObjectByType<SkillPresentationPreviewController>();
        }

        if (playerPlace == null && playerSpawner != null)
        {
            playerPlace = playerSpawner.transform;
        }

        if (enemyPlace == null && enemySpawner != null)
        {
            enemyPlace = enemySpawner.transform;
        }
    }

    private BattleCharactor SpawnPlayerPreviewUnit()
    {
        if (playerSpawner == null)
        {
            Debug.LogWarning("[PreviewBattleSceneManager] playerSpawner is missing.");
            return null;
        }

        GameObject spawned = playerSpawner.SpawnUnit(playerUnitId, playerGridNumber);
        return ResolveSpawnedBattleCharactor(spawned, "player");
    }

    private List<BattleCharactor> SpawnEnemyPreviewUnits()
    {
        List<BattleCharactor> spawnedEnemies = new List<BattleCharactor>();

        if (enemySpawner == null)
        {
            Debug.LogWarning("[PreviewBattleSceneManager] enemySpawner is missing.");
            return spawnedEnemies;
        }

        if (previewEnemies == null || previewEnemies.Count == 0)
        {
            BattleCharactor legacyEnemy = SpawnEnemyPreviewUnit(enemyUnitId, enemyGridNumber);
            if (legacyEnemy != null)
            {
                spawnedEnemies.Add(legacyEnemy);
            }

            return spawnedEnemies;
        }

        for (int i = 0; i < previewEnemies.Count; i++)
        {
            PreviewEnemy previewEnemy = previewEnemies[i];
            if (previewEnemy == null || string.IsNullOrWhiteSpace(previewEnemy.UnitId))
            {
                continue;
            }

            BattleCharactor enemy = SpawnEnemyPreviewUnit(previewEnemy.UnitId, previewEnemy.GridNumber);
            if (enemy != null)
            {
                spawnedEnemies.Add(enemy);
            }
        }

        return spawnedEnemies;
    }

    private BattleCharactor SpawnEnemyPreviewUnit(string unitId, int gridNumber)
    {
        GameObject spawned = enemySpawner.SpawnUnit(unitId, gridNumber);
        return ResolveSpawnedBattleCharactor(spawned, "enemy");
    }

    private static BattleCharactor ResolveSpawnedBattleCharactor(GameObject spawned, string label)
    {
        if (spawned == null)
        {
            Debug.LogWarning($"[PreviewBattleSceneManager] {label} preview unit spawn failed.");
            return null;
        }

        BattleCharactor battle = spawned.GetComponentInChildren<BattleCharactor>(true);
        if (battle == null)
        {
            Debug.LogWarning($"[PreviewBattleSceneManager] Spawned {label} unit has no BattleCharactor: {spawned.name}");
        }

        return battle;
    }

    private static void RemovePlacedUnits(Transform root)
    {
        if (root == null)
        {
            return;
        }

        BattleCharactor[] units = root.GetComponentsInChildren<BattleCharactor>(true);
        HashSet<GameObject> destroyedRoots = new HashSet<GameObject>();

        for (int i = 0; i < units.Length; i++)
        {
            BattleCharactor unit = units[i];
            if (unit == null)
            {
                continue;
            }

            GameObject unitObject = unit.gameObject;
            if (unitObject == null || !destroyedRoots.Add(unitObject))
            {
                continue;
            }

            GridCellRef parentCell = unit.GetComponentInParent<GridCellRef>();
            unit.ClearOccupiedCell();
            if (parentCell != null)
            {
                parentCell.ClearIfOccupying(unit);
            }
            unitObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(unitObject);
            }
            else
            {
                DestroyImmediate(unitObject);
            }
        }
    }
}
