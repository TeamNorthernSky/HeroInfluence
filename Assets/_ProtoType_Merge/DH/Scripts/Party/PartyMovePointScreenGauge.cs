using UnityEngine;
using UnityEngine.UI;

public class PartyMovePointScreenGauge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PartyGridMover targetParty;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image[] segmentImages;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector2 screenOffset;
    [SerializeField] private bool hideWhenOffscreen = true;

    [Header("Display")]
    [SerializeField] private float activeAlpha = 1f;
    [SerializeField] private float inactiveAlpha = 0.15f;

    private RectTransform rectTransform;
    private int lastRemainingMovePoints = int.MinValue;
    private int lastSegmentCount = -1;

    public PartyGridMover TargetParty => targetParty;

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ResolveCameraIfNeeded();
        RefreshGauge(force: true);
    }

    private void LateUpdate()
    {
        ResolveCameraIfNeeded();
        UpdateScreenPosition();
        RefreshGauge(force: false);
    }

    public void Bind(PartyGridMover party, Camera camera)
    {
        targetParty = party;
        targetCamera = camera;
        ResolveCameraIfNeeded();
        UpdateScreenPosition();
        RefreshGauge(force: true);
    }

    [ContextMenu("Refresh Gauge")]
    public void RefreshGauge()
    {
        RefreshGauge(force: true);
    }

    private void UpdateScreenPosition()
    {
        if (targetParty == null ||
            targetCamera == null ||
            rectTransform == null ||
            !targetParty.gameObject.activeInHierarchy ||
            DefeatedPartyReturnController.IsPartyWaiting(targetParty))
        {
            SetVisible(false);
            return;
        }

        Vector3 worldPosition = targetParty.transform.position + worldOffset;
        Vector3 screenPosition = targetCamera.WorldToScreenPoint(worldPosition);
        bool isVisible = screenPosition.z > 0f;

        if (hideWhenOffscreen)
        {
            isVisible = isVisible &&
                screenPosition.x >= 0f &&
                screenPosition.x <= Screen.width &&
                screenPosition.y >= 0f &&
                screenPosition.y <= Screen.height;
        }

        SetVisible(isVisible);
        if (!isVisible)
            return;

        rectTransform.position = new Vector3(
            screenPosition.x + screenOffset.x,
            screenPosition.y + screenOffset.y,
            0f);
    }

    private void RefreshGauge(bool force)
    {
        if (targetParty == null || segmentImages == null)
            return;

        int segmentCount = segmentImages.Length;
        int remaining = Mathf.Clamp(targetParty.RemainingMovePoints, 0, segmentCount);

        if (!force && remaining == lastRemainingMovePoints && segmentCount == lastSegmentCount)
            return;

        lastRemainingMovePoints = remaining;
        lastSegmentCount = segmentCount;

        for (int i = 0; i < segmentImages.Length; i++)
        {
            Image segmentImage = segmentImages[i];
            if (segmentImage == null)
                continue;

            SetImageAlpha(segmentImage, i < remaining ? activeAlpha : inactiveAlpha);
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private void ResolveCameraIfNeeded()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }
}
