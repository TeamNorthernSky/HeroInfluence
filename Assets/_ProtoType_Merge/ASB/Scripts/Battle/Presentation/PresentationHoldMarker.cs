using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Timeline 재생을 이 시점에서 지정 시간만큼 <b>일시정지(freeze)</b>하는 마커.
/// Path B의 클립 이벤트 <c>AniEvent_HoldBegin</c>(애니 정지)의 Timeline 버전이다.
///
/// 이벤트(INotification)가 아니라 데이터 마커다 — 재생기(SkillPresentationDirector)가 재생 루프에서
/// director.time이 이 시점을 지날 때 그래프 루트 속도를 0으로 만들어 <see cref="DurationSeconds"/>(배속-초)
/// 동안 포즈·시간을 정지시키고 재개한다.
///
/// charge(이펙트 스폰)와는 <b>기능이 다르다</b>: charge는 "무엇을 낼지", Hold는 "얼마나 멈출지".
/// 정지 시간 동안 이미 스폰된 charge 이펙트가 눈에 보이게 된다.
/// </summary>
public sealed class PresentationHoldMarker : Marker
{
    public const float MaxDuration = 8f;

    [Tooltip("정지 시간(배속-초). 전투 배속이 높을수록 실제 정지는 짧아진다(HoldBegin과 동일). 0이면 정지 없음.")]
    [SerializeField, Range(0f, MaxDuration)] private float _durationSeconds = 0.5f;

    /// <summary>정지 시간(배속-초). NaN/Infinity는 0으로, [0,MaxDuration]로 Clamp.</summary>
    public float DurationSeconds =>
        float.IsNaN(_durationSeconds) || float.IsInfinity(_durationSeconds)
            ? 0f : Mathf.Clamp(_durationSeconds, 0f, MaxDuration);

    public void Configure(float durationSeconds) =>
        _durationSeconds = float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)
            ? 0f : Mathf.Clamp(durationSeconds, 0f, MaxDuration);
}
