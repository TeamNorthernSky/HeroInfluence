using UnityEngine;
using TMPro;

/// <summary>EffectLab-only reward sequence. Never confirms events or changes resources.</summary>
public sealed class RewardFlightEffectLab : MonoBehaviour
{
    public RectTransform icon;
    public RectTransform startPoint;
    public RectTransform controlPoint;
    public RectTransform endPoint;
    public RectTransform zoomPreview;
    public GameObject dialogueBubble;
    public RectTransform[] curveSegments;
    public RectTransform[] additionalIcons;
    public TMP_Text resourceValue;
    public TMP_Text phaseLabel;
    public bool multipleIcons;
    [Range(0, 2)] public int motionMode;
    [Tooltip("뒤쪽 아이콘의 감속 강도입니다. 1이면 일정 속도이며, 높을수록 중간에 더 느려졌다가 출발 속도로 회복합니다.")]
    [Min(1f)] public float lastIconAcceleration = 2.5f;
    [Min(1f)] public float zoomStartScale = 1.15f;
    [Min(0f)] public float iconInterval = .07f;
    public int initialResource = 1000;
    [Min(0)] public int rewardAmount = 120;
    public int DisplayedResource { get; private set; }
    public bool IsResourceIncreasing { get; private set; }
    [Min(0f)] public float holdSeconds = 1f;
    [Min(.01f)] public float zoomOutSeconds = .6f;
    [Min(.01f)] public float flightSeconds = .65f;
    [Min(0f)] public float replayDelay = 1.2f;
    public bool loop = true;
    private float elapsed;

    private void OnEnable() => Replay();
    public void Replay()
    {
        elapsed = 0f;
        RenderAt(0f);
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(0f, holdSeconds) + Mathf.Max(.01f, zoomOutSeconds)
            + Mathf.Max(.01f, flightSeconds) + LastIconDelay + Mathf.Max(0f, replayDelay);
        if (loop && elapsed >= duration) elapsed = 0f;
        RenderAt(elapsed);
    }

    private int IconCount => 1 + (multipleIcons && additionalIcons != null ? additionalIcons.Length : 0);
    private float LastIconDelay => (IconCount - 1) * Mathf.Max(0f, iconInterval);

    public void ShowSingle() { multipleIcons = false; motionMode = 0; Replay(); }
    public void ShowMultiple() { multipleIcons = true; motionMode = 0; Replay(); }
    public void ShowSequentialSpeed() { multipleIcons = true; motionMode = 1; Replay(); }
    public void ShowSequentialSpeedAndZoom() { multipleIcons = true; motionMode = 2; Replay(); }

    public void RenderAt(float time)
    {
        if (icon == null || startPoint == null || controlPoint == null || endPoint == null) return;
        if (curveSegments != null)
            for (int i = 0; i < curveSegments.Length; i++)
            {
                var segment = curveSegments[i];
                if (segment == null || segment.parent == null) continue;
                Vector3 a = segment.parent.InverseTransformPoint(Evaluate((float)i / curveSegments.Length));
                Vector3 b = segment.parent.InverseTransformPoint(Evaluate((float)(i + 1) / curveSegments.Length));
                segment.localPosition = (a + b) * .5f;
                segment.sizeDelta = new Vector2((b - a).magnitude, 3f);
                segment.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            }
        float zoomStart = Mathf.Max(0f, holdSeconds);
        float flightStart = zoomStart + Mathf.Max(.01f, zoomOutSeconds);
        if (dialogueBubble != null) dialogueBubble.SetActive(time < zoomStart);
        float zoom = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((time - zoomStart) / Mathf.Max(.01f, zoomOutSeconds)));
        float zoomScale = Mathf.Lerp(Mathf.Max(1f, zoomStartScale), 1f, zoom);
        if (zoomPreview != null)
        {
            zoomPreview.localScale = Vector3.one * zoomScale;
        }
        int arrived = 0;
        for (int i = 0; i < IconCount; i++)
        {
            var item = i == 0 ? icon : additionalIcons[i - 1];
            float localTime = time - flightStart - i * Mathf.Max(0f, iconInterval);
            float t = Mathf.Clamp01(localTime / Mathf.Max(.01f, flightSeconds));
            if (motionMode > 0)
            {
                float order = IconCount > 1 ? (float)i / (IconCount - 1) : 0f;
                float slowdown = order * (1f - 1f / Mathf.Max(1f, lastIconAcceleration));
                // Velocity is 1 + slowdown*cos(2*pi*t): slow down in the middle, then recover.
                t += slowdown * Mathf.Sin(2f * Mathf.PI * t) / (2f * Mathf.PI);
            }
            bool finished = localTime >= Mathf.Max(.01f, flightSeconds);
            if (finished) arrived++;
            if (item == null) continue;
            item.gameObject.SetActive(!finished);
            item.position = Evaluate(t);
            float iconZoom = motionMode == 2 ? zoomScale / Mathf.Max(1f, zoomStartScale) : 1f;
            item.localScale = Vector3.one * (1f + 2f * 4f * t * (1f - t)) * iconZoom;
        }
        if (!multipleIcons && additionalIcons != null)
            foreach (var item in additionalIcons) if (item != null) item.gameObject.SetActive(false);
        DisplayedResource = initialResource + (int)((long)Mathf.Max(0, rewardAmount) * arrived / IconCount);
        IsResourceIncreasing = arrived > 0 && arrived < IconCount;
        if (resourceValue != null)
        {
            resourceValue.text = DisplayedResource.ToString("N0");
            float pulse = IsResourceIncreasing ? Mathf.Abs(Mathf.Sin((time - flightStart) * 24f)) * .10f : 0f;
            resourceValue.rectTransform.localScale = Vector3.one * (1f + pulse);
            resourceValue.color = Color.black;
        }
        if (phaseLabel != null)
            phaseLabel.text = time < zoomStart ? "보상 확인" : time < flightStart ? "줌아웃 · 아이콘 대기"
                : arrived == IconCount ? "전부 도착 · 자원 증가 완료" : $"아이콘 이동 · 도착 {arrived}/{IconCount}";
    }

    private Vector3 Evaluate(float t) => (1f - t) * (1f - t) * startPoint.position
        + 2f * (1f - t) * t * controlPoint.position + t * t * endPoint.position;
}
