using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 총기 발사 반동 시점 마커. 한 발마다 하나씩 찍는다.
/// 기본 Markers 트랙에 두면 Director와 같은 GameObject의 <see cref="GunRecoilMotion"/>이 직접 받는다.
/// 반동은 코스메틱이라 <see cref="NotificationFlags.Retroactive"/>를 쓰지 않는다 — 마커 뒤에서 재생을 시작하면 반동도 없다.
/// </summary>
public sealed class PresentationRecoilMarker : Marker, INotification, INotificationOptionProvider
{
    [Tooltip("반동 세기 배율. 1 = GunRecoilMotion 기본 각도·밀림 거리.")]
    [SerializeField, Range(0f, 3f)] private float _strength = 1f;

    public float Strength => _strength;

    PropertyName INotification.id => new PropertyName("PresentationRecoil");
    NotificationFlags INotificationOptionProvider.flags => NotificationFlags.TriggerOnce;
}
