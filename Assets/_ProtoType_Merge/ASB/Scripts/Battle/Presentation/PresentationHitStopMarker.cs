using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>Timeline에서 독립적으로 히트스탑을 요청하는 연출 마커.</summary>
public sealed class PresentationHitStopMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField, Min(0)] private int _targetSlot;
    [SerializeField, Range(0f, 0.5f)] private float _durationSeconds = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _characterSpeed = 0.05f;
    [SerializeField, Range(0f, 1f)] private float _effectSpeed = 0.25f;

    public int TargetSlot => _targetSlot;
    public HitStopSettings Settings => new HitStopSettings
    {
        Enabled = true,
        DurationSeconds = _durationSeconds,
        CharacterSpeed = _characterSpeed,
        EffectSpeed = _effectSpeed
    };

    public void Configure(float durationSeconds, float characterSpeed, float effectSpeed, int targetSlot = 0)
    {
        _durationSeconds = Mathf.Clamp(durationSeconds, 0f, 0.5f);
        _characterSpeed = Mathf.Clamp01(characterSpeed);
        _effectSpeed = Mathf.Clamp01(effectSpeed);
        _targetSlot = Mathf.Max(0, targetSlot);
    }

    PropertyName INotification.id => new PropertyName("PresentationHitStop");
    NotificationFlags INotificationOptionProvider.flags =>
        NotificationFlags.TriggerOnce | NotificationFlags.Retroactive;
}
