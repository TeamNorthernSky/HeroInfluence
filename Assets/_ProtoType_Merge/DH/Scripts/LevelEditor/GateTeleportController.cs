using System.Collections.Generic;
using UnityEngine;

public class GateTeleportController : MonoBehaviour
{
    private readonly List<GateRuntimeController> gates = new List<GateRuntimeController>();
    private PartyGridMover observedParty;
    private bool teleporting;
    private int suppressUntilFrame;

    public static GateTeleportController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolvePartySubscription();
    }

    private void OnDisable()
    {
        UnsubscribeParty();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolvePartySubscription();
    }

    public static GateTeleportController EnsureSceneInstance()
    {
        if (Instance != null)
            return Instance;

        GateTeleportController existing = FindFirstObjectByType<GateTeleportController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (!Application.isPlaying)
            return null;

        GameObject created = new GameObject(nameof(GateTeleportController));
        Instance = created.AddComponent<GateTeleportController>();
        return Instance;
    }

    public static bool IsOpenGateTeleportCell(Vector2Int grid)
    {
        GateTeleportController controller = Instance;
        if (controller == null)
            return false;

        return controller.TryFindOpenGateAtCell(grid, out _, out _);
    }

    public void RegisterGate(GateRuntimeController gate)
    {
        if (gate == null || gates.Contains(gate))
            return;

        gates.Add(gate);
    }

    public void UnregisterGate(GateRuntimeController gate)
    {
        if (gate == null)
            return;

        gates.Remove(gate);
    }

    private void ResolvePartySubscription()
    {
        PartyRegistry partyRegistry = FindFirstObjectByType<PartyRegistry>();
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == observedParty)
            return;

        UnsubscribeParty();
        observedParty = party;
        if (observedParty != null)
            observedParty.MoveCompleted += HandlePartyMoveCompleted;
    }

    private void UnsubscribeParty()
    {
        if (observedParty != null)
            observedParty.MoveCompleted -= HandlePartyMoveCompleted;

        observedParty = null;
    }

    private void HandlePartyMoveCompleted()
    {
        if (teleporting || Time.frameCount <= suppressUntilFrame || observedParty == null)
            return;

        Vector2Int currentGrid = observedParty.GetCurrentGrid();
        if (!TryFindOpenGateAtCell(currentGrid, out GateRuntimeController sourceGate, out int sourceCellIndex))
            return;

        if (!TryFindDestinationGate(sourceGate, out GateRuntimeController destinationGate))
            return;

        if (!destinationGate.TryGetTeleportCellByIndex(sourceCellIndex, out Vector2Int destinationGrid))
            return;

        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        if (gridManager != null && !gridManager.CanOccupyCell(destinationGrid, observedParty.transform, true))
        {
            Debug.LogWarning(
                $"GateTeleportController could not teleport party to occupied gate cell '{destinationGrid}' for gate '{sourceGate.GateId}'.",
                this);
            return;
        }

        teleporting = true;
        suppressUntilFrame = Time.frameCount + 1;
        // Teleport owns the first reveal step; general party fog reveal is suppressed for this frame.
        ZoneEntryGuidanceController.SuppressGeneralFogRevealForTeleport();
        string destinationZoneId = ResolveDestinationZoneId(sourceGate, currentGrid, destinationGrid);
        observedParty.SnapToGridPosition(destinationGrid, notifyMoveCompleted: false);
        ZoneEntryGuidanceController.EnsureInstance()?.TryBeginAfterTeleport(destinationGrid, observedParty, destinationZoneId);
        teleporting = false;
    }

    private static string ResolveDestinationZoneId(
        GateRuntimeController sourceGate,
        Vector2Int sourceGrid,
        Vector2Int destinationGrid)
    {
        LevelZoneLayoutLoader layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        if (sourceGate != null &&
            TryResolveZoneId(layoutLoader, sourceGrid, out string sourceZoneId) &&
            sourceGate.TryGetOtherZoneId(sourceZoneId, out string otherZoneId))
        {
            return otherZoneId;
        }

        // If the source zone cannot be resolved, use the destination cell's loaded zone as a safety net.
        return TryResolveZoneId(layoutLoader, destinationGrid, out string fallbackZoneId)
            ? fallbackZoneId
            : string.Empty;
    }

    private bool TryFindOpenGateAtCell(Vector2Int grid, out GateRuntimeController gate, out int cellIndex)
    {
        gate = null;
        cellIndex = -1;

        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController candidate = gates[i];
            if (candidate == null || !candidate.IsOpen)
                continue;

            if (!candidate.TryGetTeleportCellIndex(grid, out int candidateIndex))
                continue;

            gate = candidate;
            cellIndex = candidateIndex;
            return true;
        }

        return false;
    }

    private bool TryFindDestinationGate(GateRuntimeController sourceGate, out GateRuntimeController destinationGate)
    {
        destinationGate = null;
        if (sourceGate == null || string.IsNullOrWhiteSpace(sourceGate.GateId))
            return false;

        int matchCount = 0;
        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController candidate = gates[i];
            if (candidate == null || candidate == sourceGate || !candidate.IsOpen)
                continue;

            if (!string.Equals(candidate.GateId, sourceGate.GateId, System.StringComparison.Ordinal))
                continue;

            destinationGate = candidate;
            matchCount++;
        }

        // Teleport pairs are intentionally strict: one open source and one open destination with the same GateId.
        if (matchCount == 1)
            return true;

        if (matchCount == 0)
        {
            Debug.LogWarning($"GateTeleportController could not find a destination gate for '{sourceGate.GateId}'.", this);
            return false;
        }

        Debug.LogWarning(
            $"GateTeleportController found multiple destination gates for '{sourceGate.GateId}'. GateId teleport requires exactly two gates.",
            this);
        destinationGate = null;
        return false;
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
}
