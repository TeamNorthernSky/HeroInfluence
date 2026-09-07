using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LevelPrefabRegistry : MonoBehaviour
{
    // Catalog hub for level loading. Most prefab groups live in ScriptableObject catalogs now.
    [Header("Default Prefabs")]
    // Still direct because obstacle prefab also acts as the gate blocker fallback.
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Item Prefabs")]
    [SerializeField] private ItemPrefabCatalog itemCatalog;

    [Header("Outpost Prefabs")]
    [SerializeField] private OutpostPrefabCatalog outpostCatalog;

    [Header("Event Prefabs")]
    [SerializeField] private EventPrefabCatalog eventCatalog;

    [Header("World Event NPC Prefabs")]
    [SerializeField] private WorldEventNpcPrefabCatalog worldEventNpcCatalog;

    [Header("Unique Building Prefabs")]
    [SerializeField] private HeroUnionPrefabCatalog heroUnionCatalog;
    // Single direct prefab for now; catalog split can wait until multiple villain union variants exist.
    [SerializeField] private VillainUnionBase villainUnionBasePrefab;

    [Header("Decorative Object Prefabs")]
    [FormerlySerializedAs("decorativeBuildingCatalog")]
    [SerializeField] private DecorativeObjectPrefabCatalog decorativeObjectCatalog;

    [Header("Gate Prefabs")]
    [SerializeField] private GatePrefabCatalog gateCatalog;

    [Header("Enemy Prefabs")]
    [SerializeField] private EnemyPrefabCatalog enemyCatalog;

    [Header("Player Unit Prefabs")]
    [SerializeField] private PlayerUnitPrefabCatalog playerUnitCatalog;

    public GameObject ObstaclePrefab => obstaclePrefab;
    public VillainUnionBase VillainUnionBasePrefab => villainUnionBasePrefab;
    public IReadOnlyList<HeroUnionPrefabEntry> HeroUnionPrefabs =>
        heroUnionCatalog != null
            ? heroUnionCatalog.HeroUnionPrefabs
            : Array.Empty<HeroUnionPrefabEntry>();
    public IReadOnlyList<DecorativeObjectPrefabEntry> DecorativeObjectPrefabs =>
        decorativeObjectCatalog != null
            ? decorativeObjectCatalog.DecorativeObjectPrefabs
            : Array.Empty<DecorativeObjectPrefabEntry>();
    public IReadOnlyList<MainEventPrefabEntry> MainEventPrefabs =>
        eventCatalog != null
            ? eventCatalog.MainEventPrefabs
            : Array.Empty<MainEventPrefabEntry>();
    public IReadOnlyList<SubEventPrefabEntry> SubEventPrefabs =>
        eventCatalog != null
            ? eventCatalog.SubEventPrefabs
            : Array.Empty<SubEventPrefabEntry>();
    public IReadOnlyList<GatePrefabEntry> GatePrefabs =>
        gateCatalog != null
            ? gateCatalog.GatePrefabs
            : Array.Empty<GatePrefabEntry>();

    public bool TryGetItemPrefab(ResourceType resourceType, out ItemObject prefab)
    {
        if (itemCatalog != null && itemCatalog.TryGetItemPrefab(resourceType, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetOutpostPrefab(OutpostType outpostType, out Outpost prefab)
    {
        if (outpostCatalog != null && outpostCatalog.TryGetOutpostPrefab(outpostType, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetEventPrefab(MapEventType eventType, out MapEventObject prefab)
    {
        if (eventCatalog != null && eventCatalog.TryGetEventPrefab(eventType, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetHeroUnionPrefab(out HeroUnionUnit prefab)
    {
        return TryGetHeroUnionPrefab(string.Empty, out prefab);
    }

    public bool TryGetHeroUnionPrefab(string prefabKey, out HeroUnionUnit prefab)
    {
        if (heroUnionCatalog != null && heroUnionCatalog.TryGetHeroUnionPrefab(prefabKey, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetVillainUnionBasePrefab(out VillainUnionBase prefab)
    {
        prefab = villainUnionBasePrefab;
        return prefab != null;
    }

    public bool TryGetDecorativeObjectPrefab(string prefabKey, out GameObject prefab)
    {
        if (decorativeObjectCatalog != null &&
            decorativeObjectCatalog.TryGetDecorativeObjectPrefab(prefabKey, out prefab))
        {
            return true;
        }

        prefab = null;
        return false;
    }

    public bool TryGetMainEventPrefab(string prefabKey, out MainEventObject prefab)
    {
        if (eventCatalog != null && eventCatalog.TryGetMainEventPrefab(prefabKey, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetSubEventPrefab(string prefabKey, out SubEventObject prefab)
    {
        if (eventCatalog != null && eventCatalog.TryGetSubEventPrefab(prefabKey, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetWorldEventNpcPrefab(int npcType, out GameObject prefab)
    {
        if (worldEventNpcCatalog != null && worldEventNpcCatalog.TryGetNpcPrefab(npcType, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetGatePrefab(string prefabKey, out GateFootprint prefab)
    {
        if (gateCatalog != null && gateCatalog.TryGetGatePrefab(prefabKey, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetEnemyGroupPrefab(out EnemyGridMover prefab)
    {
        if (enemyCatalog != null && enemyCatalog.TryGetPlacedEnemyGroupPrefab(out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetRuntimeEnemyGroupPrefab(EnemyBehaviorType behaviorType, out EnemyGridMover prefab)
    {
        if (enemyCatalog != null && enemyCatalog.TryGetRuntimeEnemyGroupPrefab(behaviorType, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetEnemyUnitPrefab(int enemyUnitIndex, out EnemyUnitState prefab)
    {
        if (enemyCatalog != null && enemyCatalog.TryGetEnemyUnitPrefab(enemyUnitIndex, out prefab))
            return true;

        prefab = null;
        return false;
    }

    public bool TryGetPlayerUnitPrefab(string unitTemplateKey, out PartyUnitState prefab)
    {
        if (playerUnitCatalog != null && playerUnitCatalog.TryGetPlayerUnitPrefab(unitTemplateKey, out prefab))
            return true;

        prefab = null;
        return false;
    }
}

