using UnityEngine;
using UnityEngine.UI;

/// <summary>그림 컷이 활성화될 때 원래 투명도까지 서서히 표시한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class CutSceneFadeIn : MonoBehaviour
{
    private Image image;
    private float originalAlpha;
    private float elapsed;
    private float duration = 0.35f;

    private void Awake()
    {
        image = GetComponent<Image>();
        originalAlpha = image.color.a;
    }

    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0f, seconds);
        if (image != null && isActiveAndEnabled)
            SetAlpha(duration > 0f ? originalAlpha * Mathf.Clamp01(elapsed / duration) : originalAlpha);
    }

    private void OnEnable()
    {
        elapsed = 0f;
        SetAlpha(duration > 0f ? 0f : originalAlpha);
        if (Application.isPlaying) AudioManager.Instance?.PlaySfx("Tutorial_TextBubble");
    }

    private void Update() => Advance(Time.unscaledDeltaTime);

    private void Advance(float deltaTime)
    {
        if (elapsed >= duration) return;
        elapsed = Mathf.Min(elapsed + deltaTime, duration);
        SetAlpha(originalAlpha * (elapsed / duration));
    }

    private void OnDisable() => SetAlpha(originalAlpha);

    private void SetAlpha(float alpha)
    {
        var color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
