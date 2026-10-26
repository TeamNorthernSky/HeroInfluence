using UnityEngine;

/// <summary>Expands modal content from the opening button; leaves the modal root and dim intact.</summary>
[DisallowMultipleComponent]
public sealed class LobbyModalButtonPop : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private CanvasGroup contentGroup;
    [Tooltip("창 등장 시간(초). 작을수록 빠르게 열립니다."), Min(0.01f)]
    public float duration = 0.28f;
    [SerializeField, Range(0.01f, 1f)] private float initialScale = 0.08f;
    [SerializeField] private bool allowCrossSceneOrigin;

    private Vector3 endPosition;
    private Vector3 endScale;
    private Vector3 startPosition;
    private Vector3 endContentPosition;
    private float endAlpha;
    private bool endInteractable;
    private float elapsed;
    private bool running;
    private bool hasSnapshot;
    private bool closing;

    public static void Close(GameObject modalRoot)
    {
        if (modalRoot == null) return;
        var effect = modalRoot.GetComponent<LobbyModalButtonPop>();
        if (effect != null && effect.isActiveAndEnabled && effect.hasSnapshot)
        {
            if (effect.closing) return;
            effect.closing = true;
            effect.running = true;
            effect.contentGroup.interactable = false;
            return;
        }
        modalRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (content == null || contentGroup == null ||
            !LobbyModalOriginButton.TryGetOrigin(gameObject.scene.handle, out var origin, allowCrossSceneOrigin)) return;
        var parent = content.parent as RectTransform;
        if (parent == null) return;
        var canvas = content.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, origin, camera, out var local)) return;

        Restore();
        endContentPosition = content.localPosition;
        endScale = content.localScale;
        endPosition = endContentPosition + CentreOffset(endScale);
        endAlpha = contentGroup.alpha;
        endInteractable = contentGroup.interactable;
        hasSnapshot = true;
        // Track the visual centre, so scaling a non-centred pivot cannot curve its path.
        startPosition = new Vector3(local.x, local.y, endPosition.z);
        elapsed = 0f;
        closing = false;
        running = true;
        contentGroup.interactable = false;
        Evaluate(0f);
    }

    private void Update()
    {
        if (!running) return;
        float seconds = Mathf.Max(0.01f, duration);
        elapsed = Mathf.Clamp(elapsed + (closing ? -1f : 1f) * Time.unscaledDeltaTime, 0f, seconds);
        Evaluate(elapsed / seconds);
        if (closing && elapsed <= 0f) gameObject.SetActive(false);
        else if (!closing && elapsed >= seconds) Restore(false);
    }

    private void Evaluate(float normalizedTime)
    {
        float t = Mathf.Clamp01(normalizedTime);
        // One monotonic progress for position AND scale: every corner follows a straight
        // path from the button-sized start to the final window, without an overshoot arc.
        float progress = 1f - Mathf.Pow(1f - t, 3f);
        content.localScale = endScale * Mathf.Lerp(initialScale, 1f, progress);
        content.localPosition = Vector3.Lerp(startPosition, endPosition, progress) - CentreOffset(content.localScale);
        contentGroup.alpha = endAlpha * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(t / 0.45f));
    }

    private Vector3 CentreOffset(Vector3 scale) =>
        content.localRotation * Vector3.Scale(content.rect.center, scale);

    private void Restore(bool clearSnapshot = true)
    {
        running = false;
        if (!hasSnapshot) return;
        if (content != null)
        {
            content.localPosition = endContentPosition;
            content.localScale = endScale;
        }
        if (contentGroup != null)
        {
            contentGroup.alpha = endAlpha;
            contentGroup.interactable = endInteractable;
        }
        if (clearSnapshot)
        {
            hasSnapshot = false;
            closing = false;
        }
    }

    private void OnDisable() => Restore();
}
