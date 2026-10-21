using UnityEngine;

/// <summary>Screen-space dialogue entrance with a short overshoot and settle.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public sealed class DialogueBubblePopEffect : MonoBehaviour
{
    public enum EntranceMode { PopAndSettle, GrowAndHold }
    public EntranceMode mode;
    [Min(0.05f)] public float duration = 0.3f;
    [Range(0f, 0.25f)] public float overshoot = 0.08f;
    [Tooltip("확대가 시작되는 위치. (0,0)은 왼쪽 아래, (1,1)은 오른쪽 위입니다.")]
    public Vector2 growthOrigin = new Vector2(0.417f, 0f);
    private RectTransform rect;
    private CanvasGroup group;
    private Vector2 home;
    private Vector3 homeScale;
    private float elapsed;
    private bool running;
    public bool IsPlaying => running;

    private void Awake() => Cache();

    private void Cache()
    {
        if (rect != null) return;
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        home = rect.anchoredPosition;
        homeScale = rect.localScale;
    }

    private void OnEnable() => Play();

    public void Play()
    {
        Cache();
        elapsed = 0f;
        running = true;
        Evaluate(0f);
        GetComponent<DialogueTypewriterEffect>()?.Play();
    }

    private void Update()
    {
        if (!running) return;
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
        Evaluate(t);
        if (t >= 1f) running = false;
    }

    private void Evaluate(float t)
    {
        float scale;
        if (mode == EntranceMode.GrowAndHold)
            scale = Mathf.Lerp(0.08f, 1f + overshoot, 1f - Mathf.Pow(1f - t, 3f));
        else if (t < 0.6f)
        {
            float u = t / 0.6f;
            scale = Mathf.Lerp(0.08f, 1f + overshoot, 1f - Mathf.Pow(1f - u, 3f));
        }
        else if (t < 0.82f)
            scale = Mathf.Lerp(1f + overshoot, 0.98f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.22f));
        else
            scale = Mathf.Lerp(0.98f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.82f) / 0.18f));
        rect.localScale = Vector3.Scale(homeScale, new Vector3(scale, scale, 1f));
        var origin = rect.rect.min + Vector2.Scale(rect.rect.size, growthOrigin);
        var originOffset = rect.localRotation * Vector3.Scale(homeScale, new Vector3(origin.x, origin.y, 0f));
        rect.anchoredPosition = home + (Vector2)originOffset * (1f - scale);
        group.alpha = Mathf.Clamp01(t / 0.16f);
    }

    private void OnDisable()
    {
        if (rect == null) return;
        running = false;
        rect.anchoredPosition = home;
        rect.localScale = homeScale;
        group.alpha = 1f;
    }
}
