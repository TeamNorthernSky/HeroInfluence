using UnityEngine;
using UnityEngine.UI;

public class PartyMovePointWorldBillboardGauge : MonoBehaviour
{
    private enum BillboardMode
    {
        MatchCameraRotation,
        FaceCameraPosition
    }

    [Header("References")]
    [SerializeField] private PartyGridMover targetParty;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image[] segmentImages;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private bool hideWhenBehindCamera = true;

    [Header("Billboard")]
    [SerializeField] private BillboardMode billboardMode = BillboardMode.MatchCameraRotation;
    [SerializeField] private Vector3 rotationOffsetEuler;

    [Header("Scale")]
    [SerializeField] private float worldScale = 0.01f;

    [Header("Display")]
    [SerializeField] private float activeAlpha = 1f;
    [SerializeField] private float inactiveAlpha = 0.15f;

    private int lastRemainingMovePoints = int.MinValue;
    private int lastSegmentCount = -1;

    public PartyGridMover TargetParty => targetParty;

    private void Awake()
    {
        ResolveReferences();
        ApplyWorldCanvasMode();
        RefreshGauge(force: true);
    }

    private void LateUpdate()
    {
        ResolveCameraIfNeeded();
        UpdateWorldTransform();
        RefreshGauge(force: false);
    }

    public void Bind(PartyGridMover party, Camera camera)
    {
        targetParty = party;
        targetCamera = camera;
        ResolveReferences();
        ApplyWorldCanvasMode();
        UpdateWorldTransform();
        RefreshGauge(force: true);
    }

    [ContextMenu("Refresh Gauge")]
    public void RefreshGauge()
    {
        RefreshGauge(force: true);
    }

    private void UpdateWorldTransform()
    {
        if (targetParty == null ||
            targetCamera == null ||
            !targetParty.gameObject.activeInHierarchy ||
            DefeatedPartyReturnController.IsPartyWaiting(targetParty))
        {
            SetVisible(false);
            return;
        }

        Vector3 targetPosition = targetParty.transform.position + worldOffset;
        transform.position = targetPosition;
        transform.localScale = Vector3.one * Mathf.Max(0f, worldScale);

        if (hideWhenBehindCamera)
        {
            Vector3 viewportPosition = targetCamera.WorldToViewportPoint(targetPosition);
            if (viewportPosition.z <= 0f)
            {
                SetVisible(false);
                return;
            }
        }

        ApplyBillboardRotation(targetPosition);
        SetVisible(true);
    }

    private void ApplyBillboardRotation(Vector3 targetPosition)
    {
        Quaternion billboardRotation;

        if (billboardMode == BillboardMode.FaceCameraPosition)
        {
            Vector3 directionFromCamera = targetPosition - targetCamera.transform.position;
            if (directionFromCamera.sqrMagnitude <= 0.0001f)
                return;

            billboardRotation = Quaternion.LookRotation(directionFromCamera.normalized, targetCamera.transform.up);
        }
        else
        {
            billboardRotation = targetCamera.transform.rotation;
        }

        transform.rotation = billboardRotation * Quaternion.Euler(rotationOffsetEuler);
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

    private void ResolveReferences()
    {
        if (worldCanvas == null)
            worldCanvas = GetComponentInChildren<Canvas>();

        if (worldCanvas == null)
            worldCanvas = GetComponent<Canvas>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ResolveCameraIfNeeded();
    }

    private void ResolveCameraIfNeeded()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void ApplyWorldCanvasMode()
    {
        if (worldCanvas == null)
            return;

        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = targetCamera;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}
