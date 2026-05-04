using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Outpost : MonoBehaviour
{
    public static event Action<Outpost> OutpostClaimed;

    [Header("Data")]
    [FormerlySerializedAs("mineType")]
    [SerializeField] private OutpostType outpostType = OutpostType.Bank;
    public int resourcePerTurn;
    [FormerlySerializedAs("mineState")]
    public OutpostState outpostState = OutpostState.Unclaimed;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material unclaimedMaterial;
    [SerializeField] private Material enemyClaimedMaterial;
    [SerializeField] private Material claimedMaterial;
    private MultiGridOccupant multiGridOccupant;
    private OutpostRegistry outpostRegistry;

    public bool IsClaimableByPlayer => outpostState == OutpostState.Unclaimed || outpostState == OutpostState.EnemyClaimed;
    public bool IsPlayerClaimed => outpostState == OutpostState.Claimed;
    public bool IsEnemyClaimed => outpostState == OutpostState.EnemyClaimed;
    public OutpostType OutpostType => outpostType;

    private void Awake()
    {
        multiGridOccupant = GetComponent<MultiGridOccupant>();

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        ApplyStateMaterial();
    }

    private void OnEnable()
    {
        ResolveOutpostRegistry();
        outpostRegistry?.Register(this);
    }

    private void OnDisable()
    {
        outpostRegistry?.Unregister(this);
    }

    public void Claim()
    {
        if (IsClaimableByPlayer)
        {
            outpostState = OutpostState.Claimed;
            ApplyStateMaterial();
            OutpostClaimed?.Invoke(this);
        }
    }

    public void EnemyClaim()
    {
        if (outpostState == OutpostState.EnemyClaimed)
            return;

        outpostState = OutpostState.EnemyClaimed;
        ApplyStateMaterial();
    }

    public void ProduceForTurn(ResourceManager resourceManager)
    {
        if (!IsPlayerClaimed)
            return;

        if (resourceManager == null || resourcePerTurn <= 0)
            return;

        switch (outpostType)
        {
            case OutpostType.Bank:
                resourceManager.AddResource(ResourceType.Money, resourcePerTurn);
                break;
            case OutpostType.Composite:
                resourceManager.AddResource(ResourceType.Chip, resourcePerTurn);
                resourceManager.AddResource(ResourceType.Crystal, resourcePerTurn);
                resourceManager.AddResource(ResourceType.Supply, resourcePerTurn);
                break;
        }
    }

    public void ApplyInitialData(int nextResourcePerTurn, OutpostState nextState)
    {
        resourcePerTurn = nextResourcePerTurn;
        outpostState = nextState;
        ApplyStateMaterial();
    }

    private void ApplyStateMaterial()
    {
        if (targetRenderer == null)
            return;

        Material nextMaterial = outpostState switch
        {
            OutpostState.Claimed => claimedMaterial,
            OutpostState.EnemyClaimed => enemyClaimedMaterial,
            _ => unclaimedMaterial
        };
        if (nextMaterial == null)
            return;

        targetRenderer.material = nextMaterial;
    }

    private void ResolveOutpostRegistry()
    {
        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();
    }

    public Vector2Int GetAnchorGrid(GridManager gridManager)
    {
        if (multiGridOccupant != null)
            return multiGridOccupant.AnchorGrid;

        return gridManager != null
            ? gridManager.WorldToGrid(transform.position)
            : Vector2Int.zero;
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager gridManager)
    {
        if (multiGridOccupant != null)
            return multiGridOccupant.OccupiesCell(grid);

        return GetAnchorGrid(gridManager) == grid;
    }

    public IReadOnlyList<Vector2Int> GetAdjacentInteractionCells(GridManager gridManager)
    {
        if (multiGridOccupant != null)
            return multiGridOccupant.GetAdjacentOuterCells();

        Vector2Int anchorGrid = GetAnchorGrid(gridManager);
        List<Vector2Int> adjacentCells = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            adjacentCells.Add(anchorGrid + GridManager.Directions8[i]);

        return adjacentCells;
    }

    public string GetOutpostTypeDisplayName()
    {
        return outpostType switch
        {
            OutpostType.Bank => "Bank",
            OutpostType.Composite => "Composite",
            _ => outpostType.ToString()
        };
    }

    public string GetProductionDisplayText()
    {
        return outpostType switch
        {
            OutpostType.Bank => $"Money +{resourcePerTurn} / turn",
            OutpostType.Composite => $"Chip +{resourcePerTurn}, Crystal +{resourcePerTurn}, Supply +{resourcePerTurn} / turn",
            _ => string.Empty
        };
    }
}
