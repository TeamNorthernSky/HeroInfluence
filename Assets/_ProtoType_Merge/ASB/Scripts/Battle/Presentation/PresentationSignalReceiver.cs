using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Path A Timeline 재생 중 <see cref="PresentationSignalMarker"/> 알림을 받아 라우팅한다(지시서 §6):
///   Cue    → 라우터의 <c>PresentationCueById</c>(이름 충돌 없이 id로 발화)
///   Impact → 대미지 콜백 1회
///   SFX    → <see cref="PresentationSfxPlayer"/>로 DH 효과음 재생
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
    private Action<int> _onImpactSlot;
    private Action _onProjectile;
    private Action<PresentationMoveMarker> _onMove;
    private Action<PresentationReturnMarker> _onReturn;
    private Action<PresentationHitStopMarker> _onHitStop;
    private Action<PresentationSignalMarker> _onCue;
    private double _activeStart = double.NegativeInfinity;
    private double _activeEnd = double.PositiveInfinity;
    private bool _sfxConfigured;
    private readonly HashSet<PresentationSfxMarker> _playedSfx =
        new HashSet<PresentationSfxMarker>(SfxMarkerReferenceComparer.Instance);

    private sealed class SfxMarkerReferenceComparer : IEqualityComparer<PresentationSfxMarker>
    {
        public static readonly SfxMarkerReferenceComparer Instance = new SfxMarkerReferenceComparer();

        public bool Equals(PresentationSfxMarker x, PresentationSfxMarker y) => ReferenceEquals(x, y);
        public int GetHashCode(PresentationSfxMarker marker) => RuntimeHelpers.GetHashCode(marker);
    }

    public void Configure(UnitAnimationEventRouter router, Action onImpact, Action onProjectile = null)
    {
        Configure(router, onImpact, onProjectile, double.NegativeInfinity, double.PositiveInfinity);
    }

    /// <summary>
    /// 부분 구간 재생 시 범위 밖의 Retroactive Marker 알림을 차단한다. Full 재생은 무한 범위를 사용한다.
    /// </summary>
    public void Configure(UnitAnimationEventRouter router, Action onImpact, Action onProjectile,
        double activeStart, double activeEnd)
    {
        _router = router;
        _onImpact = onImpact;
        _onImpactSlot = null;
        _onProjectile = onProjectile;
        _onMove = null;
        _onReturn = null;
        _activeStart = activeStart;
        _activeEnd = activeEnd;
        _sfxConfigured = true;
        _playedSfx.Clear();
    }

    public void ConfigureForPlan(UnitAnimationEventRouter router, Action<int> onImpactSlot,
        Action<PresentationMoveMarker> onMove, double activeStart, double activeEnd)
    {
        ConfigureForPlanWithReturn(router, onImpactSlot, onMove, null, activeStart, activeEnd);
    }

    public void ConfigureForPlanWithReturn(UnitAnimationEventRouter router, Action<int> onImpactSlot,
        Action<PresentationMoveMarker> onMove, Action<PresentationReturnMarker> onReturn,
        double activeStart, double activeEnd)
    {
        _router = router;
        _onImpact = null;
        _onImpactSlot = onImpactSlot;
        _onProjectile = null;
        _onMove = onMove;
        _onReturn = onReturn;
        _activeStart = activeStart;
        _activeEnd = activeEnd;
        _sfxConfigured = true;
        _playedSfx.Clear();
    }

    public void ConfigureMovementCallbacks(Action<PresentationMoveMarker> onMove,
        Action<PresentationReturnMarker> onReturn)
    {
        _onMove = onMove;
        _onReturn = onReturn;
    }

    public void ConfigureHitStopCallback(Action<PresentationHitStopMarker> onHitStop)
    {
        _onHitStop = onHitStop;
    }

    /// <summary>
    /// Cue 마커를 라우터(스킬 연출 Cue) 대신/함께 직접 받는다. 스킬 연출 데이터가 없는 반응 Timeline(율리아 흡수 등)이
    /// 시점만 마커로 정하고 그리기는 자기 컴포넌트가 맡을 때 쓴다. ClearConfig에서 해제된다.
    /// </summary>
    public void ConfigureCueCallback(Action<PresentationSignalMarker> onCue)
    {
        _onCue = onCue;
    }

    public void ClearConfig()
    {
        _router = null;
        _onImpact = null;
        _onImpactSlot = null;
        _onProjectile = null;
        _onMove = null;
        _onReturn = null;
        _onHitStop = null;
        _onCue = null;
        _activeStart = double.NegativeInfinity;
        _activeEnd = double.PositiveInfinity;
        _sfxConfigured = false;
        _playedSfx.Clear();
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is PresentationHitStopMarker hitStop)
        {
            if (hitStop.time >= _activeStart - 0.0001d && hitStop.time <= _activeEnd + 0.0001d)
                _onHitStop?.Invoke(hitStop);
            return;
        }
        if (notification is PresentationSfxMarker sfx)
        {
            if (!_sfxConfigured || sfx.time < _activeStart - 0.0001d ||
                sfx.time > _activeEnd + 0.0001d) return;

            string key = sfx.SfxKey;
            if (string.IsNullOrEmpty(key)) return;

            bool duplicate = !_playedSfx.Add(sfx);
            if (LogSignals)
                Debug.Log($"[PathA-SFX] '{key}' 마커 도달 ({sfx.time:F3}s, 중복 무시={duplicate})", this);
            if (!duplicate)
            {
                if (sfx.DurationSeconds > 0f)
                    PresentationSfxPlayer.PlayForDuration(key, sfx.DurationSeconds);
                else
                    PresentationSfxPlayer.Play(key);
            }
            return;
        }

        if (notification is PresentationMoveMarker move)
        {
            if (LogSignals) Debug.Log($"[PathA-Move] {move.Boundary} @ {move.time:F3}s", this);
            if (move.time >= _activeStart - 0.0001d && move.time <= _activeEnd + 0.0001d)
                _onMove?.Invoke(move);
            return;
        }
        if (notification is PresentationReturnMarker returnMarker)
        {
            if (LogSignals) Debug.Log($"[PathA-Return] {returnMarker.Boundary} @ {returnMarker.time:F3}s", this);
            if (returnMarker.time >= _activeStart - 0.0001d && returnMarker.time <= _activeEnd + 0.0001d)
                _onReturn?.Invoke(returnMarker);
            return;
        }
        if (!(notification is PresentationSignalMarker marker)) return;
        if (marker.time < _activeStart - 0.0001d || marker.time > _activeEnd + 0.0001d) return;

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
                _onCue?.Invoke(marker);
                break;
            case PresentationSignalKind.Impact:
                if (_onImpactSlot != null) _onImpactSlot(marker.TargetSlot);
                else _onImpact?.Invoke();
                break;
            case PresentationSignalKind.Projectile:
                _onProjectile?.Invoke();
                break;
        }
    }
}
