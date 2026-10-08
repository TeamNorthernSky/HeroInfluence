using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Path A Timeline 효과음 알림 마커. 재생 중 이 지점을 지나면
/// <see cref="PresentationSignalReceiver"/>가 DH AudioManager에 SFX 키를 전달한다.
/// </summary>
public sealed class PresentationSfxMarker : Marker, INotification, INotificationOptionProvider
{
    [Tooltip("DHAudioClipCatalog의 SfxClips 키(예: Battle_Hit01).")]
    [SerializeField] private string _sfxKey;

    public string SfxKey => DHAudioClipCatalog.NormalizeKey(_sfxKey);

    /// <summary>툴링에서 마커 생성 시 효과음 키를 설정한다.</summary>
    public void Configure(string key)
    {
        _sfxKey = DHAudioClipCatalog.NormalizeKey(key);
    }

    PropertyName INotification.id => new PropertyName("PresentationSfx");

    NotificationFlags INotificationOptionProvider.flags =>
        NotificationFlags.TriggerOnce | NotificationFlags.Retroactive;
}
