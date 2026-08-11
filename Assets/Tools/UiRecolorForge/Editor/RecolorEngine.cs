using System.Collections.Generic;
using UnityEngine;

namespace JC.UiRecolor
{
    /// <summary>슬롯의 프레임 불변 산출값 — 픽셀 루프 밖에서 한 번만 계산.</summary>
    internal struct SlotRuntime
    {
        public bool active;
        public RecolorSlot s;
        public Vector3 refLch;    // 기준색 OKLCH
        public Vector3 anchorLch; // 절대 모드의 출발점(편집 중=기준색, 배치=이미지별 자동 검출)
        public Vector3 deltaAbs;  // 절대 모드: 앵커→목표 (dL, dC, dH°)
        public Vector3 anchorLin; // 앵커의 linear RGB — 경계 언믹싱 선분 끝점
        public Vector3 editedLin; // 앵커에 이 슬롯 편집을 적용한 결과의 linear RGB(비활성=원색 그대로)
    }

    /// <summary>배치 리포트 한 행.</summary>
    internal struct BatchRow
    {
        public string file;
        public string slot;
        public float deltaE;      // 앵커-기준색 지각 색차(-1 = 앵커 없음)
        public string action;     // applied / skipped-noanchor / skipped-drift 등
    }

    /// <summary>
    /// 픽셀 파이프라인 — 마스크(원본 기준 판정)·델타 가중 합성·앵커 검출·경계 재구성.
    /// ★겹침 규칙: 모든 슬롯의 w 를 원본 픽셀로 계산하고 델타를 가중 합산(Σw>1 만 정규화) —
    /// 슬롯 순서 무관, 이동된 픽셀의 이중 적용 없음.
    /// ★경계 언믹싱: 픽셀을 슬롯 A↔B 앵커 선분에 투영해 「A색 α + B색 (1-α)」로 분해,
    /// 편집된 끝색으로 재합성. 잔차 억제(A)·혼합비 평활화(B)로 원본 경계 잡음까지 재구성.
    /// </summary>
    internal static class RecolorEngine
    {
        /// <summary>편집/배치 공용 슬롯 런타임 구성. anchors 가 null 이면 앵커=기준색(단일 편집 모드).</summary>
        public static SlotRuntime[] BuildRuntimes(RecolorPreset preset, Vector3?[] anchors = null)
        {
            var list = preset.slots;
            var rt = new SlotRuntime[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                rt[i].s = s;
                rt[i].refLch = RecolorMath.SrgbToLch(s.referenceColor);
                rt[i].active = s.enabled;
                Vector3 anchor = rt[i].refLch;
                if (anchors != null)
                {
                    if (anchors[i].HasValue) anchor = anchors[i].Value;
                    else if (s.absoluteMode) rt[i].active = false; // 절대 모드인데 앵커 검출 실패 → 스킵
                }
                rt[i].anchorLch = anchor;
                if (s.absoluteMode)
                {
                    Vector3 target = RecolorMath.SrgbToLch(s.targetColor);
                    rt[i].deltaAbs = new Vector3(
                        target.x - anchor.x,
                        target.y - anchor.y,
                        RecolorMath.HueDelta(anchor.z, target.z));
                }

                // 경계 언믹싱 끝점(linear RGB): 원색 = 앵커, 편집색 = 앵커에 슬롯 편집 적용
                rt[i].anchorLin = LchToLinearClamped(anchor);
                rt[i].editedLin = rt[i].active
                    ? LchToLinearClamped(ApplyEdit(anchor, s, rt[i].deltaAbs))
                    : rt[i].anchorLin;
            }
            return rt;
        }

        /// <summary>슬롯의 편집(상대 노브 또는 절대 델타)을 임의 LCH 색에 적용.</summary>
        public static Vector3 ApplyEdit(Vector3 lch, RecolorSlot s, Vector3 deltaAbs)
        {
            float dL, dC, dH;
            if (s.absoluteMode) { dL = deltaAbs.x; dC = deltaAbs.y; dH = deltaAbs.z; }
            else
            {
                dL = lch.x * (s.lightScale - 1f) + s.lightOffset;
                dC = lch.y * (s.chromaScale - 1f) + s.chromaOffset;
                dH = s.hueShift;
            }
            return new Vector3(Mathf.Clamp01(lch.x + dL), Mathf.Max(0f, lch.y + dC), lch.z + dH);
        }

