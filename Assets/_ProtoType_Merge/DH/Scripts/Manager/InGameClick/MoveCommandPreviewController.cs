using System.Collections.Generic;
using UnityEngine;

public class MoveCommandPreviewController
{
    private const int PreviewPathMaxVisitedNodes = 2000;
    private const int MaxApproachPathCandidateChecks = 3;

    private readonly GridManager gridManager;
    private readonly AStarPathfinder pathfinder;
    private readonly Transform marker;
    private readonly PathPreviewRenderer pathPreviewRenderer;
    private readonly Color validMarkerColor;
    private readonly Color invalidMarkerColor;
    private readonly float rayDistance;

    private Vector2Int markerGrid;
    private bool hasMarkerGrid;
    private List<Vector2Int> previewPath;
    private List<Vector2Int> movePath;
    private bool hasPreviewPath;
    private bool keepMarkerTailWhileMoving;
    private bool canMoveToMarker;
    private bool isDestinationFullyReachable;
    private bool hasOverLimitTail;
    private Renderer[] markerRenderers;
    private MaterialPropertyBlock markerPropertyBlock;
    private EnemyGridMover previewEnemyTarget;
    private MainEventObject previewMainEventTarget;
    private WorldEventObject previewWorldEventTarget;

    public MoveCommandPreviewController(
        GridManager gridManager,
        AStarPathfinder pathfinder,
        Transform marker,
        PathPreviewRenderer pathPreviewRenderer,
        Color validMarkerColor,
        Color invalidMarkerColor,
        float rayDistance)
    {
        this.gridManager = gridManager;
        this.pathfinder = pathfinder;
        this.marker = marker;
        this.pathPreviewRenderer = pathPreviewRenderer;
        this.validMarkerColor = validMarkerColor;
        this.invalidMarkerColor = invalidMarkerColor;
        this.rayDistance = rayDistance;
    }

    public void Initialize()
    {
        if (marker != null)
        {
            markerRenderers = marker.GetComponentsInChildren<Renderer>(true);
            markerPropertyBlock = new MaterialPropertyBlock();
            ApplyMarkerColor(validMarkerColor);
            marker.gameObject.SetActive(false);
        }

        pathPreviewRenderer?.Hide();
    }

    public void ClearPreview()
    {
        previewPath = null;
        movePath = null;
        hasPreviewPath = false;
        hasMarkerGrid = false;
        keepMarkerTailWhileMoving = false;
        canMoveToMarker = false;
        isDestinationFullyReachable = false;
        hasOverLimitTail = false;
        previewEnemyTarget = null;
        previewMainEventTarget = null;
        previewWorldEventTarget = null;

        if (marker != null)
        {
            ApplyMarkerColor(validMarkerColor);
            marker.gameObject.SetActive(false);
        }

        pathPreviewRenderer?.Hide();
    }

    public void PreviewMoveToGrid(PartyGridMover activeMover, Vector2Int clickedGrid)
    {
        if (!CanPreviewForMover(activeMover) || gridManager == null || pathfinder == null || marker == null)
        {
            ClearPreview();
            return;
        }

        previewEnemyTarget = null;
        previewMainEventTarget = null;
        previewWorldEventTarget = null;

        if (!TryResolveDestinationGrid(activeMover, clickedGrid, out Vector2Int requestedDestinationGrid))
        {
            ClearPreview();
            return;
        }

        if (!TutorialMovementConstraint.IsTargetAllowed(requestedDestinationGrid, activeMover))
        {
            ClearPreview();
            return;
        }

        Vector2Int partyGrid = activeMover.GetCurrentGrid();
        List<Vector2Int> path = FindPlayerPreviewPath(
            activeMover,
            partyGrid,
            requestedDestinationGrid,
            out Vector2Int destinationGrid);

        if (path == null || path.Count == 0)
        {
            ClearPreview();
            return;
        }

        if (ZoneEntryGuidanceController.IsActive)
            path = TrimGuidancePathAtFirstItem(path, out destinationGrid);

        Vector3 markerWorld = gridManager.GridToWorldCenter(destinationGrid);
        markerWorld.y = gridManager.GetLandSurfaceY() + 0.02f;
        PlaceMarker(destinationGrid, markerWorld);
        previewPath = path;

        List<Vector2Int> fullMoveCandidate = AdjustPathForSpecialDestination(ClonePath(path));
        movePath = TrimPathToMovePoints(fullMoveCandidate, activeMover.RemainingMovePoints);
        hasPreviewPath = previewPath != null && previewPath.Count > 1;
        keepMarkerTailWhileMoving = ShouldKeepDestinationTailWhileMoving();

        int selectedMoveCost = GetPathMoveCost(movePath);
        int fullMoveCost = GetPathMoveCost(fullMoveCandidate);

        canMoveToMarker = selectedMoveCost > 0 || CanConfirmZeroStepInteraction(activeMover);
        isDestinationFullyReachable = hasPreviewPath && activeMover.CanSpendMovePoints(fullMoveCost);
        hasOverLimitTail = hasPreviewPath && fullMoveCost > activeMover.RemainingMovePoints;

        ApplyMarkerColor(isDestinationFullyReachable ? validMarkerColor : invalidMarkerColor);

        pathPreviewRenderer?.RenderPath(
            previewPath,
            gridManager,
            GetDisplayReachableSegmentCount(previewPath, GetPathMoveCost(movePath)));
    }

