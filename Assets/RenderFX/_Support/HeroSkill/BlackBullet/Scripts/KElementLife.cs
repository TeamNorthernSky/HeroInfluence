using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>구간 보간 방식. AnimationCurve를 쓰지 않는다 — 중첩 로직을 직관적으로 유지하기 위함.</summary>
    public enum KEase { Linear, EaseIn, EaseOut, EaseInOut }

    /// <summary>
    /// 이펙트 요소 1개의 독립 생명주기 정의. 블랙불릿 VFX의 모든 파트(머즐·탄두·궤적·탄착)가
    /// 이 블록을 각자 하나씩 들고, 서로 무관한 자기 시계로 돌아간다.
    ///
    /// 설계 원칙:
    ///  - 수명(lifetime)은 페이즈 게이트가 아니다. 시퀀스 시작 기준 startDelay에 발동해 lifetime 동안 살고 끝난다.
    ///    따라서 한 요소의 수명을 늘려도 다른 요소의 타이밍이 밀리지 않는다.
    ///  - 알파(페이드)와 크기는 서로 독립된 두 봉투다. 겹치면 곱해진다(둘 다 줄어드는 구간은 급격히 사라짐).
    ///  - 크기 구간은 경계 2개로 지정한다(구간 3개를 독립 값으로 두면 합이 1을 넘는 조합이 생김).
    /// </summary>
    [Serializable]
    public class KElementLife
    {
        [Tooltip("이 시각 요소를 표시할지 정합니다. 끄면 해당 요소를 사용하지 않습니다.")]
        public bool enabled = true;

        [Header("수명")]
        [Tooltip("시퀀스 시작 기준 발동 시각(초). 다른 요소와 무관하게 자기 시계로 돈다.")]
        public float startDelay = 0f;
        [Tooltip("이 요소의 총 수명(초).")]
        public float lifetime = 0.2f;

        [Header("페이드 (수명 대비 비율)")]
        [Tooltip("0=페이드인 없음, 0.5=수명의 절반을 페이드인에 사용.")]
        [Range(0f, 0.5f)] public float fadeIn = 0f;
        [Tooltip("0=페이드아웃 없음, 0.5=수명의 절반을 페이드아웃에 사용.")]
        [Range(0f, 0.5f)] public float fadeOut = 0.3f;
        [Tooltip("페이드인 보간. 밝기 0→1 진행에 적용된다.")]
        public KEase fadeInEase = KEase.Linear;
        [Tooltip("페이드아웃 보간. 남은 밝기(1→0)에 적용된다. " +
                 "EaseIn=초반에 급락, EaseOut=후반까지 버티다 급락.")]
        public KEase fadeOutEase = KEase.Linear;

        [Header("크기")]
        [Tooltip("이 요소의 시작 크기입니다. 최대 크기보다 크게 두면 크게 나타난 뒤 줄어드는 팝 효과가 됩니다.")]
        public float startSize = 0.2f;
        [Tooltip("확대 단계가 끝났을 때의 기준 크기입니다. 시작·종료 크기와 함께 크기 변화를 정합니다.")]
        public float maxSize = 0.45f;
        [Tooltip("축소 구간이 수렴하는 크기.")]
        public float endSize = 0.1f;

        [Header("크기 구간 경계 (수명 대비 비율)")]
        [Tooltip("확장이 끝나는 지점. 0이면 처음부터 최대 크기.")]
        [Range(0f, 1f)] public float expandEnd = 0.25f;
        [Tooltip("최대 크기 유지가 끝나는 지점. 이후가 축소 구간. expandEnd보다 작으면 expandEnd로 올려 잡는다.")]
        [Range(0f, 1f)] public float holdEnd = 0.6f;

        [Header("보간")]
        [Tooltip("시작 크기에서 최대 크기로 커지는 속도 곡선입니다. 전체 확대 시간은 별도 시간 항목을 따릅니다.")]
        public KEase expandEase = KEase.Linear;
        [Tooltip("최대 크기에서 종료 크기로 줄어드는 속도 곡선입니다. 전체 축소 시간은 별도 시간 항목을 따릅니다.")]
        public KEase shrinkEase = KEase.Linear;

        [Header("배치")]
        [Tooltip("기준점에서의 추가 오프셋. 소켓 회전만 적용되고 소켓 스케일은 무시된다.")]
        public Vector3 offset = Vector3.zero;

        /// <summary>
        /// 깊은 복사. 프리셋↔프리팹 2중 저장 구조에서 참조를 공유하면 적용·캡처가 무의미해지므로
        /// 반드시 값 단위로 옮긴다.
        /// </summary>
        public void CopyFrom(KElementLife o)
        {
            if (o == null) return;
            enabled = o.enabled;
            startDelay = o.startDelay;
            lifetime = o.lifetime;
            fadeIn = o.fadeIn;
            fadeOut = o.fadeOut;
            fadeInEase = o.fadeInEase;
            fadeOutEase = o.fadeOutEase;
            startSize = o.startSize;
            maxSize = o.maxSize;
            endSize = o.endSize;
            expandEnd = o.expandEnd;
            holdEnd = o.holdEnd;
            expandEase = o.expandEase;
            shrinkEase = o.shrinkEase;
            offset = o.offset;
        }
    }

    /// <summary>KElementLife 샘플러. 요소별 상태를 갖지 않는 순수 함수로 둔다.</summary>
    public static class KLifeEval
    {
        public static float Ease(KEase mode, float t)
        {
            t = Mathf.Clamp01(t);
            switch (mode)
            {
                case KEase.EaseIn:    return t * t;
                case KEase.EaseOut:   return 1f - (1f - t) * (1f - t);
                case KEase.EaseInOut: return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
                default:              return t;
            }
        }

        /// <summary>
        /// 요소 자기 시계 t(초, startDelay 차감 후)에서의 크기·페이드를 구한다.
        /// </summary>
        /// <returns>살아 있으면 true. t가 수명을 넘었거나 아직 발동 전이면 false.</returns>
        public static bool Sample(KElementLife L, float t, out float size, out float fade)
            => Sample(L, t, L != null ? L.lifetime : 0f, out size, out fade);

        /// <summary>
        /// 수명을 외부에서 지정해 샘플링. 요소가 다른 시스템의 시간축에 종속돼
        /// 유효 수명이 계산으로 결정되는 경우에 쓴다(예: 궤적은 TrailRenderer 정점 만료 시각에 묶인다).
        /// 블록의 lifetime 값을 건드리지 않으므로 프리셋 캡처가 오염되지 않는다.
        /// </summary>
        public static bool Sample(KElementLife L, float t, float lifetime, out float size, out float fade)
        {
            size = 0f; fade = 0f;
            if (L == null || !L.enabled) return false;
            float life = Mathf.Max(lifetime, 1e-5f);
            if (t < 0f || t > life) return false;

            float u = t / life;

            // ── 페이드: 인/아웃이 각각 0~0.5로 캡되어 합이 1을 넘지 않는다 ──
            fade = 1f;
            if (L.fadeIn > 1e-5f && u < L.fadeIn)
                fade = Ease(L.fadeInEase, u / L.fadeIn);
            float outStart = 1f - L.fadeOut;
            if (L.fadeOut > 1e-5f && u > outStart)
            {
                // k = 남은 밝기 비율(페이드아웃 시작 1 → 수명 끝 0). 이징은 이 k에 적용된다.
                float k = (1f - u) / L.fadeOut;
                fade = Mathf.Min(fade, Ease(L.fadeOutEase, k));
            }
            fade = Mathf.Clamp01(fade);

            // ── 크기: 확장 → 유지 → 축소. 경계 2개, holdEnd는 expandEnd 아래로 내려가지 않는다 ──
            float e = Mathf.Clamp01(L.expandEnd);
            float h = Mathf.Max(e, Mathf.Clamp01(L.holdEnd));

            if (u <= e)
            {
                float k = e > 1e-5f ? u / e : 1f;
                size = Mathf.LerpUnclamped(L.startSize, L.maxSize, Ease(L.expandEase, k));
            }
            else if (u <= h)
            {
                size = L.maxSize;
            }
            else
            {
                float span = 1f - h;
                float k = span > 1e-5f ? (u - h) / span : 1f;
                size = Mathf.LerpUnclamped(L.maxSize, L.endSize, Ease(L.shrinkEase, k));
            }

            return true;
        }
    }
}
