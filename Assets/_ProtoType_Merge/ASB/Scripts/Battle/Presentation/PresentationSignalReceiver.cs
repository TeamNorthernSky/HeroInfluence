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
    private UnitAnimationEventRouter _router;
    private Action _onImpact;

    public void Configure(UnitAnimationEventRouter router, Action onImpact)
    {
        _router = router;
        _onImpact = onImpact;
    }

    public void ClearConfig()
    {
        _router = null;
        _onImpact = null;
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (!(notification is PresentationSignalMarker marker)) return;

        switch (marker.Kind)
        {
            case PresentationSignalKind.Cue:
                _router?.PresentationCueById(marker.CueId);
                break;
            case PresentationSignalKind.Impact:
                _onImpact?.Invoke();
                break;
        }
    }
}
