using System.Collections.Generic;
using UnityEngine;

/// <summary>턴 프레임과 강조 대상의 화면 영역을 피해서 정보창을 배치합니다.</summary>
public sealed class JcBattleTooltipPlacement
{
    private readonly List<Rect> obstacles = new List<Rect>();
    private readonly List<float> xs = new List<float>();
    private readonly List<float> ys = new List<float>();
    private readonly Vector3[] corners = new Vector3[4];
    private Canvas canvas;
    private RectTransform canvasRect;
    private TurnOrderUI[] turnOrders;
    private BattleCharactor cachedUnit;
    private Renderer[] renderers;
    private bool hasPlacement;
    private RectTransform previousPanel;
    private BattleCharactor previousUnit;
    private TurnSlotUI previousSlot;
    private Rect previousArea, previousPlacement;
    private Vector2 previousSize, previousPointer;

    public void Initialize(Canvas owner)
    {
        canvas = owner;
        canvasRect = owner != null ? owner.transform as RectTransform : null;
        turnOrders = owner != null ? owner.GetComponentsInChildren<TurnOrderUI>(true) : null;
        cachedUnit = null;
        renderers = null;
        hasPlacement = false;
    }

    public bool Place(RectTransform panel, BattleCharactor unit, TurnSlotUI slot, Camera worldCamera,
        Vector2 pointer, Vector2 pointerOffset, float edgePadding, float gap, float modelClearancePixels = 0, float badgeClearance = 0)
    {
        if (canvasRect == null || panel == null) return false;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointer, uiCamera, out var localPointer)) return false;
        Rect area = canvasRect.rect;
        area = Rect.MinMaxRect(area.xMin + edgePadding, area.yMin + edgePadding, area.xMax - edgePadding, area.yMax - edgePadding);
        Rect current = CanvasBounds(panel);
        // 위치와 무관한 방향 벡터로 크기를 계산하여 이동에 따른 반올림 오차의 반복을 방지합니다.
        Vector2 size = CanvasSize(panel);
        obstacles.Clear();
        var tutorialMessage = JC.Tutorial.JcTutorialBattleGuide.MessageObstacle;
        if (tutorialMessage != null && tutorialMessage.gameObject.scene == panel.gameObject.scene)
            obstacles.Add(Expand(CanvasBounds(tutorialMessage), gap));
        float modelGap = Mathf.Max(gap, modelClearancePixels / Mathf.Max(.01f, canvas.scaleFactor));
        gap = Mathf.Max(gap, badgeClearance);
        bool hasTurn = false;
        Rect turn = default;
        if (turnOrders == null || turnOrders.Length == 0) turnOrders = canvas.GetComponentsInChildren<TurnOrderUI>(true);
        foreach (var order in turnOrders)
        {
            if (order == null || !order.isActiveAndEnabled || !(order.transform is RectTransform rect)) continue;
            Rect bounds = CanvasBounds(rect);
            obstacles.Add(Expand(bounds, gap));
            if (!hasTurn || (slot != null && slot.transform.IsChildOf(order.transform))) { turn = bounds; hasTurn = true; }
        }

        Vector2 preferred;
        if (slot != null)
        {
            Rect badge = CanvasBounds((RectTransform)slot.transform);
            if (!hasTurn) { turn = badge; obstacles.Add(Expand(turn, gap)); hasTurn = true; }
            // 배지 호버는 커서 이동과 무관한 슬롯 중앙을 기준으로 합니다.
            preferred = new Vector2(turn.xMin - gap - size.x, badge.center.y + size.y * 0.5f);
        }
        else
        {
            preferred = localPointer + pointerOffset;
            // 툴팁이 커서를 덮어서 호버가 끊기는 현상도 방지합니다.
            obstacles.Add(new Rect(localPointer - Vector2.one * gap, Vector2.one * gap * 2f));
        }

        // 메시 직접 호버도 모델과 바닥 표시를 함께 강조하므로 양쪽 경로 모두 보호합니다.
        if (TryModelBounds(unit, worldCamera, uiCamera, out var model)) obstacles.Add(Expand(model, modelGap));

        // 같은 호버에서는 여전히 안전한 위치를 유지해 모델 경계의 미세 변화로 인한 왕복을 방지합니다.
        float pointerThreshold = 1f / Mathf.Max(.01f, canvas.scaleFactor);
        if (hasPlacement && previousPanel == panel && previousUnit == unit && previousSlot == slot
            && previousArea == area && previousSize == size
            && (slot != null || (localPointer - previousPointer).sqrMagnitude <= pointerThreshold * pointerThreshold)
            && IsClear(previousPlacement, obstacles))
        {
            panel.position += canvasRect.TransformVector(new Vector3(
                previousPlacement.xMin - current.xMin, previousPlacement.yMax - current.yMax, 0));
            return true;
        }

        Rect chosen;
        bool placed = false;
        if (slot != null && hasTurn)
        {
            var leftArea = Rect.MinMaxRect(area.xMin, area.yMin, Mathf.Min(area.xMax, turn.xMin - gap), area.yMax);
            placed = TryPlace(leftArea, size, preferred, obstacles, xs, ys, out chosen);
        }
        else chosen = default;
        if (!placed) placed = TryPlace(area, size, preferred, obstacles, xs, ys, out chosen);
        if (!placed) { hasPlacement = false; return false; } // 공간이 없으면 정보창만 숨깁니다.

        hasPlacement = true;
        previousPanel = panel; previousUnit = unit; previousSlot = slot;
        previousArea = area; previousSize = size; previousPointer = localPointer; previousPlacement = chosen;

        // 피봇/앵커/Canvas 배율과 무관하게 실제 화면상의 좌상단 차이로 이동합니다.
        var shift = new Vector3(chosen.xMin - current.xMin, chosen.yMax - current.yMax, 0);
        panel.position += canvasRect.TransformVector(shift);
        return true;
    }

    private Rect CanvasBounds(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            Vector2 point = canvasRect.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Vector2 CanvasSize(RectTransform rect)
    {
        Vector3 x = canvasRect.InverseTransformVector(rect.TransformVector(new Vector3(rect.rect.width, 0, 0)));
        Vector3 y = canvasRect.InverseTransformVector(rect.TransformVector(new Vector3(0, rect.rect.height, 0)));
        return new Vector2(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y));
    }

    private static bool IsClear(Rect candidate, IReadOnlyList<Rect> obstacles)
    {
        foreach (var obstacle in obstacles) if (candidate.Overlaps(obstacle)) return false;
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private bool TryModelBounds(BattleCharactor unit, Camera camera, Camera uiCamera, out Rect result)
    {
        result = default;
        if (unit == null || camera == null) return false;
        if (cachedUnit != unit || renderers == null)
        {
            cachedUnit = unit;
            renderers = unit.GetComponentsInChildren<Renderer>(true);
        }
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) ||
                renderer.GetComponentInParent<Canvas>() != null || renderer.GetComponentInParent<ParticleSystem>() != null ||
                (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
            Bounds bounds = renderer.bounds;
            int behind = 0;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var screen = camera.WorldToScreenPoint(corner);
                if (screen.z < camera.nearClipPlane) { behind++; continue; }
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out var point)) continue;
                min = Vector2.Min(min, point); max = Vector2.Max(max, point); found = true;
            }
            // 근평면을 가로지르는 모델의 투영은 보수적으로 보호합니다.
            if (behind > 0 && behind < 8) { result = canvasRect.rect; return true; }
        }
        var grid = ASB.Work.BattleGrid.BattleGridManager.Instance;
        if (grid != null && grid.gameObject.scene == unit.gameObject.scene && grid.TryGetInfoHoverBounds(unit, out var ground))
        {
            int behind = 0;
            for (int i = 0; i < 8; i++)
            {
                var corner = ground.center + Vector3.Scale(ground.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var screen = camera.WorldToScreenPoint(corner);
                if (screen.z < camera.nearClipPlane) { behind++; continue; }
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out var point)) continue;
                min = Vector2.Min(min, point); max = Vector2.Max(max, point); found = true;
            }
            if (behind > 0 && behind < 8) { result = canvasRect.rect; return true; }
        }
        if (!found) return false;
        result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static Rect Expand(Rect rect, float gap) => Rect.MinMaxRect(rect.xMin - gap, rect.yMin - gap, rect.xMax + gap, rect.yMax + gap);

    /// <summary>장애물의 경계와 화면 경계에서 겹치지 않는 가장 가까운 위치를 찾습니다.</summary>
    public static bool TryPlace(Rect area, Vector2 size, Vector2 preferredTopLeft, IReadOnlyList<Rect> obstacles,
        List<float> xs, List<float> ys, out Rect result)
    {
        result = default;
        if (!Finite(size.x) || !Finite(size.y) || !Finite(preferredTopLeft.x) || !Finite(preferredTopLeft.y)
            || !Finite(area.xMin) || !Finite(area.yMin) || !Finite(area.xMax) || !Finite(area.yMax)) return false;
        if (size.x <= 0 || size.y <= 0 || area.width < size.x || area.height < size.y) return false;
        // 여유 공간이 있는 후보를 우선하되, 정확히 맞는 좁은 공간도 사용할 수 있어야 합니다.
        float skin = Mathf.Max(.01f, Mathf.Max(area.width, area.height) * 0.000002f);
        return TryPlaceWithSkin(area, size, preferredTopLeft, obstacles, xs, ys, skin, out result)
            || TryPlaceWithSkin(area, size, preferredTopLeft, obstacles, xs, ys, 0, out result);
    }

    private static bool TryPlaceWithSkin(Rect area, Vector2 size, Vector2 preferredTopLeft, IReadOnlyList<Rect> obstacles,
        List<float> xs, List<float> ys, float skin, out Rect result)
    {
        result = default;
        xs.Clear(); ys.Clear();
        float preferredY = preferredTopLeft.y - size.y;
        // 경계 접촉을 float 오차로 겹침으로 판정하지 않도록 아주 작은 여유를 확보합니다.
        xs.Add(Mathf.Clamp(preferredTopLeft.x, area.xMin, area.xMax - size.x));
        ys.Add(Mathf.Clamp(preferredY, area.yMin, area.yMax - size.y));
        xs.Add(area.xMin); xs.Add(area.xMax - size.x);
        ys.Add(area.yMin); ys.Add(area.yMax - size.y);
        foreach (var obstacle in obstacles)
        {
            xs.Add(Mathf.Clamp(obstacle.xMin - size.x - skin, area.xMin, area.xMax - size.x));
            xs.Add(Mathf.Clamp(obstacle.xMax + skin, area.xMin, area.xMax - size.x));
            ys.Add(Mathf.Clamp(obstacle.yMin - size.y - skin, area.yMin, area.yMax - size.y));
            ys.Add(Mathf.Clamp(obstacle.yMax + skin, area.yMin, area.yMax - size.y));
        }
        float best = float.PositiveInfinity;
        foreach (float x in xs) foreach (float y in ys)
        {
            var candidate = new Rect(x, y, size.x, size.y);
            if (!IsClear(candidate, obstacles)) continue;
            float score = (new Vector2(x, y) - new Vector2(preferredTopLeft.x, preferredY)).sqrMagnitude;
            if (score >= best) continue;
            best = score; result = candidate;
        }
        return !float.IsPositiveInfinity(best);
    }
}