        private static Vector3 LchToLinearClamped(Vector3 lch)
        {
            Vector3 lin = RecolorMath.OklabToLinear(RecolorMath.LchToOklab(lch));
            return new Vector3(Mathf.Clamp01(lin.x), Mathf.Clamp01(lin.y), Mathf.Clamp01(lin.z));
        }

        /// <summary>슬롯 마스크 가중치(0~1) — 원본 픽셀 LCH 기준.</summary>
        public static float Weight(in Vector3 lch, RecolorSlot s, in Vector3 refLch)
        {
            float w = RecolorMath.FalloffBelow(RecolorMath.HueDistance(lch.z, refLch.z), s.hueRadius, s.hueFalloff);
            if (w <= 0f) return 0f;
            w *= RecolorMath.GateAbove(lch.y, s.chromaMin, s.chromaSoft);
            if (s.useLightGate)
                w *= RecolorMath.GateWindow(lch.x, s.lightMin, s.lightMax, s.lightSoft);
            return w;
        }

        /// <summary>
        /// 전체 처리. maskSlot >= 0 이면 해당 슬롯의 w 를 maskOut(0~1)에도 기록(마스크 프리뷰용).
        /// 알파는 보존, 완전 투명 픽셀은 스킵. 경계 재구성은 preset.boundary 로 제어(사용 시 2패스).
        /// </summary>
        public static Color32[] Process(Color32[] src, int width, int height,
            SlotRuntime[] rts, RecolorPreset preset, int maskSlot = -1, float[] maskOut = null)
        {
            bool dither = preset.dither;

            // 경계 언믹싱 사전 계산(선분 A→B, linear RGB)
            var bu = preset.boundary;
            bool useBu = bu != null && bu.enabled
                && bu.slotA >= 0 && bu.slotA < rts.Length
                && bu.slotB >= 0 && bu.slotB < rts.Length && bu.slotA != bu.slotB;
            Vector3 buA = default, buAB = default, buE0 = default, buE1 = default;
            float buLen2 = 0f;
            if (useBu)
            {
                buA = rts[bu.slotA].anchorLin;
                buAB = rts[bu.slotB].anchorLin - buA;
                buLen2 = Vector3.Dot(buAB, buAB);
                buE0 = rts[bu.slotA].editedLin;
                buE1 = rts[bu.slotB].editedLin;
                useBu = buLen2 > 1e-6f; // 두 끝점이 같은 색이면 무의미
            }

            var dst = new Color32[src.Length];

            // 경계 재구성용 중간 버퍼(경계 보정 사용 시에만 할당 — float 정밀도 유지가 목적)
            Vector3[] normalLinBuf = useBu ? new Vector3[src.Length] : null;
            float[] tBuf = useBu ? new float[src.Length] : null;
            float[] mBuf = useBu ? new float[src.Length] : null;

            // ── 1차 패스: 일반 색역 편집(+색 조임) + 경계 혼합비·소속도 수집 ──
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    Color32 p = src[idx];
                    if (p.a == 0) { dst[idx] = p; if (maskOut != null) maskOut[idx] = 0f; continue; }

                    var srgb = new Color(p.r / 255f, p.g / 255f, p.b / 255f, 1f);
                    Color srcCol = srgb; // 원본 보존 — 언믹싱 판정은 원본 기준
                    Vector3 lch = RecolorMath.SrgbToLch(srgb);

                    float totalW = 0f;
                    float dL = 0f, dC = 0f, dH = 0f;
                    for (int si = 0; si < rts.Length; si++)
                    {
                        if (!rts[si].active) { if (maskOut != null && si == maskSlot) maskOut[idx] = 0f; continue; }
                        var s = rts[si].s;
                        float w = Weight(lch, s, rts[si].refLch);
                        if (maskOut != null && si == maskSlot) maskOut[idx] = w;
                        if (w <= 0f) continue;

                        float sdL, sdC, sdH;
                        if (s.absoluteMode)
                        {
                            sdL = rts[si].deltaAbs.x; sdC = rts[si].deltaAbs.y; sdH = rts[si].deltaAbs.z;
                        }
                        else
                        {
                            sdL = lch.x * (s.lightScale - 1f) + s.lightOffset;
                            sdC = lch.y * (s.chromaScale - 1f) + s.chromaOffset;
                            sdH = s.hueShift;
                        }
                        // ★색 조임(C): 색상 산포를 앵커 쪽으로 당김(채도는 절반 강도) — AI 잡색 정리
                        if (s.tighten > 0f)
                        {
                            sdH += RecolorMath.HueDelta(lch.z, rts[si].anchorLch.z) * s.tighten;
                            sdC += (rts[si].anchorLch.y - lch.y) * s.tighten * 0.5f;
                        }
                        dL += w * sdL; dC += w * sdC; dH += w * sdH;
                        totalW += w;
                    }

                    if (totalW > 1f) { float inv = 1f / totalW; dL *= inv; dC *= inv; dH *= inv; }
                    if (totalW > 0f)
                    {
                        lch.x = Mathf.Clamp01(lch.x + dL);
                        lch.y = Mathf.Max(0f, lch.y + dC);
                        lch.z += dH;
                        srgb = RecolorMath.LchToSrgb(lch);
                    }

                    if (useBu)
                    {
                        normalLinBuf[idx] = new Vector3(
                            RecolorMath.SrgbToLinear(srgb.r),
                            RecolorMath.SrgbToLinear(srgb.g),
                            RecolorMath.SrgbToLinear(srgb.b));
                        Vector3 pLin = new Vector3(
                            RecolorMath.SrgbToLinear(srcCol.r),
                            RecolorMath.SrgbToLinear(srcCol.g),
                            RecolorMath.SrgbToLinear(srcCol.b));
                        float t = Vector3.Dot(pLin - buA, buAB) / buLen2;
                        // 양 끝점 근처는 일반 경로가 담당(테이퍼) — 순수 색역은 지각 공간 편집이 더 정확
                        float taper = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f))
                                    * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.12f));
                        float m = 0f;
                        if (taper > 0f)
                        {
                            float dist = Vector3.Distance(pLin, buA + t * buAB); // 선분까지 수직 거리
                            m = RecolorMath.FalloffBelow(dist, bu.distRadius, bu.distFalloff) * taper;
                        }
                        tBuf[idx] = t;
                        mBuf[idx] = m;
                        continue; // 합성·양자화는 2차 패스에서
                    }

                    dst[idx] = Quantize(srgb, p.a, x, y, dither);
                }
            }

            if (!useBu) return dst;

            // ── 경계 재구성(B): 혼합비 t 를 m 가중 평활화 — 뭉개진 전이를 이상적 램프로 다시 그린다 ──
            float[] tSmooth = tBuf;
            if (bu.smoothRadius > 0)
                tSmooth = SmoothWeighted(tBuf, mBuf, width, height, bu.smoothRadius);

            float keepResidual = 1f - Mathf.Clamp01(bu.residualSuppress);

            // ── 2차 패스: 언믹싱 재합성 + 일반 결과와 블렌드 + 양자화 ──
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    Color32 p = src[idx];
                    if (p.a == 0) continue; // 1차 패스에서 원본 그대로 기록됨

                    Vector3 outLin = normalLinBuf[idx];
                    float m = mBuf[idx];
                    if (m > 0f)
                    {
                        Vector3 pLin = new Vector3(
                            RecolorMath.SrgbToLinear(p.r / 255f),
                            RecolorMath.SrgbToLinear(p.g / 255f),
                            RecolorMath.SrgbToLinear(p.b / 255f));
                        float tc = Mathf.Clamp01(tBuf[idx]);
                        // 잔차 = 이상적인 두 색 혼합선에서 벗어난 성분(원본 잡음 포함) — 억제(A) 노브로 감쇠
                        Vector3 residual = (pLin - (buA + tc * buAB)) * keepResidual;
                        float tS = Mathf.Clamp01(tSmooth[idx]);
                        Vector3 unmix = Vector3.LerpUnclamped(buE0, buE1, tS) + residual;
                        unmix = new Vector3(Mathf.Clamp01(unmix.x), Mathf.Clamp01(unmix.y), Mathf.Clamp01(unmix.z));
                        outLin = Vector3.Lerp(outLin, unmix, m);
                    }
                    var srgb = new Color(
                        RecolorMath.LinearToSrgb(outLin.x),
                        RecolorMath.LinearToSrgb(outLin.y),
                        RecolorMath.LinearToSrgb(outLin.z), 1f);
                    dst[idx] = Quantize(srgb, p.a, x, y, dither);
                }
            }
            return dst;
        }

        private static Color32 Quantize(Color srgb, byte alpha, int x, int y, bool dither)
        {
            float d = dither ? RecolorMath.Dither(x, y) : 0f;
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((srgb.r + d) * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((srgb.g + d) * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((srgb.b + d) * 255f), 0, 255),
                alpha);
        }

        /// <summary>
        /// m 가중 분리형 박스 블러 — 혼합비 필드를 소속도(m) 가중으로 평활화.
        /// 밴드 밖(m≈0)의 t 값이 밴드 안을 오염시키지 않는다.
        /// </summary>
        private static float[] SmoothWeighted(float[] t, float[] m, int width, int height, int radius)
        {
            var num = new float[t.Length];
            var den = new float[t.Length];
            for (int i = 0; i < t.Length; i++) { num[i] = t[i] * m[i]; den[i] = m[i]; }
            BoxBlur(num, width, height, radius);
            BoxBlur(den, width, height, radius);
            var outT = new float[t.Length];
            for (int i = 0; i < t.Length; i++)
                outT[i] = den[i] > 1e-5f ? num[i] / den[i] : t[i];
            return outT;
        }

        private static void BoxBlur(float[] a, int width, int height, int radius)
        {
            var tmp = new float[a.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f; int n = 0;
                    int x0 = Mathf.Max(0, x - radius), x1 = Mathf.Min(width - 1, x + radius);
                    for (int xx = x0; xx <= x1; xx++) { sum += a[row + xx]; n++; }
                    tmp[row + x] = sum / n;
                }
            }
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float sum = 0f; int n = 0;
                    int y0 = Mathf.Max(0, y - radius), y1 = Mathf.Min(height - 1, y + radius);
                    for (int yy = y0; yy <= y1; yy++) { sum += tmp[yy * width + x]; n++; }
                    a[y * width + x] = sum / n;
                }
            }
        }

        /// <summary>
        /// 배치용 앵커 자동 검출 — 기준색 주변 넓은 창(anchorWindow) 안 픽셀의 대표색.
        /// L·C = 가중 중앙값(이상치 강건), H = 가중 원형 평균(순환 처리). 픽셀이 없으면 null.
        /// </summary>
        public static Vector3? DetectAnchor(Color32[] src, RecolorSlot s, Vector3 refLch)
        {
            var ls = new List<(float v, float w)>(256);
            var cs = new List<(float v, float w)>(256);
            float hx = 0f, hy = 0f; float total = 0f;

            for (int i = 0; i < src.Length; i++)
            {
                Color32 p = src[i];
                if (p.a < 32) continue; // 투명·경계 잔털은 통계에서 제외
                var srgb = new Color(p.r / 255f, p.g / 255f, p.b / 255f, 1f);
                Vector3 lch = RecolorMath.SrgbToLch(srgb);

                float w = RecolorMath.FalloffBelow(RecolorMath.HueDistance(lch.z, refLch.z), s.anchorWindow, 10f)
                        * RecolorMath.GateAbove(lch.y, s.chromaMin, s.chromaSoft);
                if (w <= 0.01f) continue;

                ls.Add((lch.x, w)); cs.Add((lch.y, w));
                float rad = lch.z * Mathf.Deg2Rad;
                hx += w * Mathf.Cos(rad); hy += w * Mathf.Sin(rad);
                total += w;
            }

            if (total < 4f) return null; // 표본 부족 = 이 이미지에 해당 색역 없음

            float hue = Mathf.Atan2(hy, hx) * Mathf.Rad2Deg;
            return new Vector3(WeightedMedian(ls), WeightedMedian(cs), hue);
        }

        private static float WeightedMedian(List<(float v, float w)> items)
        {
            items.Sort((a, b) => a.v.CompareTo(b.v));
            float total = 0f;
            foreach (var it in items) total += it.w;
            float acc = 0f;
            foreach (var it in items)
            {
                acc += it.w;
                if (acc >= total * 0.5f) return it.v;
            }
            return items.Count > 0 ? items[items.Count - 1].v : 0f;
        }
    }
}
