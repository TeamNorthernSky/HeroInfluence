using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Path A Timeline 신호의 종류.</summary>
public enum PresentationSignalKind
{
    /// <summary>연출 Cue 발화(이펙트/사운드). CueId로 특정.</summary>
    Cue,
    /// <summary>타격 시점 — 대미지 콜백을 1회 호출.</summary>
    Impact,
    /// <summary>투사체 발사 — 스킬의 ProjectileVisual을 타깃으로 발사하고, 도착 시 대미지를 적용한다.</summary>
    Projectile
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

    [Tooltip("Kind=Cue일 때 발화할 Cue 이름(예: cast). 대부분 이것만 넣으면 됩니다. Impact/Projectile이면 비워둠.")]
    [SerializeField] private string _cueName;

    [Tooltip("선택: CueId(자동 생성 GUID). 넣으면 CueName보다 우선하며 동명 Cue를 정확히 특정합니다. 이름이 유니크하면 비워도 됨.")]
    [SerializeField] private string _cueId;

    public PresentationSignalKind Kind => _kind;
    public string CueName => _cueName;
    public string CueId => _cueId;

    /// <summary>런타임에 값 주입용(Jig '굽기'에서 마커 생성 시 설정).</summary>
    public void Configure(PresentationSignalKind kind, string cueName, string cueId = null)
    {
        _kind = kind;
        _cueName = cueName;
        _cueId = cueId;
    }

    PropertyName INotification.id => new PropertyName("PresentationSignal");

    NotificationFlags INotificationOptionProvider.flags =>
        NotificationFlags.TriggerOnce | NotificationFlags.Retroactive;
}
