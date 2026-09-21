using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Timeline 구간 재생 속도 마커. 발화(INotification)가 아니라 <b>"상태 데이터"</b>다 —
/// 재생기가 매 프레임 director.time으로 <see cref="PresentationTimelineSpeed.SpeedAt"/>를 조회해 적용한다.
///
/// 이 마커 시각부터 다음 속도 마커까지 이 배속이 유지된다(첫 마커 이전 기본 1.0).
/// 최종 루트 속도 = 전투배속 × 구간속도(SkillPresentationDirector가 곱한다).
///
/// 에디터 전용 CueMarker/발화용 PresentationSignalMarker와 별개다 — 이건 런타임 어셈블리 타입이며
/// PresentationSectionMarker와 마찬가지로 발화하지 않고 데이터로만 읽힌다.
/// </summary>
public sealed class PresentationSpeedMarker : Marker
{
    public const float MinSpeed = 0.01f;
    public const float MaxSpeed = 8f;

    [Tooltip("이 시각부터 다음 속도 마커까지 적용할 배속. 최종 속도 = 전투배속 × 이 값.")]
    [SerializeField, Range(MinSpeed, MaxSpeed)] private float _speed = 1f;

    /// <summary>정상화된 배속(NaN/Infinity→1.0, [MinSpeed,MaxSpeed] Clamp). 런타임/프리뷰는 이 값을 쓴다.</summary>
    public float Speed =>
        float.IsNaN(_speed) || float.IsInfinity(_speed)
            ? 1f : Mathf.Clamp(_speed, MinSpeed, MaxSpeed);

    /// <summary>저작 원본값. Validator가 정상화 이전 값을 검사하는 데 쓴다(범위 밖/NaN 탐지).</summary>
    public float RawSpeed => _speed;

    public void Configure(float speed) =>
        _speed = float.IsNaN(speed) || float.IsInfinity(speed)
            ? 1f : Mathf.Clamp(speed, MinSpeed, MaxSpeed);
}
