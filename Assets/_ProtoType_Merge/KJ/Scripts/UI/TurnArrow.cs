using UnityEngine;
using UnityEngine.UI;

public class TurnArrow : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Graphic arrowGraphic;

    public Vector3 ScreenPosition { get; private set; }

    private void Awake()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (arrowGraphic == null)
        {
            arrowGraphic = GetComponent<Graphic>();
        }

        SetEnabled(false);
    }

    public void SetScreenPosition(Vector3 screenPosition)
    {
        ScreenPosition = screenPosition;

        if (rectTransform == null)
        {
            return;
        }

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Canvas parentCanvas = rectTransform.GetComponentInParent<Canvas>();
        Camera canvasCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                ScreenPosition,
                canvasCamera,
                out Vector2 localPosition))
        {
            rectTransform.anchoredPosition = localPosition;
        }
    }

    public void SetEnabled(bool isEnabled)
    {
        if (arrowGraphic != null)
        {
            arrowGraphic.enabled = isEnabled;
        }
    }
}
