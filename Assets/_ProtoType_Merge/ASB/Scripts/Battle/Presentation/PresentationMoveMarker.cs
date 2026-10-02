using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Marks the beginning or end of actor movement owned by a Timeline segment.</summary>
public sealed class PresentationMoveMarker : Marker, INotification, INotificationOptionProvider
{
    public const float MinRotationPortion = 0.05f;

    [SerializeField] private PresentationSectionBoundary _boundary = PresentationSectionBoundary.Start;
    [SerializeField, Min(0)] private int _targetSlot;

    [Tooltip("Start 마커에서만 사용. 이동 진행도(0~1)를 위치 비율(0~1)로 바꾸는 곡선. 비우면 등속. " +
             "결과는 0~1로 제한되며, 끝값이 0→1이 아니면 Start/End에서 위치가 튑니다.")]
    [SerializeField] private AnimationCurve _positionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Start 마커에서만 사용. 이동 구간 중 대상 쪽 회전을 마치는 비율. " +
             "1이면 이동 내내 천천히 돌고, 작을수록 출발 직후 대상 쪽을 봅니다.")]
    [SerializeField, Range(MinRotationPortion, 1f)] private float _rotationPortion = 0.35f;

    public PresentationSectionBoundary Boundary => _boundary;
    public int TargetSlot => _targetSlot;
    public AnimationCurve PositionCurve => _positionCurve;
    public float RotationPortion => _rotationPortion;

    public void Configure(PresentationSectionBoundary boundary, int targetSlot = 0)
    {
        _boundary = boundary;
        _targetSlot = Mathf.Max(0, targetSlot);
    }

    // Configure와 이름을 나눈다 — 테스트가 GetMethod("Configure")로 찾으므로 오버로드하면 모호해진다.
    public void ConfigureEasing(AnimationCurve positionCurve, float rotationPortion)
    {
        _positionCurve = positionCurve;
        _rotationPortion = Mathf.Clamp(rotationPortion, MinRotationPortion, 1f);
    }

    /// <summary>선형 진행도 t(0~1)를 위치 보간 비율로 바꾼다. 곡선이 비면 등속.</summary>
    public float EvaluatePosition(float t)
    {
        t = Mathf.Clamp01(t);
        if (_positionCurve == null || _positionCurve.length == 0) return t;
        float value = _positionCurve.Evaluate(t);
        return float.IsNaN(value) || float.IsInfinity(value) ? t : Mathf.Clamp01(value);
    }

    /// <summary>선형 진행도 t(0~1)를 회전 보간 비율로 바꾼다. RotationPortion 시점에 회전을 마친다.</summary>
    public float EvaluateRotation(float t)
    {
        float portion = Mathf.Clamp(_rotationPortion, MinRotationPortion, 1f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t) / portion);
    }

    PropertyName INotification.id => new PropertyName("PresentationMove");
    NotificationFlags INotificationOptionProvider.flags =>
        NotificationFlags.TriggerOnce | NotificationFlags.Retroactive;
}
