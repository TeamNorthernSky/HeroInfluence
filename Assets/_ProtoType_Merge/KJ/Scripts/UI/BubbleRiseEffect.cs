using UnityEngine;

/// <summary>Standalone chat-bubble entrance. Does not change the chat system.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class BubbleRiseEffect : MonoBehaviour
{
    public enum EntranceStyle { SoftRise, ConversationPop, QuickLift, LeftPop, RightPop }

    [SerializeField] private EntranceStyle style;
    [SerializeField, Min(0.01f)] private float duration = 0.3f;
    [SerializeField, Min(0f)] private float riseDistance = 65f;
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 destination;
    private float elapsed;
    private bool running;

    public float Duration => duration;
    public bool IsRunning => running;

    public void Configure(EntranceStyle entranceStyle, float seconds, float distance)
    {
        style = entranceStyle;
        duration = Mathf.Max(0.01f, seconds);
        riseDistance = Mathf.Max(0f, distance);
    }

    private void Cache()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (group == null) group = GetComponent<CanvasGroup>();
    }

    public void Play(Vector2 target)
    {
        Cache();
        destination = target;
        elapsed = 0f;
        running = true;
        gameObject.SetActive(true);
        Evaluate(0f);
    }

    public void Hide()
    {
        Cache();
        running = false;
        rect.localScale = Vector3.one;
        group.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void SetDestination(Vector2 target)
    {
        Cache();
        destination = target;
        Evaluate(running ? elapsed / duration : 1f);
    }

    private void Update()
    {
        if (!running) return;
        elapsed = Mathf.Min(elapsed + Time.unscaledDeltaTime, duration);
        Evaluate(elapsed / duration);
        if (elapsed >= duration) running = false;
    }

    private void Evaluate(float normalizedTime)
    {
        float t = Mathf.Clamp01(normalizedTime);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        bool sidePop = style == EntranceStyle.LeftPop || style == EntranceStyle.RightPop;
        Vector2 direction = sidePop
            ? (style == EntranceStyle.LeftPop ? Vector2.left : Vector2.right)
            : Vector2.down;
        rect.anchoredPosition = destination + direction * (riseDistance * (1f - eased));
        float scale = 1f;
        if (style == EntranceStyle.ConversationPop || sidePop)
        {
            float u = t - 1f;
            float back = 1f + 2.1f * u * u * u + 1.1f * u * u;
            scale = Mathf.LerpUnclamped(sidePop ? 0.65f : 0.84f, 1f, back);
        }
        else if (style == EntranceStyle.QuickLift) scale = Mathf.Lerp(0.96f, 1f, eased);
        rect.localScale = Vector3.one * scale;
        group.alpha = Mathf.Clamp01(t / (style == EntranceStyle.SoftRise ? 0.7f : 0.35f));
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void OnDisable() => running = false;
}
