using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ZoneEntryGuidanceController : MonoBehaviour
{
    private const string GameObjectName = "[ZoneEntryGuidanceController]";
    // Teleport reveal is delayed so the guided path becomes the first visible area in a new zone.
    private const int TeleportRevealSuppressFrameCount = 2;
    // Padding widens fog visibility only. It must not be treated as extra movement permission.
    private const int VisualRevealPadding = 1;

    // The allowed path is both the movement restriction and the persisted route for re-entry restore.
    private readonly HashSet<Vector2Int> allowedPathCells = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> pathBuffer = new List<Vector2Int>();

    private string activeZoneId = string.Empty;
    private string requiredHeroUnionId = string.Empty;
    private Coroutine sceneRefreshCoroutine;
    private Coroutine revealRefreshCoroutine;
    private static int suppressGeneralFogRevealUntilFrame = -1;

    public static ZoneEntryGuidanceController Instance { get; private set; }
    public static bool IsActive => IsGuidanceActiveForCurrentPartyZone();
    public static bool HasActiveProgressState
    {
        get
        {
            ZoneEntryGuidanceProgressState state = MapProgressRepository.Instance?.ZoneEntryGuidanceState;
            return state != null && state.Active;
        }
    }
    public static bool IsActiveOrStoredActive => IsGuidanceActiveForCurrentPartyZone() || HasStoredGuidanceForCurrentPartyZone();
    public static bool SuppressGeneralFogReveal =>
        IsGuidanceActiveForCurrentPartyZone() || Time.frameCount <= suppressGeneralFogRevealUntilFrame;

    public bool Active { get; private set; }
    public string ActiveZoneId => activeZoneId;
    public string RequiredHeroUnionId => requiredHeroUnionId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        EnsureInstance();
    }

    public static ZoneEntryGuidanceController EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        ZoneEntryGuidanceController existing = FindFirstObjectByType<ZoneEntryGuidanceController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (!Application.isPlaying)
            return null;

        GameObject host = new GameObject(GameObjectName);
        DontDestroyOnLoad(host);
        Instance = host.AddComponent<ZoneEntryGuidanceController>();
        return Instance;
    }

    public static ZoneEntryGuidanceController ApplyStoredGuidanceIfNeeded()
    {
        ZoneEntryGuidanceController controller = EnsureInstance();
        if (controller == null)
            return null;

        // Stored guidance only applies while the party is inside the guided zone.
        if (!HasStoredGuidanceForCurrentPartyZone())
        {
            controller.ClearRuntimeState();
            return controller;
        }

        if (!controller.Active)
            controller.RefreshFromProgress();

        return controller;
    }

    public static void SuppressGeneralFogRevealForTeleport()
    {
        suppressGeneralFogRevealUntilFrame = Mathf.Max(
            suppressGeneralFogRevealUntilFrame,
            Time.frameCount + TeleportRevealSuppressFrameCount);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        QueueRefreshFromProgress();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

        if (sceneRefreshCoroutine != null)
        {
            StopCoroutine(sceneRefreshCoroutine);
            sceneRefreshCoroutine = null;
        }

        if (revealRefreshCoroutine != null)
        {
            StopCoroutine(revealRefreshCoroutine);
            revealRefreshCoroutine = null;
        }
    }

    public static bool IsCellAllowed(Vector2Int grid)
    {
        if (Instance == null || !Instance.Active || !IsGuidanceActiveForCurrentPartyZone())
            return true;

        // Backtracking through an open gate remains allowed even while the new-zone path is restricted.
        return Instance.allowedPathCells.Contains(grid) ||
            GateTeleportController.IsOpenGateTeleportCell(grid);
    }

    public static bool IsRequiredHeroUnion(HeroUnionUnit heroUnion)
    {
        if (Instance == null || !Instance.Active || heroUnion == null)
            return false;

        return string.Equals(
            MapProgressKey.NormalizeSegment(heroUnion.HeroUnionId),
            Instance.requiredHeroUnionId,
            System.StringComparison.Ordinal);
    }

    public void TryBeginAfterTeleport(Vector2Int destinationGrid, PartyGridMover party)
    {
        TryBeginAfterTeleport(destinationGrid, party, string.Empty);
    }

    public void TryBeginAfterTeleport(Vector2Int destinationGrid, PartyGridMover party, string destinationZoneId)
    {
        if (party == null)
            return;

        LevelZoneLayoutLoader layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        string zoneId = MapProgressKey.NormalizeSegment(destinationZoneId);
        if (string.IsNullOrWhiteSpace(zoneId) && !TryResolveZoneId(layoutLoader, destinationGrid, out zoneId))
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null || repository.IsZoneEntryGuidanceCompleted(zoneId))
        {
            ClearRuntimeState();
            return;
        }

        // First entry into a zone forces the party toward that zone's unclaimed HeroUnion.
        HeroUnionUnit heroUnion = ResolveHeroUnion(zoneId);
        if (heroUnion == null)
        {
            Debug.LogWarning($"[ZoneEntryGuidance] Could not find HeroUnion for zone '{zoneId}'.", this);
            return;
        }

        if (heroUnion.IsClaimedByHero)
        {
            repository.CompleteZoneEntryGuidance(zoneId);
            ClearRuntimeState();
            return;
        }

        if (!TryBuildPathToHeroUnion(party, heroUnion, out List<Vector2Int> path))
        {
            Debug.LogWarning($"[ZoneEntryGuidance] Could not build a clear path to HeroUnion '{heroUnion.HeroUnionId}' in zone '{zoneId}'.", this);
            return;
        }

        BeginGuidance(zoneId, heroUnion, path, true);
    }

    public bool TryCompleteFromPartyGrid(Vector2Int partyGrid)
    {
        if (!Active)
            return false;

        HeroUnionUnit heroUnion = ResolveHeroUnion(activeZoneId);
        if (heroUnion == null)
            return false;

        if (!IsRequiredHeroUnion(heroUnion) || !heroUnion.IsInteractionCell(partyGrid))
            return false;

        if (!heroUnion.IsClaimedByHero)
            heroUnion.ClaimByHero();

        CompleteGuidance();
        return true;
    }

    public bool TryCompleteFromClaimedHeroUnion(HeroUnionUnit heroUnion)
    {
        if (!Active || heroUnion == null || !heroUnion.IsClaimedByHero)
            return false;

        if (!IsRequiredHeroUnion(heroUnion))
            return false;

        CompleteGuidance();
        return true;
    }

    public void RefreshFromProgress()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        ZoneEntryGuidanceProgressState state = repository != null ? repository.ZoneEntryGuidanceState : null;
        if (state == null || !state.Active)
        {
            ClearRuntimeState();
            return;
        }

        if (!HasStoredGuidanceForCurrentPartyZone())
        {
            ClearRuntimeState();
            return;
        }

        Active = true;
        activeZoneId = state.ZoneId;
        requiredHeroUnionId = state.RequiredHeroUnionId;
        allowedPathCells.Clear();

        IReadOnlyList<Vector2Int> savedCells = state.AllowedPathCells;
        for (int i = 0; i < savedCells.Count; i++)
            allowedPathCells.Add(savedCells[i]);

        // Restore reuses the saved route exactly instead of recalculating around changed runtime objects.
        RevealAllowedPathCells();
    }

    public void RevealAllowedPathCells()
    {
        if (!Active || allowedPathCells.Count == 0)
            return;

        FogGridManager fogGridManager = FindFirstObjectByType<FogGridManager>();
        if (fogGridManager == null)
            return;

        pathBuffer.Clear();
        pathBuffer.AddRange(allowedPathCells);
        // Reveal can be broader than movement so the path and HeroUnion read clearly through fog.
        AppendVisualPaddingCells(pathBuffer);
        AppendRequiredHeroUnionRevealCells(pathBuffer);
        fogGridManager.RevealCells(pathBuffer);
    }

    private static void AppendVisualPaddingCells(List<Vector2Int> cells)
    {
        if (cells == null || VisualRevealPadding <= 0)
            return;

        int baseCount = cells.Count;
        for (int i = 0; i < baseCount; i++)
        {
            Vector2Int center = cells[i];
            for (int x = -VisualRevealPadding; x <= VisualRevealPadding; x++)
            {
                for (int y = -VisualRevealPadding; y <= VisualRevealPadding; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Vector2Int paddedCell = center + new Vector2Int(x, y);
                    if (!cells.Contains(paddedCell))
                        cells.Add(paddedCell);
                }
            }
        }
    }

    private void AppendRequiredHeroUnionRevealCells(List<Vector2Int> cells)
    {
        if (cells == null)
            return;

        HeroUnionUnit heroUnion = ResolveHeroUnion(activeZoneId);
        if (heroUnion == null || !IsRequiredHeroUnion(heroUnion))
            return;

        IReadOnlyList<Vector2Int> interactionCells = heroUnion.GetInteractionCells();
        for (int i = 0; i < interactionCells.Count; i++)
            cells.Add(interactionCells[i]);

        MultiGridOccupant occupant = heroUnion.GetComponent<MultiGridOccupant>();
        if (occupant != null)
        {
            IReadOnlyList<Vector2Int> occupiedCells = occupant.GetOccupiedCells();
            for (int i = 0; i < occupiedCells.Count; i++)
                cells.Add(occupiedCells[i]);

            return;
        }

        cells.Add(heroUnion.GetCurrentGrid());
    }

    private void BeginGuidance(string zoneId, HeroUnionUnit heroUnion, List<Vector2Int> path, bool saveProgress)
    {
        Active = true;
        activeZoneId = MapProgressKey.NormalizeSegment(zoneId);
        requiredHeroUnionId = MapProgressKey.NormalizeSegment(heroUnion.HeroUnionId);
        allowedPathCells.Clear();

        for (int i = 0; i < path.Count; i++)
            allowedPathCells.Add(path[i]);

        // Saving the exact path lets the party leave the zone and resume the same guidance later.
        if (saveProgress)
            MapProgressRepository.Instance?.BeginZoneEntryGuidance(activeZoneId, requiredHeroUnionId, path);

        RevealAllowedPathCells();
        QueueRevealAllowedPathCells();
    }

    private void QueueRevealAllowedPathCells()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        if (revealRefreshCoroutine != null)
            StopCoroutine(revealRefreshCoroutine);

        revealRefreshCoroutine = StartCoroutine(RevealAllowedPathCellsNextFrame());
    }

    private IEnumerator RevealAllowedPathCellsNextFrame()
    {
        yield return null;
        revealRefreshCoroutine = null;
        RevealAllowedPathCells();
    }

    private void CompleteGuidance()
    {
        string completedZoneId = activeZoneId;
        // Completion is permanent per zone; revisits return to normal movement and fog reveal.
        MapProgressRepository.Instance?.CompleteZoneEntryGuidance(completedZoneId);
        ClearRuntimeState();

        PartyFogRevealer partyFogRevealer = FindFirstObjectByType<PartyFogRevealer>();
        partyFogRevealer?.RevealAllCurrentPartyPositions();
    }

    private void ClearRuntimeState()
    {
        Active = false;
        activeZoneId = string.Empty;
        requiredHeroUnionId = string.Empty;
        allowedPathCells.Clear();
    }

    private bool TryBuildPathToHeroUnion(PartyGridMover party, HeroUnionUnit heroUnion, out List<Vector2Int> bestPath)
    {
        bestPath = null;

        AStarPathfinder pathfinder = FindFirstObjectByType<AStarPathfinder>();
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        if (pathfinder == null || gridManager == null || party == null || heroUnion == null)
            return false;

        Vector2Int start = party.GetCurrentGrid();
        IReadOnlyList<Vector2Int> interactionCells = heroUnion.GetInteractionCells();
        for (int i = 0; i < interactionCells.Count; i++)
        {
            Vector2Int candidate = interactionCells[i];
            if (candidate != start && !gridManager.CanOccupyCell(candidate, party.transform, true))
                continue;

            // Item cells stay valid so guided movement can still use the normal pickup flow.
            List<Vector2Int> candidatePath = pathfinder.FindPath(
                start,
                candidate,
                party.transform,
                true,
                EnemyEncounterPathMode.BlockEncounterZones,
                allowItemCells: true);
            if (candidatePath == null || candidatePath.Count == 0)
                continue;

            if (bestPath == null || candidatePath.Count < bestPath.Count)
                bestPath = candidatePath;
        }

        return bestPath != null && bestPath.Count > 0;
    }

    private static HeroUnionUnit ResolveHeroUnion(string zoneId)
    {
        HeroUnionRegistry registry = FindFirstObjectByType<HeroUnionRegistry>();
        if (registry != null && registry.TryGetByZoneId(zoneId, out HeroUnionUnit heroUnion))
            return heroUnion;

        return null;
    }

    private static bool TryResolveZoneId(LevelZoneLayoutLoader layoutLoader, Vector2Int grid, out string zoneId)
    {
        zoneId = string.Empty;
        if (layoutLoader == null || layoutLoader.LoadedZones == null)
            return false;

        IReadOnlyList<LoadedLevelZoneData> zones = layoutLoader.LoadedZones;
        for (int i = 0; i < zones.Count; i++)
        {
            LoadedLevelZoneData zone = zones[i];
            RectInt bounds = new RectInt(zone.Anchor, zone.Size);
            if (!bounds.Contains(grid))
                continue;

            zoneId = MapProgressKey.NormalizeSegment(zone.ZoneId);
            return !string.IsNullOrWhiteSpace(zoneId);
        }

        return false;
    }

    private static bool IsGuidanceActiveForCurrentPartyZone()
    {
        if (Instance == null || !Instance.Active)
            return false;

        return TryResolveCurrentPartyZoneId(out string partyZoneId) &&
            string.Equals(partyZoneId, Instance.activeZoneId, System.StringComparison.Ordinal);
    }

    private static bool HasStoredGuidanceForCurrentPartyZone()
    {
        ZoneEntryGuidanceProgressState state = MapProgressRepository.Instance?.ZoneEntryGuidanceState;
        if (state == null || !state.Active)
            return false;

        return TryResolveCurrentPartyZoneId(out string partyZoneId) &&
            string.Equals(partyZoneId, state.ZoneId, System.StringComparison.Ordinal);
    }

    private static bool TryResolveCurrentPartyZoneId(out string zoneId)
    {
        zoneId = string.Empty;

        PartyRegistry partyRegistry = FindFirstObjectByType<PartyRegistry>();
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null)
            return false;

        LevelZoneLayoutLoader layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        return TryResolveZoneId(layoutLoader, party.GetCurrentGrid(), out zoneId);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueRefreshFromProgress();
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        QueueRefreshFromProgress();
    }

    private void QueueRefreshFromProgress()
    {
        if (!isActiveAndEnabled)
            return;

        if (sceneRefreshCoroutine != null)
            StopCoroutine(sceneRefreshCoroutine);

        sceneRefreshCoroutine = StartCoroutine(RefreshFromProgressNextFrame());
    }

    private IEnumerator RefreshFromProgressNextFrame()
    {
        yield return null;
        sceneRefreshCoroutine = null;
        RefreshFromProgress();
    }
}
