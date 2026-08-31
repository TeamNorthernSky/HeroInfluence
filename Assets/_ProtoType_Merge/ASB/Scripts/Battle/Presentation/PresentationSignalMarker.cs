using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Path A Timeline 신호의 종류.</summary>
public enum PresentationSignalKind
{
    /// <summary>연출 Cue 발화(이펙트/사운드). CueId로 특정.</summary>
    Cue,
    /// <summary>타격 시점 — 대미지 콜백을 1회 호출.</summary>
    Impact
}

/// <summary>
/// Path A <b>런타임</b> Timeline 마커(지시서 §6). 재생 중 이 지점을 지나면 알림이 발생하고,
/// <see cref="PresentationSignalReceiver"/>가 <see cref="Kind"/>에 따라 CueId로 발화하거나 대미지 콜백을 부른다.
///
/// 에디터 전용 <c>CueMarker</c>(Jig 프리뷰용)와 별개다 — 이건 런타임 어셈블리 타입이라 전투 재생에서 쓸 수 있다.
/// <see cref="NotificationFlags.TriggerOnce"/>로 반복/스크럽 중복 발화를 막고,
/// <see cref="NotificationFlags.Retroactive"/>로 시작 지점을 지나쳐 재생을 시작해도 놓치지 않는다.
/// </summary>
public class PresentationSignalMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField] private PresentationSignalKind _kind = PresentationSignalKind.Cue;

    [Tooltip("Kind=Cue일 때 발화할 Cue의 CueId(자동 생성 GUID). Impact면 비워둠.")]
    [SerializeField] private string _cueId;

    public PresentationSignalKind Kind => _kind;
    public string CueId => _cueId;

    /// <summary>런타임에 값 주입용(Jig '굽기'에서 마커 생성 시 설정).</summary>
    public void Configure(PresentationSignalKind kind, string cueId)
    {
        _kind = kind;
        _cueId = cueId;
    }

    PropertyName INotification.id => new PropertyName("PresentationSignal");

    NotificationFlags INotificationOptionProvider.flags =>
        NotificationFlags.TriggerOnce | NotificationFlags.Retroactive;
}
