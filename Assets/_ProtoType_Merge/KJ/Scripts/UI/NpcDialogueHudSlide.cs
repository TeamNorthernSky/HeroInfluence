using System.Collections.Generic;
using UnityEngine;

/// <summary>Slides exploration HUD away during NPC event zoom and restores its original placement.</summary>
public sealed class NpcDialogueHudSlide : MonoBehaviour
{
    private sealed class Item
    {
        public RectTransform rect;
        public Vector2 home, offset;
        public CanvasGroup group;
        public bool blocksRaycasts;
    }

    private readonly List<Item> items = new List<Item>();
    private QuarterViewCameraFollower cameraFollower;
    private RectTransform canvasRect;
    private float amount;
    private bool hidden;
    private const float Duration = 0.35f;

    private void OnEnable() { BindCamera(); }

    private void BindCamera()
    {
        if (cameraFollower != null) return;
        cameraFollower = FindFirstObjectByType<QuarterViewCameraFollower>();
        if (cameraFollower == null) return;
        cameraFollower.EventZoomChanged += SetHidden;
        SetHidden(cameraFollower.IsEventZoomApplied);
    }

    private void SetHidden(bool value)
    {
        var request = DHWorldEventRuntimeManager.Instance != null
            ? DHWorldEventRuntimeManager.Instance.CurrentRequest : null;
        value = value && request != null && request.SourceType == DHWorldEventSourceType.Npc;
        if (value == hidden) return;
        if (value && amount == 0f) Capture();
        hidden = value;
    }

    private void Capture()
    {
        items.Clear();
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvasRect = canvas.rootCanvas.transform as RectTransform;
        foreach (var rect in canvasRect.GetComponentsInChildren<RectTransform>(true))
        {
            Vector2 direction;
            switch (rect.name.Replace("(Clone)", ""))
            {
                case "PartyPanel": case "MiniMap": case "Minimap": case "PNL_Explor_Minmap":
                    direction = Vector2.left; break;
                case "TurnPanel": direction = Vector2.right; break;
                case "MoveGauge": case "Move Gauge": direction = Vector2.down; break;
                default: continue;
            }
            var bounds = VisibleBounds(rect);
            float distance = direction == Vector2.left ? bounds.max.x - canvasRect.rect.xMin :
                direction == Vector2.right ? canvasRect.rect.xMax - bounds.min.x :
                bounds.max.y - canvasRect.rect.yMin;
            var worldOffset = canvasRect.TransformVector(direction * Mathf.Max(0f, distance + 40f));
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            items.Add(new Item { rect = rect, home = rect.anchoredPosition,
                offset = (Vector2)rect.parent.InverseTransformVector(worldOffset),
                group = group, blocksRaycasts = group.blocksRaycasts });
        }
    }

    private Bounds VisibleBounds(RectTransform rect)
    {
        var corners = new Vector3[4];
        var bounds = new Bounds();
        bool found = false;
        foreach (var graphic in rect.GetComponentsInChildren<UnityEngine.UI.Graphic>())
        {
            if (!graphic.isActiveAndEnabled) continue;
            graphic.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = canvasRect.InverseTransformPoint(corner);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        return found ? bounds : RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, rect);
    }

    private void LateUpdate()
    {
        BindCamera();
        if (hidden)
        {
            var request = DHWorldEventRuntimeManager.Instance != null
                ? DHWorldEventRuntimeManager.Instance.CurrentRequest : null;
            if (request == null || request.SourceType != DHWorldEventSourceType.Npc) hidden = false;
        }
        amount = Mathf.MoveTowards(amount, hidden ? 1f : 0f, Time.unscaledDeltaTime / Duration);
        float eased = Mathf.SmoothStep(0f, 1f, amount);
        foreach (var item in items)
        {
            if (item.rect == null) continue;
            item.rect.anchoredPosition = item.home + item.offset * eased;
            item.group.blocksRaycasts = amount == 0f && item.blocksRaycasts;
        }
        if (amount == 0f) items.Clear();
    }

    private void OnDisable()
    {
        if (cameraFollower != null) cameraFollower.EventZoomChanged -= SetHidden;
        cameraFollower = null;
        foreach (var item in items)
        {
            if (item.rect == null) continue;
            item.rect.anchoredPosition = item.home;
            item.group.blocksRaycasts = item.blocksRaycasts;
        }
        items.Clear();
        hidden = false;
        amount = 0f;
    }
}
