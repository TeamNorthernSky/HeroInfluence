using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Noninteractive speech bubble following a character or the corresponding HUD profile.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class ExplorationActionPointBubble : MonoBehaviour
{
    private const float UnitHeight = 1.1f;

    [Header("말풍선 대사")]
    [TextArea(2, 5)] public string movementMessage = "지금은 그곳으로\n 갈 수 없어!";
    [TextArea(2, 5)] public string moneyMessage = "자금을 찾았어!\n알뜰하게 써야겠네.";
    [TextArea(2, 5)] public string chipMessage = "메달을 얻었어!\n협회에 도움이 되겠네.";
    [TextArea(2, 5)] public string crystalMessage = "보석을 발견했어!\n반짝반짝 빛나네.";
    [TextArea(2, 5)] public string supplyMessage = "보급품을 찾았어!\n탐사에 도움이 되겠네.";

    [SerializeField] private RectTransform followAnchor;
    [SerializeField] private BubbleRiseEffect entrance;
    [SerializeField] private CanvasGroup bubbleGroup;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Sprite partyPanelSprite;
    [SerializeField, Min(0f)] private float holdSeconds = 1.6f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;
    [SerializeField, Tooltip("머리 위치 자동 추종. 끄면 직접 부착한 위치를 사용하고, 설정 크기를 기준으로 카메라 확대·축소만 보정합니다.")]
    private bool followCharacterPosition = true;

    private Transform character;
    private Camera worldCamera;
    private RectTransform canvasRect;
    private float elapsed;
    private RectTransform profileTarget;
    private readonly Vector3[] profileCorners = new Vector3[4];
    private Vector3 attachedReferenceScale;
    private Vector3 attachedReferenceParentScale;
    private float attachedReferenceViewHeight;
    private float minimumBubbleHeight;

    public float TotalDuration => entrance != null ? entrance.Duration + holdSeconds + fadeSeconds : 0f;

    public string GetResourceMessage(ResourceType type)
    {
        string message;
        switch (type)
        {
            case ResourceType.Money: message = moneyMessage; break;
            case ResourceType.Chip: message = chipMessage; break;
            case ResourceType.Crystal: message = crystalMessage; break;
            case ResourceType.Supply: message = supplyMessage; break;
            default: message = null; break;
        }
        return string.IsNullOrWhiteSpace(message) ? "재화를 얻었어!" : message;
    }

    public void AttachToCharacter(bool followHead = true)
    {
        GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        followCharacterPosition = followHead;
    }

    public static bool TryShow(PartyGridMover party)
        => Application.isPlaying && ExplorationPartyBubbleManager.Instance != null
            && ExplorationPartyBubbleManager.Instance.ShowForParty(party);

    public void Hide()
    {
        if (entrance != null) entrance.Hide();
        gameObject.SetActive(false);
    }

    public void Present(Transform target, Camera camera, string message = null)
    {
        if (target == null || camera == null || followAnchor == null || entrance == null ||
            bubbleGroup == null || visibility == null || messageText == null) return;

        profileTarget = null;
        character = target;
        worldCamera = camera;
        canvasRect = (RectTransform)GetComponent<Canvas>().transform;
        elapsed = 0f;
        messageText.text = string.IsNullOrWhiteSpace(message) ? movementMessage : message;
        messageText.fontStyle = FontStyles.Bold;
        visibility.interactable = visibility.blocksRaycasts = false;
        gameObject.SetActive(true);
        FitBubbleToText();
        entrance.Play(Vector2.zero);
        FollowCharacter();
    }

    public void PresentBesideProfile(RectTransform profile, string message = null)
    {
        if (profile == null || followAnchor == null || entrance == null || bubbleGroup == null ||
            visibility == null || messageText == null) return;
        character = null;
        profileTarget = profile;
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasRect = (RectTransform)canvas.transform;
        canvasRect.localScale = Vector3.one;
        canvasRect.localRotation = Quaternion.identity;
        followAnchor.anchorMin = followAnchor.anchorMax = canvasRect.pivot;
        elapsed = 0f;
        messageText.text = string.IsNullOrWhiteSpace(message) ? movementMessage : message;
        visibility.alpha = 1f;
        visibility.interactable = visibility.blocksRaycasts = false;
        var background = entrance.GetComponent<Image>();
        if (background != null && partyPanelSprite != null)
        {
            background.sprite = partyPanelSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            var textRect = messageText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(36f, 12f);
            textRect.offsetMax = new Vector2(-16f, -12f);
        }
        ((RectTransform)entrance.transform).pivot = new Vector2(0f, .5f);
        entrance.Configure(BubbleRiseEffect.EntranceStyle.LeftPop, .36f, 110f);
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        FitBubbleToText();
        entrance.Play(Vector2.zero);
        FollowProfile();
    }

    private void LateUpdate()
    {
        bool followingProfile = profileTarget != null;
        if (followingProfile ? !profileTarget.gameObject.activeInHierarchy
            : character == null || worldCamera == null || !character.gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }
        elapsed += Time.unscaledDeltaTime;
        float fadeStart = entrance.Duration + holdSeconds;
        if (elapsed >= fadeStart + fadeSeconds)
        {
            entrance.Hide();
            gameObject.SetActive(false);
            return;
        }
        if (elapsed > fadeStart)
            bubbleGroup.alpha = 1f - Mathf.Clamp01((elapsed - fadeStart) / fadeSeconds);
        FitBubbleToText();
        if (followingProfile) FollowProfile();
        else FollowCharacter();
    }

    private void FitBubbleToText()
    {
        var bubbleRect = (RectTransform)entrance.transform;
        if (minimumBubbleHeight <= 0f) minimumBubbleHeight = bubbleRect.rect.height;
        var textRect = messageText.rectTransform;
        var margin = messageText.margin;
        margin.y = Mathf.Max(margin.y, 12f);
        margin.w = Mathf.Max(margin.w, 12f);
        if (messageText.margin != margin) messageText.margin = margin;
        float width = Mathf.Max(1f, textRect.rect.width);
        float verticalPadding = textRect.offsetMin.y - textRect.offsetMax.y;
        float preferredHeight = messageText.GetPreferredValues(messageText.text, width, Mathf.Infinity).y;
        float height = Mathf.Max(minimumBubbleHeight, Mathf.Ceil(preferredHeight + verticalPadding));
        if (!Mathf.Approximately(bubbleRect.rect.height, height))
            bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void FollowProfile()
    {
        profileTarget.GetWorldCorners(profileCorners);
        var sourceCanvas = profileTarget.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? sourceCanvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera,
            (profileCorners[2] + profileCorners[3]) * .5f);
        var targetCanvas = GetComponent<Canvas>();
        Camera targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, targetCamera, out var local)) return;
        var bubbleRect = (RectTransform)entrance.transform;
        local.x += 12f;
        local.x = Mathf.Clamp(local.x, canvasRect.rect.xMin + 8f, canvasRect.rect.xMax - bubbleRect.rect.width - 8f);
        local.y = Mathf.Clamp(local.y, canvasRect.rect.yMin + bubbleRect.rect.height * .5f + 8f,
            canvasRect.rect.yMax - bubbleRect.rect.height * .5f - 8f);
        followAnchor.anchoredPosition = local;
    }

    private void FollowCharacter()
    {
        if (GetComponent<Canvas>().renderMode == RenderMode.WorldSpace && !followCharacterPosition)
        {
            float depth = worldCamera.WorldToScreenPoint(canvasRect.position).z;
            visibility.alpha = depth > 0f ? 1f : 0f;
            canvasRect.rotation = worldCamera.transform.rotation;
            if (depth > .01f) UpdateAttachedScale(depth);
            return;
        }
        Vector3 world = character.position + Vector3.up * UnitHeight;
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        visibility.alpha = screen.z > 0f ? 1f : 0f;
        if (GetComponent<Canvas>().renderMode == RenderMode.WorldSpace)
        {
            canvasRect.position = world;
            canvasRect.rotation = worldCamera.transform.rotation;
            float height = worldCamera.orthographic ? worldCamera.orthographicSize * 2f
                : 2f * Mathf.Max(.01f, screen.z) * Mathf.Tan(worldCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            float scale = height / 1080f;
            Vector3 parentScale = canvasRect.parent != null ? canvasRect.parent.lossyScale : Vector3.one;
            canvasRect.localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
            followAnchor.anchoredPosition = Vector2.zero;
            return;
        }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local))
            followAnchor.anchoredPosition = local;
    }

    private void UpdateAttachedScale(float depth)
    {
        float viewHeight = worldCamera.orthographic ? worldCamera.orthographicSize * 2f
            : 2f * depth * Mathf.Tan(worldCamera.fieldOfView * .5f * Mathf.Deg2Rad);
        Vector3 parentScale = canvasRect.parent != null ? canvasRect.parent.lossyScale : Vector3.one;
        // Capture once, so reopening cannot turn the previous zoom correction into a new base size.
        if (attachedReferenceViewHeight <= 0f)
        {
            attachedReferenceViewHeight = Mathf.Max(.0001f, viewHeight);
            attachedReferenceScale = canvasRect.localScale;
            attachedReferenceParentScale = parentScale;
        }
        float ratio = viewHeight / attachedReferenceViewHeight;
        canvasRect.localScale = new Vector3(
            attachedReferenceScale.x * Mathf.Abs(attachedReferenceParentScale.x) / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            attachedReferenceScale.y * Mathf.Abs(attachedReferenceParentScale.y) / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)),
            attachedReferenceScale.z * Mathf.Abs(attachedReferenceParentScale.z) / Mathf.Max(.0001f, Mathf.Abs(parentScale.z))) * ratio;
    }

}
