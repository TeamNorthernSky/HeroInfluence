using System.Collections.Generic;
using UnityEngine;

public class MoveCommandPreviewController
{
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

        if (marker != null)
        {
            ApplyMarkerColor(validMarkerColor);
            marker.gameObject.SetActive(false);
        }

        pathPreviewRenderer?.Hide();
    }

    public void PreviewMoveToGrid(PartyGridMover activeMover, Vector2Int clickedGrid)
    {
        if (activeMover == null || gridManager == null || pathfinder == null || marker == null)
            return;

        Vector2Int destinationGrid = ResolveDestinationGrid(activeMover, clickedGrid);
        Vector3 markerWorld = gridManager.GridToWorldCenter(destinationGrid);
        markerWorld.y = gridManager.GetLandSurfaceY();
        PlaceMarker(destinationGrid, markerWorld);

        Vector2Int partyGrid = activeMover.GetCurrentGrid();
        List<Vector2Int> path = pathfinder.FindPath(partyGrid, markerGrid, activeMover.transform);
        previewPath = path;

        List<Vector2Int> fullMoveCandidate = AdjustPathForSpecialDestination(ClonePath(path));
        movePath = TrimPathToMovePoints(fullMoveCandidate, activeMover.RemainingMovePoints);
        hasPreviewPath = previewPath != null && previewPath.Count > 1;
        keepMarkerTailWhileMoving = ShouldKeepDestinationTailWhileMoving();

        int selectedMoveCost = GetPathMoveCost(movePath);
        int fullMoveCost = GetPathMoveCost(fullMoveCandidate);

        canMoveToMarker = selectedMoveCost > 0;
        isDestinationFullyReachable = hasPreviewPath && activeMover.CanSpendMovePoints(fullMoveCost);
        hasOverLimitTail = hasPreviewPath && fullMoveCost > activeMover.RemainingMovePoints;

        ApplyMarkerColor(isDestinationFullyReachable ? validMarkerColor : invalidMarkerColor);

        pathPreviewRenderer?.RenderPath(
            previewPath,
            gridManager,
            GetDisplayReachableSegmentCount(previewPath, GetPathMoveCost(movePath)));
    }

    public bool TryConfirmMove(Ray ray, PartyGridMover activeMover)
    {
        if (activeMover == null)
            return false;

        if (!TryHandleMarkerClick(ray))
            return false;

        activeMover.MoveByGridPath(movePath);
        return true;
    }

    public void HandlePathUpdated(List<Vector2Int> remainingPath)
    {
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
        if (pathPreviewRenderer == null || activeMover == null || gridManager == null)
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
        if (marker == null || !marker.gameObject.activeInHierarchy || !hasMarkerGrid || !hasPreviewPath || !canMoveToMarker)
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

    private void PlaceMarker(Vector2Int grid, Vector3 worldPosition)
    {
        markerGrid = grid;
        hasMarkerGrid = true;

        marker.position = worldPosition;
        ApplyMarkerColor(validMarkerColor);
        if (!marker.gameObject.activeSelf)
            marker.gameObject.SetActive(true);
    }

    private Vector2Int ResolveDestinationGrid(PartyGridMover activeMover, Vector2Int clickedGrid)
    {
        if (activeMover == null || gridManager == null)
            return clickedGrid;

        if (gridManager.TryGetOutpostObjectAtGrid(clickedGrid, out Outpost outpost))
            return ResolveApproachGrid(activeMover, clickedGrid, outpost.GetAnchorGrid(gridManager), outpost.GetAdjacentInteractionCells(gridManager));

        if (!gridManager.TryGetCastleObjectAtGrid(clickedGrid, out CastleUnit castle))
            return clickedGrid;

        MultiGridOccupant occupant = castle.GetComponent<MultiGridOccupant>();
        if (occupant == null)
            return clickedGrid;

        return ResolveApproachGrid(activeMover, clickedGrid, castle.GetCurrentGrid(), occupant.GetAdjacentOuterCells());
    }

    private Vector2Int ResolveApproachGrid(
        PartyGridMover activeMover,
        Vector2Int fallbackGrid,
        Vector2Int targetGrid,
        IReadOnlyList<Vector2Int> approachCandidates)
    {
        Vector2Int moverGrid = activeMover.GetCurrentGrid();
        for (int i = 0; i < approachCandidates.Count; i++)
        {
            if (approachCandidates[i] == moverGrid)
                return moverGrid;
        }

        Vector2Int bestGrid = fallbackGrid;
        List<Vector2Int> bestPath = null;

        for (int i = 0; i < approachCandidates.Count; i++)
        {
            Vector2Int candidate = approachCandidates[i];
            List<Vector2Int> candidatePath = pathfinder.FindPath(moverGrid, candidate, activeMover.transform);
            if (candidatePath == null || candidatePath.Count == 0)
                continue;

            if (bestPath == null || candidatePath.Count < bestPath.Count)
            {
                bestPath = candidatePath;
                bestGrid = candidate;
                continue;
            }

            if (bestPath != null
                && candidatePath.Count == bestPath.Count
                && IsBetterCastleApproach(moverGrid, targetGrid, candidate, bestGrid))
            {
                bestPath = candidatePath;
                bestGrid = candidate;
            }
        }

        return bestPath != null ? bestGrid : fallbackGrid;
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

    private static bool IsBetterCastleApproach(
        Vector2Int moverGrid,
        Vector2Int castleGrid,
        Vector2Int candidateApproachGrid,
        Vector2Int currentBestApproachGrid)
    {
        int candidateAlignment = GetApproachAlignmentScore(moverGrid, castleGrid, candidateApproachGrid);
        int currentAlignment = GetApproachAlignmentScore(moverGrid, castleGrid, currentBestApproachGrid);
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
}
