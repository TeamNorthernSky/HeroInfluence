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

    [Tooltip("효과음 재생 길이(초). 0이면 원본 클립을 끝까지 재생합니다. 원본 길이보다 길게 재생하지는 않습니다.")]
    [SerializeField, Min(0f)] private float _durationSeconds;

    public string SfxKey => DHAudioClipCatalog.NormalizeKey(_sfxKey);
    public float DurationSeconds => Mathf.Max(0f, _durationSeconds);

    /// <summary>툴링에서 마커 생성 시 효과음 키를 설정한다.</summary>
    public void Configure(string key)
    {
        _sfxKey = DHAudioClipCatalog.NormalizeKey(key);
    }

    PropertyName INotification.id => new PropertyName("PresentationSfx");

    NotificationFlags INotificationOptionProvider.flags =>
        NotificationFlags.TriggerOnce | NotificationFlags.Retroactive;
}
