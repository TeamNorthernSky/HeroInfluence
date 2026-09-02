using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Path A Timeline 재생 중 <see cref="PresentationSignalMarker"/> 알림을 받아 라우팅한다(지시서 §6):
///   Cue    → 라우터의 <c>PresentationCueById</c>(이름 충돌 없이 id로 발화)
///   Impact → 대미지 콜백 1회
/// 재생기가 재생 전에 <see cref="Configure"/>로 라우터/콜백을 주입하고, 종료 시 <see cref="ClearConfig"/>한다.
///
/// ★알림 수신은 이 컴포넌트가 PlayableDirector와 같은 GameObject에 있을 때 동작한다(마커 트랙 알림 대상).
///   실제 발화 여부는 PlayMode + 저작된 Timeline으로 검증한다(지시서 §15).
/// </summary>
[DisallowMultipleComponent]
public class PresentationSignalReceiver : MonoBehaviour, INotificationReceiver
{
    /// <summary>마커 수신 진단 로그. 저작·디버그 때만 켠다(기본 꺼짐 — 콘솔 조용). <c>PresentationSignalReceiver.LogSignals = true</c>로 켬.</summary>
    public static bool LogSignals;

    private UnitAnimationEventRouter _router;
    private Action _onImpact;
    private Action _onProjectile;

    public void Configure(UnitAnimationEventRouter router, Action onImpact, Action onProjectile = null)
    {
        _router = router;
        _onImpact = onImpact;
        _onProjectile = onProjectile;
    }

    public void ClearConfig()
    {
        _router = null;
        _onImpact = null;
        _onProjectile = null;
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (!(notification is PresentationSignalMarker marker)) return;

        if (LogSignals)
        {
            Debug.Log($"[PathA-Signal] '{marker.Kind}' 마커 도달 (cueName={marker.CueName}, cueId={marker.CueId})", this);
        }

        switch (marker.Kind)
        {
            case PresentationSignalKind.Cue:
                // CueId가 있으면 정확 특정(동명 충돌 없음), 없으면 CueName으로 발화.
                if (!string.IsNullOrEmpty(marker.CueId)) _router?.PresentationCueById(marker.CueId);
                else _router?.PresentationCueByName(marker.CueName);
                break;
            case PresentationSignalKind.Impact:
                _onImpact?.Invoke();
                break;
            case PresentationSignalKind.Projectile:
                _onProjectile?.Invoke();
                break;
        }
    }
}