    // [JC 추가 260511] 정지 후 path 재계산. markerGrid 그대로 두고 현재 mover 위치 기준으로 path만 다시 그림.
    public void RecomputePathForCurrent(PartyGridMover activeMover)
    {
        if (!CanPreviewForMover(activeMover) || !hasMarkerGrid)
            return;

        PreviewMoveToGrid(activeMover, markerGrid);
    }

    public bool TryConfirmMove(Ray ray, PartyGridMover activeMover)
    {
        if (!CanPreviewForMover(activeMover))
            return false;

        if (!TryHandleMarkerClick(ray))
            return false;

        ConfirmMove(activeMover);
        return true;
    }

    public bool TryConfirmMoveAtGrid(Vector2Int clickedGrid, PartyGridMover activeMover)
    {
        if (!CanConfirmCurrentPreview(activeMover) || gridManager == null)
            return false;

        if (previewEnemyTarget != null
            && gridManager.TryGetEnemyObjectAtGrid(clickedGrid, out EnemyGridMover enemy)
            && enemy == previewEnemyTarget)
        {
            ConfirmMove(activeMover);
            return true;
        }

        if (previewMainEventTarget != null
            && gridManager.TryGetMainEventObjectAtGrid(clickedGrid, out MainEventObject mainEvent)
            && mainEvent == previewMainEventTarget)
        {
            ConfirmMove(activeMover);
            return true;
        }

        if (previewWorldEventTarget != null
            && gridManager.TryGetWorldEventObjectAtGrid(clickedGrid, out WorldEventObject worldEvent)
            && worldEvent == previewWorldEventTarget)
        {
            ConfirmMove(activeMover);
            return true;
        }

        return false;
    }

    private void ConfirmMove(PartyGridMover activeMover)
    {
        bool isItemOrEventTarget = hasMarkerGrid
            && gridManager != null
            && (gridManager.TryGetItemObjectAtGrid(markerGrid, out _)
                || gridManager.TryGetEventObjectAtGrid(markerGrid, out _)
                || gridManager.TryGetWorldEventObjectAtGrid(markerGrid, out _)
                || gridManager.TryGetSubEventObjectAtGrid(markerGrid, out _));

        Vector2Int? interactionTarget = null;
        if (hasMarkerGrid
            && gridManager != null
            && gridManager.HasInteractionTarget(markerGrid)
            && (isItemOrEventTarget || !IsSingleEnemyEncounterZone(markerGrid)))
        {
            interactionTarget = markerGrid;
        }

        activeMover.MoveByGridPath(movePath, interactionTarget);
    }

    public void HandlePathUpdated(List<Vector2Int> remainingPath)
    {
        if (!hasPreviewPath)
            return;

        if (remainingPath == null || remainingPath.Count <= 1)
        {
            ClearPreview();
            return;
        }

        if (pathPreviewRenderer == null)
            return;

        List<Vector2Int> renderedPath = GetRenderedPathWithMarkerTail(remainingPath);
        pathPreviewRenderer.RenderPath(
            renderedPath,
            gridManager,
            GetDisplayReachableSegmentCount(
                renderedPath,
                GetPathMoveCost(remainingPath)));
    }

