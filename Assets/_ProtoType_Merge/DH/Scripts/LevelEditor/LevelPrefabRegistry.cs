using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LevelPrefabRegistry : MonoBehaviour
{
    [Header("Default Prefabs")]
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Item Prefabs")]
    [SerializeField] private List<ItemPrefabEntry> itemPrefabs = new List<ItemPrefabEntry>();

    [Header("Outpost Prefabs")]
    [FormerlySerializedAs("minePrefabs")]
    [SerializeField] private List<OutpostPrefabEntry> outpostPrefabs = new List<OutpostPrefabEntry>();

    [Header("Event Prefabs")]
    [SerializeField] private MapEventObject defaultEventPrefab;
    [SerializeField] private List<EventPrefabEntry> eventPrefabs = new List<EventPrefabEntry>();

    [Header("Main Event Prefabs")]
    [SerializeField] private List<MainEventPrefabEntry> mainEventPrefabs = new List<MainEventPrefabEntry>();

    [Header("Sub Event Prefabs")]
    [SerializeField] private List<SubEventPrefabEntry> subEventPrefabs = new List<SubEventPrefabEntry>();

    [Header("Unique Building Prefabs")]
    [SerializeField] private HeroUnionUnit heroUnionPrefab;
    [SerializeField] private List<HeroUnionPrefabEntry> heroUnionPrefabs = new List<HeroUnionPrefabEntry>();
    [SerializeField] private VillainUnionBase villainUnionBasePrefab;

    [Header("Decorative Building Prefabs")]
    [SerializeField] private List<DecorativeBuildingPrefabEntry> decorativeBuildingPrefabs = new List<DecorativeBuildingPrefabEntry>();

    [Header("Gate Prefabs")]
    [SerializeField] private List<GatePrefabEntry> gatePrefabs = new List<GatePrefabEntry>();

    [Header("Enemy Prefabs")]
    [SerializeField] private EnemyGridMover enemyGroupPrefab;
    [SerializeField] private List<EnemyUnitPrefabEntry> enemyUnitPrefabs = new List<EnemyUnitPrefabEntry>();

    [Header("Player Unit Prefabs")]
    [SerializeField] private List<PlayerUnitPrefabEntry> playerUnitPrefabs = new List<PlayerUnitPrefabEntry>();

    public GameObject ObstaclePrefab => obstaclePrefab;
    public HeroUnionUnit HeroUnionPrefab => heroUnionPrefab;
    public VillainUnionBase VillainUnionBasePrefab => villainUnionBasePrefab;
    public IReadOnlyList<HeroUnionPrefabEntry> HeroUnionPrefabs =>
        heroUnionPrefabs != null
            ? heroUnionPrefabs
            : Array.Empty<HeroUnionPrefabEntry>();
    public IReadOnlyList<DecorativeBuildingPrefabEntry> DecorativeBuildingPrefabs =>
        decorativeBuildingPrefabs != null
            ? decorativeBuildingPrefabs
            : Array.Empty<DecorativeBuildingPrefabEntry>();
    public IReadOnlyList<MainEventPrefabEntry> MainEventPrefabs =>
        mainEventPrefabs != null
            ? mainEventPrefabs
            : Array.Empty<MainEventPrefabEntry>();
    public IReadOnlyList<SubEventPrefabEntry> SubEventPrefabs =>
        subEventPrefabs != null
            ? subEventPrefabs
            : Array.Empty<SubEventPrefabEntry>();
    public IReadOnlyList<GatePrefabEntry> GatePrefabs =>
        gatePrefabs != null
            ? gatePrefabs
            : Array.Empty<GatePrefabEntry>();

    private void OnValidate()
    {
        for (int i = 0; i < outpostPrefabs.Count; i++)
            outpostPrefabs[i] = outpostPrefabs[i].Normalized();

        heroUnionPrefabs ??= new List<HeroUnionPrefabEntry>();
        for (int i = 0; i < heroUnionPrefabs.Count; i++)
            heroUnionPrefabs[i] = heroUnionPrefabs[i].Normalized();

        decorativeBuildingPrefabs ??= new List<DecorativeBuildingPrefabEntry>();
        for (int i = 0; i < decorativeBuildingPrefabs.Count; i++)
            decorativeBuildingPrefabs[i] = decorativeBuildingPrefabs[i].Normalized();

        mainEventPrefabs ??= new List<MainEventPrefabEntry>();
        for (int i = 0; i < mainEventPrefabs.Count; i++)
            mainEventPrefabs[i] = mainEventPrefabs[i].Normalized();

        subEventPrefabs ??= new List<SubEventPrefabEntry>();
        for (int i = 0; i < subEventPrefabs.Count; i++)
            subEventPrefabs[i] = subEventPrefabs[i].Normalized();

        gatePrefabs ??= new List<GatePrefabEntry>();
        for (int i = 0; i < gatePrefabs.Count; i++)
            gatePrefabs[i] = gatePrefabs[i].Normalized();
    }

    public bool TryGetItemPrefab(ResourceType resourceType, out ItemObject prefab)
    {
        for (int i = 0; i < itemPrefabs.Count; i++)
        {
            if (itemPrefabs[i].ResourceType != resourceType)
                continue;

            prefab = itemPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetOutpostPrefab(OutpostType outpostType, out Outpost prefab)
    {
        outpostType = OutpostTypeUtility.Normalize(outpostType);

        for (int i = 0; i < outpostPrefabs.Count; i++)
        {
            if (outpostPrefabs[i].OutpostType != outpostType)
                continue;

            prefab = outpostPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetEventPrefab(MapEventType eventType, out MapEventObject prefab)
    {
        for (int i = 0; i < eventPrefabs.Count; i++)
        {
            EventPrefabEntry entry = eventPrefabs[i];
            if (entry.EventType != eventType)
                continue;

            prefab = entry.Prefab;
            return prefab != null;
        }

        prefab = defaultEventPrefab;
        return prefab != null;
    }

    public bool TryGetHeroUnionPrefab(out HeroUnionUnit prefab)
    {
        return TryGetHeroUnionPrefab(string.Empty, out prefab);
    }

    public bool TryGetHeroUnionPrefab(string prefabKey, out HeroUnionUnit prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (!string.IsNullOrEmpty(normalizedKey) && heroUnionPrefabs != null)
        {
            for (int i = 0; i < heroUnionPrefabs.Count; i++)
            {
                if (!string.Equals(heroUnionPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = heroUnionPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = heroUnionPrefab;
        return prefab != null;
    }

    public bool TryGetVillainUnionBasePrefab(out VillainUnionBase prefab)
    {
        prefab = villainUnionBasePrefab;
        return prefab != null;
    }

    public bool TryGetDecorativeBuildingPrefab(string prefabKey, out GameObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (decorativeBuildingPrefabs == null)
        {
            prefab = null;
            return false;
        }

        for (int i = 0; i < decorativeBuildingPrefabs.Count; i++)
        {
            if (!string.Equals(decorativeBuildingPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                continue;

            prefab = decorativeBuildingPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetMainEventPrefab(string prefabKey, out MainEventObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (mainEventPrefabs == null)
        {
            prefab = null;
            return false;
        }

        for (int i = 0; i < mainEventPrefabs.Count; i++)
        {
            if (!string.Equals(mainEventPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                continue;

            prefab = mainEventPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetSubEventPrefab(string prefabKey, out SubEventObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (subEventPrefabs == null)
        {
            prefab = null;
            return false;
        }

        for (int i = 0; i < subEventPrefabs.Count; i++)
        {
            if (!string.Equals(subEventPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                continue;

            prefab = subEventPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetGatePrefab(string prefabKey, out GateFootprint prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (gatePrefabs == null)
        {
            prefab = null;
            return false;
        }

        for (int i = 0; i < gatePrefabs.Count; i++)
        {
            if (!string.Equals(gatePrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                continue;

            prefab = gatePrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetEnemyGroupPrefab(out EnemyGridMover prefab)
    {
        prefab = enemyGroupPrefab;
        return prefab != null;
    }

    public bool TryGetEnemyUnitPrefab(int enemyUnitIndex, out EnemyUnitState prefab)
    {
        for (int i = 0; i < enemyUnitPrefabs.Count; i++)
        {
            if (enemyUnitPrefabs[i].EnemyUnitIndex != enemyUnitIndex)
                continue;

            prefab = enemyUnitPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetPlayerUnitPrefab(string unitTemplateKey, out PartyUnitState prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        for (int i = 0; i < playerUnitPrefabs.Count; i++)
        {
            if (!string.Equals(playerUnitPrefabs[i].UnitTemplateKey, normalizedKey, StringComparison.Ordinal))
                continue;

            prefab = playerUnitPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct ItemPrefabEntry
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private ItemObject prefab;

    public ResourceType ResourceType => resourceType;
    public ItemObject Prefab => prefab;
}

[Serializable]
public struct OutpostPrefabEntry
{
    [FormerlySerializedAs("resourceType")]
    [SerializeField] private OutpostType outpostType;
    [SerializeField] private Outpost prefab;

    public OutpostType OutpostType => OutpostTypeUtility.Normalize(outpostType);
    public Outpost Prefab => prefab;

    public OutpostPrefabEntry Normalized()
    {
        OutpostPrefabEntry entry = this;
        entry.outpostType = OutpostType;
        return entry;
    }
}

[Serializable]
public struct EventPrefabEntry
{
    [SerializeField] private MapEventType eventType;
    [SerializeField] private MapEventObject prefab;

    public MapEventType EventType => eventType;
    public string EventKey => MapEventTypeUtility.ToEventKey(eventType);
    public MapEventObject Prefab => prefab;
}

[Serializable]
public struct HeroUnionPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private HeroUnionUnit prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public HeroUnionUnit Prefab => prefab;

    public HeroUnionPrefabEntry Normalized()
    {
        HeroUnionPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}

[Serializable]
public struct DecorativeBuildingPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private GameObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            DecorativeBuildingPlacement placement = prefab != null ? prefab.GetComponent<DecorativeBuildingPlacement>() : null;
            return placement != null ? placement.PrefabKey : string.Empty;
        }
    }

    public GameObject Prefab => prefab;

    public DecorativeBuildingPrefabEntry Normalized()
    {
        DecorativeBuildingPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}

[Serializable]
public struct MainEventPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private MainEventObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public MainEventObject Prefab => prefab;

    public MainEventPrefabEntry Normalized()
    {
        MainEventPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}

[Serializable]
public struct SubEventPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private SubEventObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public SubEventObject Prefab => prefab;

    public SubEventPrefabEntry Normalized()
    {
        SubEventPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}

[Serializable]
public struct GatePrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private GateFootprint prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public GateFootprint Prefab => prefab;

    public GatePrefabEntry Normalized()
    {
        GatePrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
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

[Serializable]
public struct PlayerUnitPrefabEntry
{
    [SerializeField] private string unitTemplateKey;
    [SerializeField] private PartyUnitState prefab;

    public string UnitTemplateKey => string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
    public PartyUnitState Prefab => prefab;
}
