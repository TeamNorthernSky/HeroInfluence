using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Noninteractive speech bubble following a character or the corresponding HUD profile.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class ExplorationActionPointBubble : MonoBehaviour
{
    [System.Serializable]
    private sealed class CharacterBalloon
    {
        public int TemplateId;
        public Sprite AboveCharacter;
        public Sprite BesideProfile;
    }

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
    [SerializeField] private CharacterBalloon[] movementBalloons;
    [SerializeField, Min(0f)] private float holdSeconds = 1.6f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;
    [SerializeField, Tooltip("머리 위치 자동 추종. 끄면 직접 부착한 위치를 사용하고, 설정 크기를 기준으로 카메라 확대·축소만 보정합니다.")]
    private bool followCharacterPosition = true;

    private Transform character;
    private ExplorationUnitBubble characterBubble;
    private Camera worldCamera;
    private RectTransform canvasRect;
    private float elapsed;
    private RectTransform profileTarget;
    private readonly Vector3[] profileCorners = new Vector3[4];
    private Vector3 attachedReferenceScale;
    private Vector3 attachedReferenceParentScale;
    private float attachedReferenceViewHeight;
    private float minimumBubbleHeight;
    private float minimumBubbleWidth;
    private bool appearanceCached;
    private Sprite defaultWorldSprite;
    private Color defaultWorldColor;
    private float defaultPixelsPerUnitMultiplier;
    private Vector2 defaultBubbleSize;
    private Vector2 defaultTextOffsetMin;
    private Vector2 defaultTextOffsetMax;

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

    public void Present(Transform target, Camera camera, string message = null, int movementTemplateId = 0)
    {
        if (target == null || camera == null || followAnchor == null || entrance == null ||
            bubbleGroup == null || visibility == null || messageText == null) return;

        profileTarget = null;
        character = target;
        characterBubble = target.GetComponent<ExplorationUnitBubble>();
        worldCamera = camera;
        canvasRect = (RectTransform)GetComponent<Canvas>().transform;
        elapsed = 0f;
        messageText.text = string.IsNullOrWhiteSpace(message) ? movementMessage : message;
        messageText.fontStyle = FontStyles.Bold;
        visibility.interactable = visibility.blocksRaycasts = false;
        ApplyAppearance(movementTemplateId, false);
        gameObject.SetActive(true);
        FitBubbleToText();
        entrance.Play(Vector2.zero);
        FollowCharacter();
    }

    public void PresentBesideProfile(RectTransform profile, string message = null, int movementTemplateId = 0)
    {
        if (profile == null || followAnchor == null || entrance == null || bubbleGroup == null ||
            visibility == null || messageText == null) return;
        character = null;
        characterBubble = null;
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
        ApplyAppearance(movementTemplateId, true);
        ((RectTransform)entrance.transform).pivot = new Vector2(0f, .5f);
        entrance.Configure(BubbleRiseEffect.EntranceStyle.LeftPop, .36f, 110f);
        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        FitBubbleToText();
        entrance.Play(Vector2.zero);
        FollowProfile();
    }

    private void ApplyAppearance(int movementTemplateId, bool besideProfile)
    {
        var background = entrance.GetComponent<Image>();
        if (background == null) return;
        var bubbleRect = (RectTransform)entrance.transform;
        bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0f, .5f);
        bubbleRect.pivot = new Vector2(0f, .5f);
        var textRect = messageText.rectTransform;
        if (!appearanceCached)
        {
            appearanceCached = true;
            defaultWorldSprite = background.sprite;
            defaultWorldColor = background.color;
            defaultPixelsPerUnitMultiplier = background.pixelsPerUnitMultiplier;
            defaultBubbleSize = bubbleRect.sizeDelta;
            defaultTextOffsetMin = textRect.offsetMin;
            defaultTextOffsetMax = textRect.offsetMax;
        }

        background.sprite = besideProfile && partyPanelSprite != null ? partyPanelSprite : defaultWorldSprite;
        background.color = besideProfile ? Color.white : defaultWorldColor;
        background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = defaultPixelsPerUnitMultiplier;
        bubbleRect.sizeDelta = defaultBubbleSize;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = besideProfile ? new Vector2(36f, 12f) : defaultTextOffsetMin;
        textRect.offsetMax = besideProfile ? new Vector2(-16f, -12f) : defaultTextOffsetMax;
        minimumBubbleHeight = defaultBubbleSize.y;
        minimumBubbleWidth = defaultBubbleSize.x;

        if (movementTemplateId <= 0 || movementBalloons == null) return;
        foreach (var entry in movementBalloons)
        {
            if (entry == null || entry.TemplateId != movementTemplateId) continue;
            var sprite = besideProfile ? entry.BesideProfile : entry.AboveCharacter;
            if (sprite == null) return;
            background.sprite = sprite;
            background.color = Color.white;
            minimumBubbleHeight = besideProfile ? 100f : 150f;
            minimumBubbleWidth = besideProfile ? 340f : 420f;
            bubbleRect.sizeDelta = new Vector2(minimumBubbleWidth, minimumBubbleHeight);
            background.pixelsPerUnitMultiplier = sprite.rect.height / minimumBubbleHeight;
            textRect.offsetMin = besideProfile ? new Vector2(42f, 14f) : new Vector2(140f, 24f);
            textRect.offsetMax = besideProfile ? new Vector2(-20f, -14f) : new Vector2(-24f, -52f);
            return;
        }
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
        if (minimumBubbleWidth <= 0f) minimumBubbleWidth = bubbleRect.rect.width;
        var textRect = messageText.rectTransform;
        string singleLine = messageText.text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ')
            .Replace("<br>", " ").Replace("<br/>", " ").Replace("<br />", " ");
        if (messageText.text != singleLine) messageText.text = singleLine;
        messageText.enableWordWrapping = false;
        messageText.enableAutoSizing = false;
        var margin = messageText.margin;
        margin.y = Mathf.Max(margin.y, 12f);
        margin.w = Mathf.Max(margin.w, 12f);
        if (messageText.margin != margin) messageText.margin = margin;
        float horizontalPadding = textRect.offsetMin.x - textRect.offsetMax.x;
        float preferredWidth = messageText.GetPreferredValues(messageText.text, Mathf.Infinity, Mathf.Infinity).x;
        float width = Mathf.Max(minimumBubbleWidth, Mathf.Ceil(preferredWidth + horizontalPadding + 2f));
        if (!Mathf.Approximately(bubbleRect.rect.width, width))
            bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        if (!Mathf.Approximately(bubbleRect.rect.height, minimumBubbleHeight))
            bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minimumBubbleHeight);
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
        float minX = canvasRect.rect.xMin + 8f;
        float maxX = Mathf.Max(minX, canvasRect.rect.xMax - bubbleRect.rect.width - 8f);
        local.x = Mathf.Clamp(local.x, minX, maxX);
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
        Vector3 world = character.TransformPoint(characterBubble != null
            ? characterBubble.PositionOffset : Vector3.up * UnitHeight);
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
