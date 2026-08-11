using UnityEngine;

namespace JC.UiRecolor
{
    /// <summary>
    /// UiRecolorForge 색 수학 — sRGB ↔ linear ↔ OKLab ↔ OKLCH 변환 + 소프트 게이트.
    /// ★CPU 단일 경로: 프리뷰와 디스크 저장이 전부 이 함수들을 공유한다(프리뷰≠결과 드리프트 방지).
    /// 판정(유사도 ΔE)·편집(L/C/H 이동) 모두 OKLab 계열에서 수행 — 지각 균일(같은 거리=같은 체감 차이,
    /// hue 회전에 체감 밝기 불변). OKLab 행렬 = Björn Ottosson 표준 계수.
    /// </summary>
    internal static class RecolorMath
    {
        // ── sRGB ↔ linear (PNG 는 sRGB — 변환을 거치지 않으면 감쇠 곡선이 비뚤어진다) ──

        public static float SrgbToLinear(float c)
            => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        public static float LinearToSrgb(float c)
            => c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;

        // ── linear sRGB ↔ OKLab ──

        public static Vector3 LinearToOklab(Vector3 c)
        {
            float l = 0.4122214708f * c.x + 0.5363325363f * c.y + 0.0514459929f * c.z;
            float m = 0.2119034982f * c.x + 0.6806995451f * c.y + 0.1073969566f * c.z;
            float s = 0.0883024619f * c.x + 0.2817188376f * c.y + 0.6299787005f * c.z;
            float l_ = Cbrt(l); float m_ = Cbrt(m); float s_ = Cbrt(s);
            return new Vector3(
                0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_,
                1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_,
                0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_);
        }

        public static Vector3 OklabToLinear(Vector3 lab)
        {
            float l_ = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
            float m_ = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
            float s_ = lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z;
            float l = l_ * l_ * l_; float m = m_ * m_ * m_; float s = s_ * s_ * s_;
            return new Vector3(
                +4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
                -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
                -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
        }

        private static float Cbrt(float v) => v <= 0f ? 0f : Mathf.Pow(v, 1f / 3f);

        // ── OKLab ↔ OKLCH (L=지각 밝기 0~1, C=채도 0~0.4쯤, H=색상각 °) ──

        public static Vector3 OklabToLch(Vector3 lab)
        {
            float c = Mathf.Sqrt(lab.y * lab.y + lab.z * lab.z);
            float h = Mathf.Atan2(lab.z, lab.y) * Mathf.Rad2Deg;
            return new Vector3(lab.x, c, h);
        }

        public static Vector3 LchToOklab(Vector3 lch)
        {
            float rad = lch.z * Mathf.Deg2Rad;
            return new Vector3(lch.x, lch.y * Mathf.Cos(rad), lch.y * Mathf.Sin(rad));
        }

        // ── 단축 파이프라인 ──

        /// <summary>sRGB Color(0~1) → OKLCH.</summary>
        public static Vector3 SrgbToLch(Color c)
        {
            var lin = new Vector3(SrgbToLinear(c.r), SrgbToLinear(c.g), SrgbToLinear(c.b));
            return OklabToLch(LinearToOklab(lin));
        }

        /// <summary>OKLCH → sRGB Color(클램프 색역 처리). 알파는 호출부가 보존.</summary>
        public static Color LchToSrgb(Vector3 lch)
        {
            Vector3 lin = OklabToLinear(LchToOklab(lch));
            // 색역 밖은 단순 클램프(UI 리소스 보정 용도에선 충분 — 큰 이동은 사용자가 눈으로 확인)
            lin.x = Mathf.Clamp01(lin.x); lin.y = Mathf.Clamp01(lin.y); lin.z = Mathf.Clamp01(lin.z);
            return new Color(LinearToSrgb(lin.x), LinearToSrgb(lin.y), LinearToSrgb(lin.z), 1f);
        }

        /// <summary>OKLab 지각 색차(ΔE). 배치의 「같은 색인가」 판정 축.</summary>
        public static float DeltaE(Vector3 lchA, Vector3 lchB)
            => Vector3.Distance(LchToOklab(lchA), LchToOklab(lchB));

        // ── 소프트 게이트 (경계 스무딩 = 이 감쇠 폭들. 공간 블러는 쓰지 않는다 — UI 윤곽 번짐) ──

        /// <summary>x가 radius 이내면 1, radius+falloff 밖이면 0, 사이는 smoothstep 감쇠.</summary>
        public static float FalloffBelow(float x, float radius, float falloff)
        {
            if (falloff < 1e-4f) return x <= radius ? 1f : 0f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - radius) / falloff));
        }

        /// <summary>x가 edge 미만이면 0, edge+soft 이상이면 1 (채도 하한 게이트).</summary>
        public static float GateAbove(float x, float edge, float soft)
        {
            if (soft < 1e-4f) return x >= edge ? 1f : 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - edge) / soft));
        }

        /// <summary>명도 창 게이트: [min, max] 안 1, 바깥으로 soft 폭 감쇠.</summary>
        public static float GateWindow(float x, float min, float max, float soft)
            => GateAbove(x, min - soft, soft) * (1f - GateAbove(x, max, soft));

        /// <summary>hue 원형 거리(°, 0~180).</summary>
        public static float HueDistance(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a, b));

        /// <summary>a→b 의 부호 있는 최단 hue 델타(°).</summary>
        public static float HueDelta(float from, float to) => Mathf.DeltaAngle(from, to);

        /// <summary>저장 양자화용 해시 디더(±0.5/255) — 8bit 그라데이션 밴딩 완화.</summary>
        public static float Dither(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h & 0xFFFF) / 65535f - 0.5f) / 255f;
        }
    }
}
