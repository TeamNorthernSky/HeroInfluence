using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PanelDragHandler : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    private RectTransform target;
    private Vector2 dragOffset;
    private System.Action<Vector2> onDragged;
    private bool blockOnInteractable;
    private bool canDrag;

    public void SetTarget(RectTransform targetRect, System.Action<Vector2> onDraggedCallback = null,
        bool blockOnInteractableElements = false)
    {
        target = targetRect;
        onDragged = onDraggedCallback;
        blockOnInteractable = blockOnInteractableElements;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canDrag = true;

        if (blockOnInteractable)
        {
            var hitObject = eventData.pointerCurrentRaycast.gameObject;
            if (hitObject != null && hitObject != gameObject)
            {
                if (hitObject.GetComponentInParent<Button>() != null ||
                    hitObject.GetComponentInParent<ScrollRect>() != null ||
                    hitObject.GetComponentInParent<Scrollbar>() != null ||
                    hitObject.GetComponent<PanelResizeHandler>() != null)
                {
                    canDrag = false;
                    return;
                }
            }
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 mousePos
        );
        dragOffset = (Vector2)target.localPosition - mousePos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null || !canDrag) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 mousePos
        );
        target.localPosition = mousePos + dragOffset;
        onDragged?.Invoke(target.anchoredPosition);
    }
}
