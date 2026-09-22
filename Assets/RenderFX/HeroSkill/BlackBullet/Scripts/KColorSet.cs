using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 이펙트 요소 1개의 색 설정. 저스티스 `JusticeArcShardPreset`의 색 체계를 이식한 것.
    ///
    /// 구조: **테두리(3스톱 그라데이션) + 내부(심) 단색**.
    ///   - headColor / midColor / tailColor — 테두리 색이 축을 따라 변한다.
    ///   - innerColor — 내부(심) 색. 테두리와 완전히 독립이다.
    ///   - 각 층은 자기 발광 배수(edgeEmission / innerEmission)를 갖는다.
    ///
    /// ★그라데이션 축은 요소마다 다르다:
    ///   · 머즐·탄두·탄착 → **시간축**. head=갓 생성, mid=중간, tail=소멸 직전(저스티스 원래 의미).
    ///   · 궤적          → **공간축**. head=탄두 쪽, mid=중간, tail=총구 쪽.
    ///     (궤적의 휘도 감쇠는 trailTailBrightnessRatio가 따로 담당한다. 여기서는 색만 맡아
    ///      역할이 겹치지 않는다.)
    /// </summary>
    [Serializable]
    public class KColorSet
    {
        [Header("테두리 (축 방향 3스톱)")]
        [Tooltip("효과가 시작되는 머리 구간의 색입니다. 중간·꼬리 색으로 이어집니다.")]
        [ColorUsage(true, true)] public Color headColor = new Color(0.85f, 0.95f, 1f, 1f);
        [Tooltip("효과 중간 구간의 색입니다. 머리·꼬리 사이의 색 변화를 조절합니다.")]
        [ColorUsage(true, true)] public Color midColor = new Color(0.45f, 0.75f, 1f, 1f);
        [Tooltip("효과 끝 또는 사라지기 직전 구간의 색입니다.")]
        [ColorUsage(true, true)] public Color tailColor = new Color(0.20f, 0.45f, 0.9f, 1f);
        [Tooltip("테두리 발광 배수. chromaHold와 함께 쓴다.")]
        [Range(0f, 24f)] public float edgeEmission = 2.0f;

        [Header("내부 (심)")]
        [Tooltip("내부(심) 색 — 빛나는 흰색이 기본.")]
        [ColorUsage(true, true)] public Color innerColor = Color.white;
        [Tooltip("내부 발광 배수.")]
        [Range(0f, 24f)] public float innerEmission = 1.5f;

        [Header("탈색 방지")]
        [Tooltip("발광을 올릴수록 가산 합성에서 흰색으로 탈색되는 것을 막는다. " +
                 "0이면 전 채널 균등 배수(탈색), 1이면 지배 채널 위주로만 밝아져 색이 유지된다.")]
        [Range(0f, 1f)] public float chromaHold = 1f;

        public void CopyFrom(KColorSet o)
        {
            if (o == null) return;
            headColor = o.headColor;
            midColor = o.midColor;
            tailColor = o.tailColor;
            edgeEmission = o.edgeEmission;
            innerColor = o.innerColor;
            innerEmission = o.innerEmission;
            chromaHold = o.chromaHold;
        }
    }

    /// <summary>KColorSet 평가기. 상태를 갖지 않는 순수 함수로 둔다.</summary>
    public static class KColorEval
    {
        /// <summary>
        /// 색에 발광 배수를 적용하되 탈색을 막는다. 저스티스 JusticeTrailPresetBinder.Boost 이식.
        ///
        /// 단순히 색×발광을 하면 밝아질수록 흰색으로 탈색된다.
        /// 예: 선홍 (1, 0.40, 0.36) × 5.93 = (5.93, 2.37, 2.13) → 사실상 흰색.
        /// chromaHold를 올리면 지배 채널 위주로만 밝아져(가중치 = 채널비²) 낮은 채널이
        /// 1 아래에 머무르므로 밝아져도 색이 유지된다. 흰색(전 채널 동일)은 어느 쪽이든 흰색이다.
        /// </summary>
        public static Color Boost(Color c, float emission, float chromaHold)
        {
            float e = Mathf.Max(0f, emission);
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m <= 0.0001f) return new Color(0f, 0f, 0f, c.a);

            float hold = Mathf.Clamp01(chromaHold);
            float Ch(float v)
            {
                float ratio = v / m;                              // 0..1, 지배 채널이 1
                float w = Mathf.Lerp(1f, ratio * ratio, hold);    // hold=0이면 균등 배수
                return v * (1f + (e - 1f) * w);
            }
            return new Color(Ch(c.r), Ch(c.g), Ch(c.b), c.a);
        }

        /// <summary>축 위치 t(0=head, 0.5=mid, 1=tail)에서의 테두리 색(발광 적용 완료).</summary>
        public static Color EdgeAt(KColorSet s, float t)
        {
            if (s == null) return Color.white;
            t = Mathf.Clamp01(t);
            Color c = t < 0.5f
                ? Color.Lerp(s.headColor, s.midColor, t * 2f)
                : Color.Lerp(s.midColor, s.tailColor, (t - 0.5f) * 2f);
            return Boost(c, s.edgeEmission, s.chromaHold);
        }

        /// <summary>3스톱 각각에 발광을 적용한 값. 셰이더가 축을 따라 보간하는 경우(궤적)에 쓴다.</summary>
        public static void EdgeStops(KColorSet s, out Color head, out Color mid, out Color tail)
        {
            if (s == null) { head = mid = tail = Color.white; return; }
            head = Boost(s.headColor, s.edgeEmission, s.chromaHold);
            mid = Boost(s.midColor, s.edgeEmission, s.chromaHold);
            tail = Boost(s.tailColor, s.edgeEmission, s.chromaHold);
        }

        /// <summary>내부(심) 색(발광 적용 완료).</summary>
        public static Color Inner(KColorSet s)
            => s == null ? Color.white : Boost(s.innerColor, s.innerEmission, s.chromaHold);
    }
}
