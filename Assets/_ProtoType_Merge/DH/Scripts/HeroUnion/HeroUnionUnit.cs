using System;
using System.Collections.Generic;
using UnityEngine;

public enum HeroUnionState
{
    Neutral = 0,
    ClaimedByHero = 1
}

public class HeroUnionUnit : MonoBehaviour
{
    public static event Action<HeroUnionUnit> HeroUnionStateChanged;

    [Header("Data")]
    [SerializeField] private string heroUnionId = "heroUnion_001";
    [SerializeField] private string zoneId = "zone_001";
    [SerializeField] private HeroUnionState initialState = HeroUnionState.ClaimedByHero;
    [SerializeField] private HeroUnionState currentState = HeroUnionState.ClaimedByHero;

    [Header("Capture Chat")]
    [SerializeField, Min(1)] private int captureChatZoneId = 1;
    [SerializeField] private int captureChatId;
    [SerializeField] private bool autoPlayWhenInitiallyClaimed;

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material neutralMaterial;
    [SerializeField] private Material claimedMaterial;

    private HeroUnionRegistry heroUnionRegistry;
    private bool runtimeStateInitialized;

    public string HeroUnionId => heroUnionId;
    public string ZoneId => NormalizeZoneId(zoneId);
    public HeroUnionState InitialState => initialState;
    public HeroUnionState CurrentState => currentState;
    public bool IsClaimedByHero => currentState == HeroUnionState.ClaimedByHero;
    public int CaptureChatZoneId => Mathf.Max(1, captureChatZoneId);
    public int CaptureChatId => Mathf.Max(0, captureChatId);
    public bool AutoPlayWhenInitiallyClaimed => autoPlayWhenInitiallyClaimed;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            zoneId = "zone_001";

        captureChatZoneId = Mathf.Max(1, captureChatZoneId);
        captureChatId = Mathf.Max(0, captureChatId);

        if (Application.isPlaying)
        {
            ResolveRenderer();
            ApplyStateMaterial();
        }
    }

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        ResolveRenderer();
        currentState = initialState;
        ApplyStateMaterial();
    }

    private void Start()
    {
        if (!runtimeStateInitialized)
            ApplyProgressOrInitialState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        heroUnionRegistry?.Register(this);
    }

    private void OnDisable()
    {
        heroUnionRegistry?.Unregister(this);
    }

    public void ApplyZoneIdFromLoader(string loaderZoneId)
    {
        if (!string.IsNullOrWhiteSpace(loaderZoneId))
            zoneId = NormalizeZoneId(loaderZoneId);

        ApplyProgressOrInitialState();
    }

    public bool ClaimByHero()
    {
        if (IsClaimedByHero)
            return false;

        SetCurrentState(HeroUnionState.ClaimedByHero, true);
        return true;
    }

    public void ApplyProgressState(HeroUnionState state)
    {
        SetCurrentState(state, false);
    }

    public string GetProgressKey()
    {
        return HeroUnionProgressState.BuildProgressKey(ZoneId);
    }

    public string GetCaptureChatProgressKey()
    {
        string normalizedHeroUnionId = MapProgressKey.NormalizeSegment(heroUnionId);
        if (string.IsNullOrWhiteSpace(normalizedHeroUnionId))
            normalizedHeroUnionId = name;

        return MapProgressKey.NormalizeSegment($"hero_union_chat_{ZoneId}_{normalizedHeroUnionId}");
    }

    public Vector2Int GetCurrentGrid()
    {
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public IReadOnlyList<Vector2Int> GetInteractionCells()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.GetBottomOuterCells();

        Vector2Int origin = GetCurrentGrid();
        List<Vector2Int> adjacentCells = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            adjacentCells.Add(origin + GridManager.Directions8[i]);

        return adjacentCells;
    }

    public bool IsInteractionCell(Vector2Int grid)
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.IsBottomOuterCell(grid);

        Vector2Int origin = GetCurrentGrid();
        int dx = Mathf.Abs(grid.x - origin.x);
        int dy = Mathf.Abs(grid.y - origin.y);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    private void ApplyProgressOrInitialState()
    {
        HeroUnionState state = initialState;
        MapProgressRepository repository = Application.isPlaying ? MapProgressRepository.Instance : null;
        if (repository != null && repository.TryGetHeroUnionState(ZoneId, out HeroUnionState savedState))
            state = savedState;

        SetCurrentState(state, false);
        runtimeStateInitialized = true;
    }

    private void SetCurrentState(HeroUnionState nextState, bool saveProgress)
    {
        bool changed = currentState != nextState;
        currentState = nextState;
        ApplyStateMaterial();

        if (saveProgress)
            SaveProgressState();

        if (changed)
            HeroUnionStateChanged?.Invoke(this);
    }

    private void SaveProgressState()
    {
        if (!Application.isPlaying)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.SetHeroUnionState(ZoneId, currentState);
    }

    private void ApplyStateMaterial()
    {
        if (targetRenderer == null)
            return;

        Material nextMaterial = currentState == HeroUnionState.ClaimedByHero
            ? claimedMaterial
            : neutralMaterial;
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

    private void ResolveRenderer()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();
    }

    private static string NormalizeZoneId(string value)
    {
        string normalized = MapProgressKey.NormalizeSegment(value);
        return string.IsNullOrWhiteSpace(normalized) ? "zone_001" : normalized;
    }
}
