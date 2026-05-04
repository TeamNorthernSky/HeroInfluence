using System.Collections.Generic;
using UnityEngine;

public class PathPreviewRenderer : MonoBehaviour
{
    [Header("Line Renderers")]
    [SerializeField] private LineRenderer reachableLineRenderer;
    [SerializeField] private LineRenderer unreachableLineRenderer;

    [Header("Style")]
    [SerializeField] private float width = 0.08f;
    [SerializeField] private Color reachableColor = Color.green;
    [SerializeField] private Color unreachableColor = Color.red;
    [SerializeField] private bool drawWhenPathValid = true;

    private const string ReachableChildName = "ReachablePath";
    private const string UnreachableChildName = "UnreachablePath";

    private void Awake()
    {
        ResolveLineRenderers();
        SetupLineRenderer(reachableLineRenderer, reachableColor);
        SetupLineRenderer(unreachableLineRenderer, unreachableColor);
        Hide();
    }

    public void RenderPath(List<Vector2Int> path, GridManager gridManager, int reachableSegments)
    {
        if (!drawWhenPathValid)
        {
            Hide();
            return;
        }

        if (path == null || path.Count < 2 || gridManager == null)
        {
            Hide();
            return;
        }

        float y = gridManager.GetLandSurfaceY() + 0.02f;
        List<Vector3> points = new List<Vector3>(path.Count);

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = gridManager.GridToWorldCenter(path[i]);
            p.y = y;
            points.Add(p);
        }

        RenderSplitPath(points, reachableSegments);
    }

    public void RenderPathFromWorld(Vector3 startWorldPosition, List<Vector2Int> remainingPath, GridManager gridManager, int reachableSegments)
    {
        if (!drawWhenPathValid)
        {
            Hide();
            return;
        }

        if (remainingPath == null || remainingPath.Count == 0 || gridManager == null)
        {
            Hide();
            return;
        }

        float y = gridManager.GetLandSurfaceY() + 0.02f;
        List<Vector3> points = new List<Vector3>(remainingPath.Count + 1);

        startWorldPosition.y = y;
        points.Add(startWorldPosition);

        for (int i = 0; i < remainingPath.Count; i++)
        {
            Vector3 p = gridManager.GridToWorldCenter(remainingPath[i]);
            p.y = y;
            points.Add(p);
        }

        RenderSplitPath(points, reachableSegments);
    }

    public void Hide()
    {
        if (reachableLineRenderer != null)
            reachableLineRenderer.enabled = false;

        if (unreachableLineRenderer != null)
            unreachableLineRenderer.enabled = false;
    }

    private void ResolveLineRenderers()
    {
        if (reachableLineRenderer == null)
            reachableLineRenderer = FindChildLineRenderer(ReachableChildName);

        if (unreachableLineRenderer == null)
            unreachableLineRenderer = FindChildLineRenderer(UnreachableChildName);
    }

    private LineRenderer FindChildLineRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<LineRenderer>() : null;
    }

    private void RenderSplitPath(List<Vector3> points, int reachableSegments)
    {
        if (points == null || points.Count < 2)
        {
            Hide();
            return;
        }

        int totalSegments = points.Count - 1;
        int clampedReachableSegments = Mathf.Clamp(reachableSegments, 0, totalSegments);
        int unreachableSegments = totalSegments - clampedReachableSegments;

        if (clampedReachableSegments > 0)
            SetLinePoints(reachableLineRenderer, points.GetRange(0, clampedReachableSegments + 1));
        else if (reachableLineRenderer != null)
            reachableLineRenderer.enabled = false;

        if (unreachableSegments > 0)
            SetLinePoints(unreachableLineRenderer, points.GetRange(clampedReachableSegments, unreachableSegments + 1));
        else if (unreachableLineRenderer != null)
            unreachableLineRenderer.enabled = false;
    }

    private void SetLinePoints(LineRenderer targetLineRenderer, List<Vector3> points)
    {
        if (targetLineRenderer == null)
            return;

        targetLineRenderer.positionCount = points.Count;
        targetLineRenderer.SetPositions(points.ToArray());
        targetLineRenderer.enabled = true;
    }

    private void SetupLineRenderer(LineRenderer targetLineRenderer, Color color)
    {
        if (targetLineRenderer == null)
            return;

        targetLineRenderer.useWorldSpace = true;
        targetLineRenderer.loop = false;
        targetLineRenderer.startWidth = width;
        targetLineRenderer.endWidth = width;
        targetLineRenderer.startColor = color;
        targetLineRenderer.endColor = color;

        if (targetLineRenderer.material == null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader != null)
                targetLineRenderer.material = new Material(shader);
        }

        if (targetLineRenderer.material != null)
            targetLineRenderer.material.color = color;
    }
}
