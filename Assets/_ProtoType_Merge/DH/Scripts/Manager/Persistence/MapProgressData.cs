using System;
using UnityEngine;

[Serializable]
public class PartyWorldState
{
    [SerializeField] private string placementKey;
    [SerializeField] private string partyId;
    [SerializeField] private Vector2Int grid;
    [SerializeField] private bool removed;

    public string PlacementKey => placementKey;
    public string PartyId => partyId;
    public Vector2Int Grid => grid;
    public bool Removed => removed;

    public PartyWorldState(string placementKey, string partyId, Vector2Int grid)
    {
        this.placementKey = placementKey;
        this.partyId = partyId;
        this.grid = grid;
        removed = false;
    }

    public void SetPartyId(string nextPartyId)
    {
        partyId = nextPartyId;
    }

    public void SetGrid(Vector2Int nextGrid)
    {
        grid = nextGrid;
    }

    public void SetRemoved(bool nextRemoved)
    {
        removed = nextRemoved;
    }
}

[Serializable]
public class EnemyWorldState
{
    public const string DefaultPrefabKey = "default";

    [SerializeField] private string placementKey;
    [SerializeField] private string enemyId;
    [SerializeField] private Vector2Int grid;
    [SerializeField] private bool defeated;
    [SerializeField] private EnemyPlacementSource placementSource = EnemyPlacementSource.Scene;
    [SerializeField] private string prefabKey = DefaultPrefabKey;

    public string PlacementKey => placementKey;
    public string EnemyId => enemyId;
    public Vector2Int Grid => grid;
    public bool Defeated => defeated;
    public EnemyPlacementSource PlacementSource => placementSource;
    public string PrefabKey => prefabKey;

    public EnemyWorldState(string placementKey, string enemyId, Vector2Int grid)
        : this(placementKey, enemyId, grid, EnemyPlacementSource.Scene, DefaultPrefabKey)
    {
    }

    public EnemyWorldState(
        string placementKey,
        string enemyId,
        Vector2Int grid,
        EnemyPlacementSource placementSource,
        string prefabKey)
    {
        this.placementKey = placementKey;
        this.enemyId = enemyId;
        this.grid = grid;
        this.placementSource = placementSource;
        this.prefabKey = string.IsNullOrWhiteSpace(prefabKey) ? DefaultPrefabKey : prefabKey;
        defeated = false;
    }

    public void SetEnemyId(string nextEnemyId)
    {
        enemyId = nextEnemyId;
    }

    public void SetGrid(Vector2Int nextGrid)
    {
        grid = nextGrid;
    }

    public void SetDefeated(bool nextDefeated)
    {
        defeated = nextDefeated;
    }

    public void SetPlacementSource(EnemyPlacementSource nextPlacementSource)
    {
        placementSource = nextPlacementSource;
    }

    public void SetPrefabKey(string nextPrefabKey)
    {
        prefabKey = string.IsNullOrWhiteSpace(nextPrefabKey) ? DefaultPrefabKey : nextPrefabKey;
    }
}

[Serializable]
public class OutpostProgressState
{
    [SerializeField] private string outpostKey;
    [SerializeField] private OutpostState state;

    public string OutpostKey => outpostKey;
    public OutpostState State => state;

    public OutpostProgressState(string outpostKey, OutpostState state)
    {
        this.outpostKey = outpostKey;
        this.state = state;
    }

    public void SetState(OutpostState nextState)
    {
        state = nextState;
    }
}
