using UnityEngine;
using UnityEngine.EventSystems;

public class PanelResizeHandler : MonoBehaviour, IDragHandler
{
    private RectTransform target;
    private Vector2 minSize;
    private Vector2 maxSize;
    private System.Action<Vector2> onResized;

    public void SetTarget(RectTransform targetRect, Vector2 min, Vector2 max, System.Action<Vector2> onResizedCallback = null)
    {
        target = targetRect;
        minSize = min;
        maxSize = max;
        onResized = onResizedCallback;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null) return;

        Vector2 delta = eventData.delta;

        // Canvas의 스케일 보정
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            delta /= canvas.scaleFactor;
        }

        Vector2 newSize = target.sizeDelta;
        newSize.x += delta.x;
        newSize.y -= delta.y; // 우하단 드래그: 아래로 드래그 시 높이 증가

        newSize.x = Mathf.Clamp(newSize.x, minSize.x, maxSize.x);
        newSize.y = Mathf.Clamp(newSize.y, minSize.y, maxSize.y);

        target.sizeDelta = newSize;
        onResized?.Invoke(newSize);
    }
}
