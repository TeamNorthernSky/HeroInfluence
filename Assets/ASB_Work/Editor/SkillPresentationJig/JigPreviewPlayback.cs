using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Playables;

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

        public static bool IsPlaying => _playing;
        public static float Speed => _speed;

        public static void Play(float speed)
        {
            PlayableDirector director = JigPreviewInstance.Director;
            if (director == null || director.playableAsset == null)
            {
                return;
            }

            _speed = Mathf.Max(0.01f, speed);

            // 끝에 있으면 처음부터 재생한다.
            if (director.duration > 0d && director.time >= director.duration - 1e-4d)
            {
                director.time = 0d;
            }

            // 재생(재)시작 — 현재 시점부터 새 패스로: fired 리셋 + 이전 스폰물/오디오 정리(§4.1).
            JigCuePreview.ResetPass();

            if (!_playing)
            {
                _playing = true;
                _lastTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
        }

        public static void SetSpeed(float speed) => _speed = Mathf.Max(0.01f, speed);

        public static void Stop()
        {
            // ★재생 여부와 무관하게 미리보기 정리를 먼저 수행한다(§6.3) — 정지 상태에서 프리뷰 파괴·토글 해제도 정리되게.
            JigCuePreview.CleanupAll();

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
            double advanced = prev + realDt * _speed;   // 배속 반영
            double duration = director.duration;

            bool looped = false;
            double t = advanced;
            if (duration > 0d && advanced >= duration)
            {
                t = advanced % duration;
                looped = true;
            }

            director.time = t;
            director.Evaluate();

            // Cue 미리보기 구동 — 이 tick 구간에 걸린 Cue 발화 + 스폰 이펙트 dt 진행.
            if (looped)
            {
                JigCuePreview.ResetPass();       // 루프 → 이전 패스 정리 + fired 리셋
                JigCuePreview.Advance(0d, t);    // 새 패스 [0, t]
            }
            else
            {
                JigCuePreview.Advance(prev, t);
            }

            // 에디트 모드에선 포즈를 바꿔도 자동 리페인트가 없다 → Game/Scene 뷰에 강제 반영한다.
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
