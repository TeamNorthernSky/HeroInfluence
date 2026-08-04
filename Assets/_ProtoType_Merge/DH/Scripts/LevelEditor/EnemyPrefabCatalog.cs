using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Enemy Prefab Catalog")]
public class EnemyPrefabCatalog : ScriptableObject
{
    [Header("Group Prefabs")]
    [SerializeField] private EnemyGridMover placedEnemyGroupPrefab;
    [SerializeField] private EnemyGridMover runtimeMobileEnemyGroupPrefab;
    [SerializeField] private EnemyGridMover runtimeStaticEnemyGroupPrefab;

    [Header("Unit Prefabs")]
    [SerializeField] private List<EnemyUnitPrefabEntry> enemyUnitPrefabs = new List<EnemyUnitPrefabEntry>();

    public EnemyGridMover PlacedEnemyGroupPrefab => placedEnemyGroupPrefab;
    public EnemyGridMover RuntimeMobileEnemyGroupPrefab => runtimeMobileEnemyGroupPrefab;
    public EnemyGridMover RuntimeStaticEnemyGroupPrefab => runtimeStaticEnemyGroupPrefab;

    public bool TryGetPlacedEnemyGroupPrefab(out EnemyGridMover prefab)
    {
        prefab = placedEnemyGroupPrefab;
        return prefab != null;
    }

    public bool TryGetRuntimeEnemyGroupPrefab(EnemyBehaviorType behaviorType, out EnemyGridMover prefab)
    {
        prefab = behaviorType == EnemyBehaviorType.Static
            ? runtimeStaticEnemyGroupPrefab
            : runtimeMobileEnemyGroupPrefab;

        return prefab != null;
    }

    public bool TryGetEnemyUnitPrefab(int enemyUnitIndex, out EnemyUnitState prefab)
    {
        if (enemyUnitPrefabs != null)
        {
            for (int i = 0; i < enemyUnitPrefabs.Count; i++)
            {
                if (enemyUnitPrefabs[i].EnemyUnitIndex != enemyUnitIndex)
                    continue;

                prefab = enemyUnitPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct EnemyUnitPrefabEntry
{
    [SerializeField] private int enemyUnitIndex;
    [SerializeField] private EnemyUnitState prefab;

    public int EnemyUnitIndex => enemyUnitIndex;
    public EnemyUnitState Prefab => prefab;
}