    public void HandleMoveCompleted()
    {
        ClearPreview();
    }

    public void UpdateRealtimePathPreview(PartyGridMover activeMover)
    {
        if (pathPreviewRenderer == null || !CanPreviewForMover(activeMover) || gridManager == null)
            return;

        List<Vector2Int> remainingPath = activeMover.GetRemainingPath();
        List<Vector2Int> renderedPath = GetRenderedPathWithMarkerTail(remainingPath);

        pathPreviewRenderer.RenderPathFromWorld(
            activeMover.transform.position,
            renderedPath,
            gridManager,
            GetDisplayReachableSegmentCount(
                renderedPath,
                remainingPath.Count,
                true));
    }

    private bool TryHandleMarkerClick(Ray ray)
    {
        if (!CanConfirmCurrentPreview(null) || marker == null || !marker.gameObject.activeInHierarchy)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance);
        float bestDist = float.PositiveInfinity;
        bool hitMarker = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == marker || hitTransform.IsChildOf(marker))
            {
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    hitMarker = true;
                }
            }
        }

        return hitMarker;
    }

    private bool CanConfirmCurrentPreview(PartyGridMover activeMover)
    {
        if (activeMover != null && !CanPreviewForMover(activeMover))
            return false;

        return hasMarkerGrid
            && hasPreviewPath
            && canMoveToMarker
            && movePath != null
            && movePath.Count > 0;
    }

    private bool CanConfirmZeroStepInteraction(PartyGridMover activeMover)
    {
        if (activeMover == null || gridManager == null || !hasMarkerGrid)
            return false;

        int selectedMoveCost = GetPathMoveCost(movePath);
        if (selectedMoveCost != 0)
            return false;

        Vector2Int currentGrid = activeMover.GetCurrentGrid();
        if (currentGrid == markerGrid)
            return false;

        if (GridManager.GridDistance(currentGrid, markerGrid) > 1)
            return false;

        return gridManager.TryGetItemObjectAtGrid(markerGrid, out _)
            || gridManager.TryGetEventObjectAtGrid(markerGrid, out _)
            || gridManager.TryGetWorldEventObjectAtGrid(markerGrid, out _)
            || gridManager.TryGetSubEventObjectAtGrid(markerGrid, out _);
    }

    private void PlaceMarker(Vector2Int grid, Vector3 worldPosition)
    {
        markerGrid = grid;
        hasMarkerGrid = true;

        marker.position = worldPosition;
        ApplyMarkerColor(validMarkerColor);
        if (!marker.gameObject.activeSelf)
            marker.gameObject.SetActive(true);
    }

    private bool TryResolveDestinationGrid(PartyGridMover activeMover, Vector2Int clickedGrid, out Vector2Int destinationGrid)
    {
        destinationGrid = clickedGrid;

        if (activeMover == null || gridManager == null)
            return true;

        if (gridManager.TryGetEnemyObjectAtGrid(clickedGrid, out EnemyGridMover enemy))
        {
            previewEnemyTarget = enemy;
            return TryResolveApproachGrid(
                activeMover,
                enemy.GetCurrentGrid(),
                GetEnemyEncounterCandidates(enemy),
                out destinationGrid);
        }

        if (gridManager.TryGetOutpostObjectAtGrid(clickedGrid, out Outpost outpost))
            return TryResolveApproachGrid(
                activeMover,
                outpost.GetAnchorGrid(gridManager),
                outpost.GetAdjacentInteractionCells(gridManager),
                out destinationGrid);

        if (gridManager.TryGetVillainUnionBaseAtGrid(clickedGrid, out VillainUnionBase villainUnionBase))
            return TryResolveApproachGrid(
                activeMover,
                villainUnionBase.GetAnchorGrid(),
                villainUnionBase.GetInteractionCells(),
                out destinationGrid);

        if (gridManager.TryGetMainEventObjectAtGrid(clickedGrid, out MainEventObject mainEvent))
        {
            previewMainEventTarget = mainEvent;
            return TryResolveApproachGrid(
                activeMover,
                mainEvent.GetCurrentGrid(gridManager),
                mainEvent.GetInteractionCells(gridManager),
                out destinationGrid);
        }

        if (gridManager.TryGetWorldEventObjectAtGrid(clickedGrid, out WorldEventObject worldEvent))
        {
            previewWorldEventTarget = worldEvent;
            destinationGrid = worldEvent.GetCurrentGrid(gridManager);
            return true;
        }

        if (!gridManager.TryGetHeroUnionObjectAtGrid(clickedGrid, out HeroUnionUnit heroUnion))
            return true;

        MultiGridOccupant occupant = heroUnion.GetComponent<MultiGridOccupant>();
        if (occupant == null)
            return true;

        return TryResolveApproachGrid(
            activeMover,
            occupant.AnchorGrid,
            heroUnion.GetInteractionCells(),
            out destinationGrid);
    }

    private List<Vector2Int> GetEnemyEncounterCandidates(EnemyGridMover enemy)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        if (enemy == null || gridManager == null)
            return candidates;

        Vector2Int enemyGrid = enemy.GetCurrentGrid();
        for (int i = 0; i < GridManager.Directions8.Length; i++)
        {
            Vector2Int candidate = enemyGrid + GridManager.Directions8[i];
            if (gridManager.GetEnemyEncounterZoneState(candidate, out EnemyGridMover owner) != EnemyEncounterZoneState.SingleEnemyZone)
                continue;

            if (owner != enemy)
                continue;

            candidates.Add(candidate);
        }

        return candidates;
    }

    private List<Vector2Int> FindPlayerPreviewPath(
        PartyGridMover activeMover,
        Vector2Int partyGrid,
        Vector2Int requestedDestinationGrid,
        out Vector2Int destinationGrid)
    {
        destinationGrid = requestedDestinationGrid;

        List<Vector2Int> strictPath = pathfinder.FindPath(
            partyGrid,
            requestedDestinationGrid,
            activeMover.transform,
            false,
            EnemyEncounterPathMode.BlockEncounterZones,
            false,
            MainEventInteractionPathMode.BlockInteractionCells,
            PreviewPathMaxVisitedNodes);
        if (strictPath != null && strictPath.Count > 0)
            return strictPath;

        List<Vector2Int> fallbackPath = pathfinder.FindPath(
            partyGrid,
            requestedDestinationGrid,
            activeMover.transform,
            false,
            EnemyEncounterPathMode.AllowSingleEncounterZonePassage,
            false,
            MainEventInteractionPathMode.AllowSingleInteractionCellPassage,
            PreviewPathMaxVisitedNodes);
        if (fallbackPath == null || fallbackPath.Count == 0)
            return null;

        for (int i = 1; i < fallbackPath.Count; i++)
        {
            Vector2Int pathGrid = fallbackPath[i];
            if (gridManager.GetEnemyEncounterZoneState(pathGrid, out _) == EnemyEncounterZoneState.SingleEnemyZone ||
                gridManager.TryGetMainEventAtInteractionCell(pathGrid, out _))
            {
                destinationGrid = pathGrid;
                return fallbackPath.GetRange(0, i + 1);
            }
        }

        return null;
    }

    private bool TryResolveApproachGrid(
        PartyGridMover activeMover,
        Vector2Int targetGrid,
        IReadOnlyList<Vector2Int> approachCandidates,
        out Vector2Int destinationGrid)
    {
        Vector2Int moverGrid = activeMover.GetCurrentGrid();
        if (approachCandidates == null || approachCandidates.Count == 0)
        {
            destinationGrid = moverGrid;
            return false;
        }

        for (int i = 0; i < approachCandidates.Count; i++)
        {
            if (approachCandidates[i] == moverGrid)
            {
                destinationGrid = moverGrid;
                return true;
            }
        }

        Vector2Int bestGrid = moverGrid;
        List<Vector2Int> bestPath = null;
        List<Vector2Int> orderedCandidates = BuildOrderedApproachCandidates(
            approachCandidates,
            moverGrid,
            targetGrid);
        int candidateCheckCount = Mathf.Min(MaxApproachPathCandidateChecks, orderedCandidates.Count);

        for (int i = 0; i < candidateCheckCount; i++)
        {
            Vector2Int candidate = orderedCandidates[i];
            List<Vector2Int> candidatePath = pathfinder.FindPath(
                moverGrid,
                candidate,
                activeMover.transform,
                false,
                EnemyEncounterPathMode.BlockEncounterZones,
                false,
                MainEventInteractionPathMode.BlockInteractionCells,
                PreviewPathMaxVisitedNodes);
            if (candidatePath == null || candidatePath.Count == 0)
                continue;

            if (bestPath == null || candidatePath.Count < bestPath.Count)
            {
                bestPath = candidatePath;
                bestGrid = candidate;
                continue;
            }

            if (bestPath != null && candidatePath.Count == bestPath.Count)
            {
                int candidateDiagonalSteps = CountDiagonalSteps(candidatePath);
                int bestDiagonalSteps = CountDiagonalSteps(bestPath);
                if (candidateDiagonalSteps < bestDiagonalSteps)
                {
                    bestPath = candidatePath;
                    bestGrid = candidate;
                    continue;
                }

                if (candidateDiagonalSteps > bestDiagonalSteps)
                    continue;
            }

            if (bestPath != null
                && candidatePath.Count == bestPath.Count
                && IsBetterHeroUnionApproach(moverGrid, targetGrid, candidate, bestGrid))
            {
                bestPath = candidatePath;
                bestGrid = candidate;
            }
        }

        destinationGrid = bestGrid;
        return bestPath != null;
    }

    private static List<Vector2Int> BuildOrderedApproachCandidates(
        IReadOnlyList<Vector2Int> approachCandidates,
        Vector2Int moverGrid,
        Vector2Int targetGrid)
    {
        List<Vector2Int> orderedCandidates = new List<Vector2Int>();
        if (approachCandidates == null)
            return orderedCandidates;

        for (int i = 0; i < approachCandidates.Count; i++)
            orderedCandidates.Add(approachCandidates[i]);

        orderedCandidates.Sort((a, b) =>
        {
            int distanceCompare = GridManager.GridDistance(moverGrid, a)
                .CompareTo(GridManager.GridDistance(moverGrid, b));
            if (distanceCompare != 0)
                return distanceCompare;

            int alignmentCompare = GetApproachAlignmentScore(moverGrid, targetGrid, b)
                .CompareTo(GetApproachAlignmentScore(moverGrid, targetGrid, a));
            if (alignmentCompare != 0)
                return alignmentCompare;

            int targetDistanceCompare = GridManager.GridDistance(targetGrid, a)
                .CompareTo(GridManager.GridDistance(targetGrid, b));
            if (targetDistanceCompare != 0)
                return targetDistanceCompare;

            int xCompare = a.x.CompareTo(b.x);
            return xCompare != 0 ? xCompare : a.y.CompareTo(b.y);
        });

        return orderedCandidates;
    }

    private static int CountDiagonalSteps(List<Vector2Int> path)
    {
        if (path == null || path.Count <= 1)
            return 0;

        int count = 0;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int delta = path[i] - path[i - 1];
            if (delta.x != 0 && delta.y != 0)
                count++;
        }

        return count;
    }

    private List<Vector2Int> AdjustPathForSpecialDestination(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0 || gridManager == null || !hasMarkerGrid)
            return path;

        if (!gridManager.HasInteractionTarget(markerGrid))
            return path;

        if (path[path.Count - 1] != markerGrid)
            return path;

        if (path.Count <= 1)
            return path;

        path.RemoveAt(path.Count - 1);
        return path;
    }

    private List<Vector2Int> TrimGuidancePathAtFirstItem(List<Vector2Int> path, out Vector2Int destinationGrid)
    {
        destinationGrid = path != null && path.Count > 0 ? path[path.Count - 1] : Vector2Int.zero;
        if (path == null || path.Count <= 1 || gridManager == null)
            return path;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int grid = path[i];
            if (!gridManager.TryGetItemObjectAtGrid(grid, out _))
                continue;

            destinationGrid = grid;
            return path.GetRange(0, i + 1);
        }

        return path;
    }

    private List<Vector2Int> ClonePath(List<Vector2Int> path)
    {
        return path == null ? null : new List<Vector2Int>(path);
    }

    private bool ShouldKeepDestinationTailWhileMoving()
    {
        if (previewPath == null || movePath == null)
            return false;

        if (previewPath.Count <= movePath.Count)
            return false;

        return hasMarkerGrid && previewPath[previewPath.Count - 1] == markerGrid;
    }

    private List<Vector2Int> GetRenderedPathWithMarkerTail(List<Vector2Int> basePath)
    {
        if (basePath == null || basePath.Count == 0)
            return basePath;

        if (!keepMarkerTailWhileMoving || !hasMarkerGrid || previewPath == null || movePath == null)
            return basePath;

        List<Vector2Int> renderedPath = new List<Vector2Int>(basePath);
        int prefixCount = movePath.Count;
        for (int i = prefixCount; i < previewPath.Count; i++)
        {
            if (renderedPath.Count == 0 || renderedPath[renderedPath.Count - 1] != previewPath[i])
                renderedPath.Add(previewPath[i]);
        }

        return renderedPath;
    }

    private int GetDisplayReachableSegmentCount(List<Vector2Int> renderedPath, int reachableSegmentCount, bool startsFromWorld = false)
    {
        if (renderedPath == null)
            return 0;

        if (!hasOverLimitTail)
            return startsFromWorld ? renderedPath.Count : Mathf.Max(0, renderedPath.Count - 1);

        return reachableSegmentCount;
    }

    private static List<Vector2Int> TrimPathToMovePoints(List<Vector2Int> path, int movePoints)
    {
        if (path == null || path.Count == 0)
            return path;

        int clampedSteps = Mathf.Clamp(movePoints, 0, Mathf.Max(0, path.Count - 1));
        int allowedNodeCount = clampedSteps + 1;
        if (allowedNodeCount >= path.Count)
            return path;

        return path.GetRange(0, allowedNodeCount);
    }

    private static int GetPathMoveCost(List<Vector2Int> path)
    {
        return path == null ? 0 : Mathf.Max(0, path.Count - 1);
    }

    private bool IsSingleEnemyEncounterZone(Vector2Int grid)
    {
        return gridManager != null
            && gridManager.GetEnemyEncounterZoneState(grid, out _) == EnemyEncounterZoneState.SingleEnemyZone;
    }

    private static bool IsBetterHeroUnionApproach(
        Vector2Int moverGrid,
        Vector2Int heroUnionGrid,
        Vector2Int candidateApproachGrid,
        Vector2Int currentBestApproachGrid)
    {
        int candidateAlignment = GetApproachAlignmentScore(moverGrid, heroUnionGrid, candidateApproachGrid);
        int currentAlignment = GetApproachAlignmentScore(moverGrid, heroUnionGrid, currentBestApproachGrid);
        if (candidateAlignment != currentAlignment)
            return candidateAlignment > currentAlignment;

        int candidateDistance = GridManager.GridDistance(moverGrid, candidateApproachGrid);
        int currentDistance = GridManager.GridDistance(moverGrid, currentBestApproachGrid);
        return candidateDistance < currentDistance;
    }

    private static int GetApproachAlignmentScore(Vector2Int originGrid, Vector2Int targetGrid, Vector2Int approachGrid)
    {
        Vector2Int toTarget = targetGrid - originGrid;
        Vector2Int toApproach = approachGrid - originGrid;

        int dot = toTarget.x * toApproach.x + toTarget.y * toApproach.y;
        int axisMatch = 0;

        if (toTarget.x != 0 && toApproach.x != 0 && Mathf.Sign(toTarget.x) == Mathf.Sign(toApproach.x))
            axisMatch++;

        if (toTarget.y != 0 && toApproach.y != 0 && Mathf.Sign(toTarget.y) == Mathf.Sign(toApproach.y))
            axisMatch++;

        return dot * 10 + axisMatch;
    }

    private void ApplyMarkerColor(Color color)
    {
        if (markerRenderers == null || markerPropertyBlock == null)
            return;

        for (int i = 0; i < markerRenderers.Length; i++)
        {
            Renderer renderer = markerRenderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(markerPropertyBlock);
            markerPropertyBlock.SetColor("_Color", color);
            markerPropertyBlock.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(markerPropertyBlock);
        }
    }

    private static bool CanPreviewForMover(PartyGridMover mover)
    {
        return mover != null
            && mover.gameObject.activeInHierarchy
            && !DefeatedPartyReturnController.IsPartyWaiting(mover);
    }
}
