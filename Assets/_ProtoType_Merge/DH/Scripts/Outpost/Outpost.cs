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
    [SerializeField] private string zoneId;
    [SerializeField, Min(1)] private int resolvedEnemyLevel = 1;

    [Header("Defender Combat Chat")]
    [SerializeField, Min(0)] private int defenderCombatChatId;

    [Header("Event Completion")]
    // The event battle completion flag claims the outpost.
    [SerializeField] private string completionFlagName;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material unclaimedMaterial;
    [SerializeField] private Material enemyClaimedMaterial;
    [SerializeField] private Material claimedMaterial;
    private MultiGridOccupant multiGridOccupant;
    private OutpostRegistry outpostRegistry;

    public bool IsClaimableByPlayer => outpostState == OutpostState.Unclaimed || outpostState == OutpostState.EnemyClaimed;
    public bool CanClaimDirectlyByPlayer => outpostState == OutpostState.Unclaimed;
    public bool IsPlayerClaimed => outpostState == OutpostState.Claimed;
    public bool IsEnemyClaimed => outpostState == OutpostState.EnemyClaimed;
    public OutpostType OutpostType => outpostType;
    public string ZoneId => NormalizeZoneId(zoneId);
    public int ResolvedEnemyLevel => Mathf.Max(1, resolvedEnemyLevel);
    public int DefenderCombatChatId => Mathf.Max(0, defenderCombatChatId);
    public string CompletionFlagName => string.IsNullOrWhiteSpace(completionFlagName) ? string.Empty : completionFlagName.Trim();

    private void OnValidate()
    {
        outpostType = OutpostTypeUtility.Normalize(outpostType);
        defenderCombatChatId = Mathf.Max(0, defenderCombatChatId);
        RefreshResolvedEnemyLevel();
    }

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
        RefreshResolvedEnemyLevel();

        if (!Application.isPlaying)
            return;

        DHEventStateRepository eventStateRepository = DHEventStateRepository.EnsureInstance();
        eventStateRepository.FlagChanged -= HandleEventFlagChanged;
        eventStateRepository.FlagChanged += HandleEventFlagChanged;
        ApplyCompletionFlagIfAlreadySet(eventStateRepository);
    }

    private void OnDisable()
    {
        outpostRegistry?.Unregister(this);

        DHEventStateRepository eventStateRepository = DHEventStateRepository.Instance;
        if (eventStateRepository != null)
            eventStateRepository.FlagChanged -= HandleEventFlagChanged;
    }

    public void Claim()
    {
        if (IsClaimableByPlayer)
            ApplyPlayerClaimedState();
    }

    public void ForceClaimFromCombatResult()
    {
        ApplyPlayerClaimedState();
    }

    public void EnemyClaim()
    {
        if (IsEnemyClaimed)
            return;

        outpostState = OutpostState.EnemyClaimed;
        ApplyStateMaterial();
        SaveProgressState();
    }

    public void ProduceForTurn()
    {
        if (!IsPlayerClaimed)
            return;

        if (resourcePerTurn <= 0)
            return;

        switch (outpostType)
        {
            case OutpostType.Bank:
                Game.Economy?.Add(ResourceType.Money, resourcePerTurn);
                break;
            case OutpostType.Composite:
                Game.Economy?.Add(ResourceType.Chip, resourcePerTurn);
                Game.Economy?.Add(ResourceType.Crystal, resourcePerTurn);
                Game.Economy?.Add(ResourceType.Supply, resourcePerTurn);
                break;
            case OutpostType.Library:
                Game.Economy?.Add(ResourceType.Chip, resourcePerTurn);
                break;
            case OutpostType.JewelryShop:
                Game.Economy?.Add(ResourceType.Crystal, resourcePerTurn);
                break;
            case OutpostType.BlockStore:
                Game.Economy?.Add(ResourceType.Supply, resourcePerTurn);
                break;
        }
    }

    public void ApplyZoneIdFromLoader(string loaderZoneId)
    {
        if (!string.IsNullOrWhiteSpace(loaderZoneId))
            zoneId = NormalizeZoneId(loaderZoneId);
        RefreshResolvedEnemyLevel();

        if (Application.isPlaying)
            ApplyCompletionFlagIfAlreadySet(DHEventStateRepository.Instance);
    }
    public void ApplyInitialData(int nextResourcePerTurn, OutpostState nextState)
    {
        ApplyInitialData(outpostType, nextResourcePerTurn, nextState);
    }

    public void ApplyInitialData(OutpostType nextOutpostType, int nextResourcePerTurn, OutpostState nextState)
    {
        outpostType = OutpostTypeUtility.Normalize(nextOutpostType);
        resourcePerTurn = nextResourcePerTurn;
        outpostState = nextState;
        ApplyStateMaterial();
        ApplyCompletionFlagIfPlaying();
    }

    public void ApplyProgressData(OutpostState nextState)
    {
        outpostState = nextState;
        ApplyStateMaterial();
        ApplyCompletionFlagIfPlaying();
    }

    public void RefreshResolvedEnemyLevel()
    {
        resolvedEnemyLevel = ResolveZoneEnemyLevel(ZoneId);
    }

    public string GetProgressKey(GridManager gridManager)
    {
        return MapProgressKey.ForOutpost(GetAnchorGrid(gridManager));
    }

    private void ApplyPlayerClaimedState(bool notifyClaimed = true)
    {
        if (IsPlayerClaimed)
            return;

        outpostState = OutpostState.Claimed;
        ApplyStateMaterial();
        SaveProgressState();

        if (notifyClaimed)
            OutpostClaimed?.Invoke(this);
    }

    private void HandleEventFlagChanged(string flagName, bool value)
    {
        if (!value || IsPlayerClaimed)
            return;

        string completionFlag = ResolveCompletionFlagName();
        if (string.IsNullOrEmpty(completionFlag))
            return;

        if (!string.Equals(DHEventStateRepository.NormalizeFlagName(flagName), completionFlag, StringComparison.Ordinal))
            return;

        ForceClaimFromCombatResult();
    }

    private void ApplyCompletionFlagIfAlreadySet(DHEventStateRepository eventStateRepository)
    {
        if (eventStateRepository == null || IsPlayerClaimed)
            return;

        string completionFlag = ResolveCompletionFlagName();
        if (string.IsNullOrEmpty(completionFlag))
            return;

        if (eventStateRepository.GetFlag(completionFlag))
            ApplyPlayerClaimedState(false);
    }

    private void ApplyCompletionFlagIfPlaying()
    {
        if (!Application.isPlaying)
            return;

        ApplyCompletionFlagIfAlreadySet(DHEventStateRepository.Instance);
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

        Material[] materials = targetRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            targetRenderer.sharedMaterials = new[] { nextMaterial };
            return;
        }

        for (int i = 0; i < materials.Length; i++)
            materials[i] = nextMaterial;

        targetRenderer.sharedMaterials = materials;
    }

    private void SaveProgressState()
    {
        if (!Application.isPlaying)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string outpostKey = GetProgressKey(gridManager);
        repository.SetOutpostState(
            outpostKey,
            outpostState,
            string.Empty,
            string.Empty);
    }

    private static int ResolveZoneEnemyLevel(string targetZoneId)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.TryGetZoneEnemyLevel(targetZoneId, out int level)
            ? Mathf.Max(1, level)
            : 1;
    }
    private static string NormalizeZoneId(string value)
    {
        return MapProgressKey.NormalizeSegment(value);
    }

    private string ResolveCompletionFlagName()
    {
        return string.IsNullOrWhiteSpace(completionFlagName)
            ? string.Empty
            : DHEventStateRepository.NormalizeFlagName(completionFlagName);
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

    public bool IsInteractionCell(Vector2Int grid, GridManager gridManager)
    {
        if (multiGridOccupant != null)
            return multiGridOccupant.IsBottomOuterCell(grid);

        Vector2Int anchorGrid = GetAnchorGrid(gridManager);
        int dx = Mathf.Abs(grid.x - anchorGrid.x);
        int dy = Mathf.Abs(grid.y - anchorGrid.y);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    public IReadOnlyList<Vector2Int> GetAdjacentInteractionCells(GridManager gridManager)
    {
        if (multiGridOccupant != null)
            return multiGridOccupant.GetBottomOuterCells();

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
            OutpostType.Library => "Library",
            OutpostType.JewelryShop => "Jewelry Shop",
            OutpostType.BlockStore => "Block Store",
            _ => outpostType.ToString()
        };
    }

    public string GetProductionDisplayText()
    {
        return outpostType switch
        {
            OutpostType.Bank => $"Money +{resourcePerTurn} / turn",
            OutpostType.Composite => $"Chip +{resourcePerTurn}, Crystal +{resourcePerTurn}, Supply +{resourcePerTurn} / turn",
            OutpostType.Library => $"Chip +{resourcePerTurn} / turn",
            OutpostType.JewelryShop => $"Crystal +{resourcePerTurn} / turn",
            OutpostType.BlockStore => $"Supply +{resourcePerTurn} / turn",
            _ => string.Empty
        };
    }
}
