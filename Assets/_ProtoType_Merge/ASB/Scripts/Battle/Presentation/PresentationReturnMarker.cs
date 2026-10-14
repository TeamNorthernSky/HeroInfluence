using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Timeline이 시전 시작 시 저장한 위치와 회전으로 돌아가는 구간.</summary>
public sealed class PresentationReturnMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField] private PresentationSectionBoundary _boundary = PresentationSectionBoundary.Start;
    [Tooltip("Start 마커에서만 사용. 비우면 등속으로 복귀합니다.")]
    [SerializeField] private AnimationCurve _positionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public PresentationSectionBoundary Boundary => _boundary;

    public void Configure(PresentationSectionBoundary boundary) => _boundary = boundary;

    public void ConfigureEasing(AnimationCurve curve) => _positionCurve = curve;

    public float EvaluatePosition(float t)
    {
        t = Mathf.Clamp01(t);
        if (_positionCurve == null || _positionCurve.length == 0) return t;
        float value = _positionCurve.Evaluate(t);
        return float.IsNaN(value) || float.IsInfinity(value) ? t : Mathf.Clamp01(value);
    }

    PropertyName INotification.id => new PropertyName("PresentationReturn");
    NotificationFlags INotificationOptionProvider.flags =>
        NotificationFlags.TriggerOnce | NotificationFlags.Retroactive;
}
