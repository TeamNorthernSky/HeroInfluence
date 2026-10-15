using UnityEngine;

/// <summary>의무실 전체회복 알림의 표시 시간과 페이드를 담당한다.</summary>
[DisallowMultipleComponent]
public sealed class InfirmaryNoticeView : MonoBehaviour
{
    [Min(0), Tooltip("알림을 선명하게 표시하는 시간(초)")]
    public float visibleSeconds = 2f;
    [Min(0), Tooltip("알림이 서서히 사라지는 시간(초)")]
    public float fadeSeconds = 0.5f;

    private GameObject notice;
    private CanvasGroup group;
    private float shownAt;
    private bool showing;

    public void Configure(GameObject target)
    {
        notice = target;
        if (notice == null) return;
        group = notice.GetComponent<CanvasGroup>();
        if (group == null) group = notice.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        Hide();
    }

    public void Show()
    {
        if (notice == null || group == null) return;
        shownAt = Time.unscaledTime;
        showing = true;
        group.alpha = 1;
        notice.SetActive(true);
    }

    private void Update()
    {
        if (!showing || notice == null) return;
        float fading = Time.unscaledTime - shownAt - Mathf.Max(0, visibleSeconds);
        if (fading < 0) return;
        float duration = Mathf.Max(0, fadeSeconds);
        if (duration == 0 || fading >= duration) { Hide(); return; }
        group.alpha = 1 - fading / duration;
    }

    public void Hide()
    {
        showing = false;
        if (group != null) group.alpha = 0;
        if (notice != null) notice.SetActive(false);
    }

    private void OnDisable() => Hide();
}
