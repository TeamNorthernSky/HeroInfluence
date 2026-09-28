using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// 지그 프리뷰의 <b>에디트 모드 슬로우 재생</b>. <c>EditorApplication.update</c>로 director.time을 배속만큼 밀고
    /// <c>Evaluate()</c>로 포즈를 갱신한다.
    ///
    /// <b>순수 감상용이다 — 재생 속도만 바꾼다.</b> 클립·마커·<c>Cue.Time</c>·<c>BlendInSeconds</c>는 건드리지 않는다.
    /// (클립 Speed Multiplier와 다르다 — 그건 클립 길이/겹침 기하를 왜곡하므로 지그에 쓰지 않는다.)
    ///
    /// 정리는 <see cref="JigPreviewInstance.DestroyInstance"/>가 <see cref="Stop"/>을 부르는 것으로 일원화한다
    /// (창 닫기·도메인 리로드·플레이모드·종료 네 훅 전부 그 함수를 지난다). director가 사라지면 Tick도 자체 정지한다.
    /// </summary>
    public static class JigPreviewPlayback
    {
        private static bool _playing;
        private static float _speed = 1f;
        private static double _lastTime;
        private static double _rangeStart;
        private static double _rangeEnd = double.PositiveInfinity;

        // ── Hold 마커 프리뷰(런타임 FreezeForHoldRoutine의 에디터판) ──
        // 목록은 재생 패스 시작(Play/SetRange/루프/되감기)에 수집한다. 재생 중 마커를 옮기면 다음 패스부터 반영된다.
        private static List<KeyValuePair<double, float>> _holds = new List<KeyValuePair<double, float>>();
        private static bool[] _holdConsumed = new bool[0];
        private static bool _holding;
        private static double _holdTime;
        private static float _holdRemaining;   // 배속-초(마커 DurationSeconds와 같은 단위)
        private static double _lastSetTime;    // Tick이 마지막으로 설정한 director.time — 사용자 되감기 감지용

        public static bool IsPlaying => _playing;
        public static float Speed => _speed;

        /// <summary>Hold 마커로 정지 중인지.</summary>
        public static bool IsHolding => _playing && _holding;

        /// <summary>남은 Hold 시간(배속-초). 정지 중이 아니면 0.</summary>
        public static float HoldRemaining => IsHolding ? Mathf.Max(0f, _holdRemaining) : 0f;

        /// <summary>현재 director.time 위치의 구간 속도(SpeedRegion 마커). UI 최종배속 표시용. 없으면 1.0.</summary>
        public static float CurrentRegionSpeed()
        {
            PlayableDirector director = JigPreviewInstance.Director;
            TimelineAsset timeline = director != null ? director.playableAsset as TimelineAsset : null;
            return timeline != null ? PresentationTimelineSpeed.SpeedAt(timeline, director.time) : 1f;
        }

        public static double RangeStart => _rangeStart;
        public static double RangeEnd => _rangeEnd;
        public static bool IsRangePlayback => !double.IsPositiveInfinity(_rangeEnd);

        public static void Play(float speed)
        {
            PlayableDirector director = JigPreviewInstance.Director;
            if (director == null || director.playableAsset == null)
            {
                return;
            }

            _speed = Mathf.Max(0.01f, speed);

            double start = IsRangePlayback ? _rangeStart : 0d;
            double end = IsRangePlayback ? _rangeEnd : director.duration;

            // 범위 밖이거나 끝에 있으면 범위 시작부터 재생한다.
            if (director.time < start - 1e-4d || director.time >= end - 1e-4d)
            {
                director.time = start;
                director.Evaluate();
            }

            // 재생(재)시작 — 현재 시점부터 새 패스로: fired 리셋 + 이전 스폰물/오디오 정리(§4.1).
            JigCuePreview.ResetPass();
            ResetHoldPass(director);

            if (!_playing)
            {
                _playing = true;
                _lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
        }

        public static void SetSpeed(float speed) => _speed = Mathf.Max(0.01f, speed);

        public static void SetFullRange()
        {
            _rangeStart = 0d;
            _rangeEnd = double.PositiveInfinity;
            ResetHoldPass(JigPreviewInstance.Director);
        }

        public static void SetRange(PresentationTimelineRange range)
        {
            _rangeStart = range.Start;
            _rangeEnd = range.End;

            PlayableDirector director = JigPreviewInstance.Director;
            if (director != null)
            {
                director.time = range.Start;
                director.Evaluate();
            }
            JigCuePreview.ResetPass();
            ResetHoldPass(director);
        }

        public static void Stop()
        {
            // ★재생 여부와 무관하게 미리보기 정리를 먼저 수행한다(§6.3) — 정지 상태에서 프리뷰 파괴·토글 해제도 정리되게.
            JigCuePreview.CleanupAll();
            _holding = false;
            _holdRemaining = 0f;

            if (!_playing)
            {
                return;
            }
            _playing = false;
            EditorApplication.update -= Tick;
        }

        private static void Tick()
        {
            PlayableDirector director = JigPreviewInstance.Director;
            if (director == null || director.playableAsset == null)
            {
                Stop();
                return;
            }

            double realNow = EditorApplication.timeSinceStartup;
            double realDt = realNow - _lastTime;
            _lastTime = realNow;
            if (realDt <= 0d)
            {
                return;
            }

            double prev = director.time;

            // 재생 중 사용자가 플레이헤드를 뒤로 끌었으면 Hold를 새 패스로 다시 소비할 수 있게 한다.
            if (prev < _lastSetTime - 1e-4d)
            {
                ResetHoldPass(director);
            }

            if (_holding)
            {
                // 사용자가 정지 중 플레이헤드를 옮겼으면 Hold를 취소하고 그 위치부터 정상 진행한다.
                if (System.Math.Abs(prev - _holdTime) > 1e-4d)
                {
                    _holding = false;
                }
                else
                {
                    // Hold: 포즈·시간 고정, 남은 시간은 프리뷰 배속만 적용(런타임도 구간 속도는 곱하지 않는다).
                    // 파티클은 계속 진행한다 — 런타임도 director만 멈추고 이펙트는 재생된다.
                    double holdStep = realDt * _speed;
                    _holdRemaining -= (float)holdStep;
                    JigCuePreview.DriveOnly(holdStep);
                    if (_holdRemaining <= 0f)
                    {
                        _holding = false;
                    }
                    _lastSetTime = prev;
                    InternalEditorUtility.RepaintAllViews();
                    return;
                }
            }

            // 프리뷰 배속 × 구간속도(런타임과 동일 계산). Editor 프레임 지연으로 한 tick에 여러 마커를
            // 넘으면 이전 구간 속도로 realDt 전체를 계산 = 런타임과 동일 "한 프레임 오차 허용" 정책.
            float regionSpeed = PresentationTimelineSpeed.SpeedAt(
                director.playableAsset as TimelineAsset, prev);
            double advanced = prev + realDt * _speed * regionSpeed;
            double rangeStart = IsRangePlayback ? _rangeStart : 0d;
            double rangeEnd = IsRangePlayback ? _rangeEnd : director.duration;
            double rangeDuration = rangeEnd - rangeStart;

            bool looped = false;
            double t = advanced;
            if (rangeDuration > 0d && advanced >= rangeEnd)
            {
                t = rangeStart + ((advanced - rangeStart) % rangeDuration);
                looped = true;
            }

            double passFrom = prev;
            if (looped)
            {
                JigCuePreview.ResetPass();       // 루프 → 이전 패스 정리 + fired 리셋
                ResetHoldPass(director);
                passFrom = rangeStart;           // 새 패스 [rangeStart, t]
            }

            // Hold: 이 tick 구간에 걸린 첫 Hold에서 멈춘다. 런타임은 마커를 지난 프레임의 시각에서 멈추지만(최대 1프레임 늦음),
            // 프리뷰는 포즈를 정확히 보기 위해 마커 시각에 맞춘다. 이번 tick의 남은 시간은 버린다(한 프레임 오차 허용).
            int crossed = JigHoldPreview.FindCrossedHold(_holds, _holdConsumed, passFrom, t);
            if (crossed >= 0)
            {
                _holdConsumed[crossed] = true;
                _holdTime = _holds[crossed].Key;
                _holdRemaining = _holds[crossed].Value;
                _holding = true;
                t = _holdTime;
            }

            director.time = t;
            director.Evaluate();
            _lastSetTime = t;

            // Cue 미리보기 구동 — 이 tick 구간에 걸린 Cue 발화 + 스폰 이펙트 dt 진행.
            JigCuePreview.Advance(passFrom, t);

            // 에디트 모드에선 포즈를 바꿔도 자동 리페인트가 없다 → Game/Scene 뷰에 강제 반영한다.
            InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>새 재생 패스 — Hold 목록을 다시 모으고 소비 상태·정지 상태를 초기화한다.</summary>
        private static void ResetHoldPass(PlayableDirector director)
        {
            TimelineAsset timeline = director != null ? director.playableAsset as TimelineAsset : null;
            _holds = JigHoldPreview.CollectHolds(timeline);
            _holdConsumed = new bool[_holds.Count];
            _holding = false;
            _holdRemaining = 0f;
            _lastSetTime = director != null ? director.time : 0d;
        }
    }
}
