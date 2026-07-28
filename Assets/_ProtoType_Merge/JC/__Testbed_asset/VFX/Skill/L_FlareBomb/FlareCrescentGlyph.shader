// 루미나 「플레어 봄」 — 초승달 글리프 랜덤 생성기 리그 (2단계).
// 셀마다 시드로 글리프를 '추첨'한다. 인스펙터는 모양이 아니라 분포(변화 벡터)만 제어.
//
// [1차 커터 — (s,d) 구성적 샘플링]
//   커터는 원본 중심 O를 내부에 포함(→ O–C 축 대칭, 2자유도 패밀리로 완전 표현).
//   s = O–C 축 위 커터 호의 교차점 거리(초승달 쪽) ∈ [SZoneMin, SZoneMax]·R (1/3 규칙)
//   d = 중심거리 ∈ (max(ε,(R−s)/2), DMax·R],  r = s + d
//   → 중심 포함·2점 교차·1/3 영역이 전부 자동 성립(기각 없음). 두께 ≥ R−s 보장.
//
// [2차 커터 — 2아형, 확률 택일. 결과는 항상 1조각(재합류·양분 없음)]
//   B 팁 파괴형: 첨점(원본∩커터1) 근방에 중심을 두고 첨점 포함을 강제 → 끝이 부러진 낫.
//   A 노치형: 바깥 호의 남은 구간에서 현(호 2점)+사지타로 커터를 역산, 깊이 ≤ κ×국소 두께
//             → 반대쪽 경계에 닿지 않는 이빨자국.
Shader "Testbed/FlareBomb/FlareCrescentGlyph"
{
    Properties
    {
        [Header(Grid Rig)]
        _GridCols ("Grid Columns", Range(1,8)) = 5
        _GridRows ("Grid Rows", Range(1,8)) = 4
        _Seed ("Reroll Seed", Range(0,100)) = 0
        _RandRotate ("Random Rotation (0=fixed)", Range(0,1)) = 1
        _Rotate ("Fixed Rotation (deg)", Range(0,360)) = 0

        [Header(First Cutter Distribution)]
        _RadiusA ("Body Radius (cell units)", Range(0.1,0.45)) = 0.30
        _SZoneMin ("Crossing Zone Min (x R, 1over3 rule)", Range(0,0.333)) = 0.05
        _SZoneMax ("Crossing Zone Max (x R)", Range(0,0.333)) = 0.333
        _DMaxFrac ("Cutter Center Dist Max (x R)", Range(0.3,3)) = 1.5

        [Header(Second Cutter Distribution)]
        _Bite2Prob ("TipBreak Probability", Range(0,1)) = 0.75
        _TipR2Min ("TipBreak Radius Min (x R)", Range(0.1,1)) = 0.25
        _TipR2Max ("TipBreak Radius Max (x R)", Range(0.1,1)) = 0.7

        [Header(Glyph Stretch)]
        _StretchMax ("Stretch Max (1=off, area preserving)", Range(1,3.5)) = 2.0

        [Header(Inner Arc Notch)]
        _InNotchProb ("Inner Notch Probability", Range(0,1)) = 0.6
        _InNotchSpanMin ("Inner Notch Half Span Min (deg)", Range(4,50)) = 10
        _InNotchSpanMax ("Inner Notch Half Span Max (deg)", Range(4,70)) = 32
        _InNotchDepthK ("Inner Notch Depth Coeff (x thickness)", Range(0.1,0.85)) = 0.7

        [Header(SDF Band Shading)]
        [HDR] _FillColor ("Fill (dark inner)", Color) = (0.16, 0.05, 0.22, 1)
        [HDR] _EdgeColor ("Edge Band (bright)", Color) = (2.2, 1.2, 3.2, 1)
        _EdgeWidth ("Edge Band Width", Range(0.002,0.1)) = 0.03
        _EdgeSoft ("Edge AA Softness", Range(0.001,0.05)) = 0.006
        _GlowRange ("Outer Glow Range", Range(0,0.2)) = 0.06
        _GlowStrength ("Outer Glow Strength", Range(0,1)) = 0.35
        _Emission ("Emission", Range(0,4)) = 1.0
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareCrescentGlyph"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define DEG2RAD 0.01745329
            #define TWO_PI 6.2831853

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half _GridCols,_GridRows,_Seed,_RandRotate,_Rotate;
                half _RadiusA,_SZoneMin,_SZoneMax,_DMaxFrac;
                half _Bite2Prob,_TipR2Min,_TipR2Max;
                half _StretchMax;
                half _InNotchProb,_InNotchSpanMin,_InNotchSpanMax,_InNotchDepthK;
                half4 _FillColor,_EdgeColor;
                half _EdgeWidth,_EdgeSoft,_GlowRange,_GlowStrength,_Emission,_Opacity;
            CBUFFER_END

            float shash2(float a, float b){ return frac(sin(a * 127.1 + b * 269.5 + 74.7) * 43758.5453); }

            Varyings vert(Attributes IN){
                Varyings OUT;
                float3 centerWS = TransformObjectToWorld(float3(0,0,0));
                float3 fwd = normalize(centerWS - GetCameraPositionWS());
                float3 upRef = abs(fwd.y) > 0.99 ? float3(0,0,1) : float3(0,1,0);
                float3 right = normalize(cross(upRef, fwd));
                float3 up = cross(fwd, right);
                float sx = length(unity_ObjectToWorld._m00_m10_m20);
                float sy = length(unity_ObjectToWorld._m01_m11_m21);
                float3 posWS = centerWS
                             + right * IN.positionOS.x * sx
                             + up    * IN.positionOS.y * sy;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            float2 rot2(float2 p, float aRad)
            {
                float c = cos(aRad), s = sin(aRad);
                return float2(c*p.x - s*p.y, s*p.x + c*p.y);
            }

            // 시드로 글리프를 추첨하고 p에서의 SDF를 평가.
            // 반환: x = 글리프 SDF(정준 공간), y = 몸통 원 SDF(글로우 게이트용)
            float2 glyphSdf(float2 p, float2 seed)
            {
                float h1 = shash2(seed.x, seed.y + _Seed);
                float h2 = shash2(seed.x + 11.3, seed.y + _Seed);
                float h3 = shash2(seed.x + 23.7, seed.y + _Seed);
                float h4 = shash2(seed.x + 37.1, seed.y + _Seed);
                float h5 = shash2(seed.x + 51.9, seed.y + _Seed);
                float h6 = shash2(seed.x + 67.3, seed.y + _Seed);
                float h7 = shash2(seed.x + 83.7, seed.y + _Seed);
                float h8 = shash2(seed.x + 97.1, seed.y + _Seed);
                float h9 = shash2(seed.x + 113.9, seed.y + _Seed);
                float h10 = shash2(seed.x + 131.3, seed.y + _Seed);
                float h11 = shash2(seed.x + 149.7, seed.y + _Seed);
                float h12 = shash2(seed.x + 167.1, seed.y + _Seed);
                float h13 = shash2(seed.x + 181.9, seed.y + _Seed);

                // 랜덤/고정 회전
                float ang = (_RandRotate > 0.5) ? h9 * TWO_PI : _Rotate * DEG2RAD;
                p = rot2(p, ang);

                // ---- 글리프 스트레치(면적 보존 아핀): 몸통·커터가 함께 타원화 ----
                // 정준 공간(원 기하)에서 모든 수식·보장이 성립하고, 변형은 평가 좌표에만 적용.
                float e = sqrt(lerp(1.0, _StretchMax, h10));   // 축 방향 ×e, 직교 ×1/e
                float eAng = h11 * TWO_PI;
                p = rot2(p, eAng);
                p = float2(p.x / e, p.y * e);
                p = rot2(p, -eAng);

                float R = _RadiusA / e;   // 셀 맞춤: 신장돼도 최대 치수가 _RadiusA를 유지

                // ---- 1차 커터: (s, d) 구성적 샘플링 ----
                float s = lerp(_SZoneMin, min(_SZoneMax, 0.3333), h1) * R;
                // ★하한 여유 계수 1.3: d=(R−s)/2는 정확히 내접 접선(첨점 퇴화=링화)이라
                // 그 근방 샘플이 고리형 NG를 만든다 — 260724 수정
                float dLo = max(0.02 * R, (R - s) * 0.65);
                float d = lerp(dLo, max(dLo + 0.001, _DMaxFrac * R), h2);
                float r = s + d;
                float2 C = float2(d, 0.0);

                float sdf = max(length(p) - R, -(length(p - C) - r));

                // 첨점(원본∩커터1) — 항상 2점 존재(구성 보장)
                float cx = (d * d + R * R - r * r) / (2.0 * d);
                float cy = sqrt(max(R * R - cx * cx, 0.0));

                // ---- 2차 커터 (확률): 팁 파괴형 단일 ----
                // (노치형은 바깥 호 중간을 파먹어 보타이 실루엣이 나와 제외 — 260724 확정)
                if (h3 < _Bite2Prob)
                {
                    // 첨점 포함 강제 → 항상 1조각(끝만 잘림).
                    // 중심을 첨점에서 바깥(몸통 중심 반대) 방향으로 치우쳐,
                    // 접선성 절단으로 "닫힌 구멍"처럼 보이는 케이스를 방지.
                    float2 cusp = float2(cx, (h5 < 0.5) ? cy : -cy);
                    float r2 = lerp(_TipR2Min, _TipR2Max, h6) * R;
                    // 절단을 한쪽 팁에 국소화: 첨점 간 거리의 절반 미만으로 캡
                    // (과대 반경은 팔 통째 제거+반대편 실낱 부스러기의 원인)
                    r2 = clamp(r2, 0.12 * R, max(0.9 * cy, 0.12 * R));
                    float2 outw = normalize(cusp + float2(1e-4, 0));
                    float2 side = float2(-outw.y, outw.x) * (h8 - 0.5);
                    float offM = lerp(0.15, 0.55, h7) * r2;  // 첨점 포함 보장(offset < r2)
                    float2 c2 = cusp + offM * normalize(outw + side);
                    sdf = max(sdf, -(length(p - c2) - r2));
                }

                // ---- 내측 호 노치 (확률): 오목 경계를 국소로 더 파서 변곡을 만든다 ----
                // 커터1 호의 몸통 내부 구간에서 현+사지타로 역산. 바깥(볼록) 호에는 절대
                // 닿지 않게 깊이를 국소 두께로 캡 → 보타이 불가·1조각 보장.
                if (h4 < _InNotchProb)
                {
                    float psiC = atan2(cy, cx - d);                  // 상단 첨점의 C 기준 각
                    // ★스팬을 '몸통 단위 호 길이'로 정규화: 커터 반경 r에 무관하게
                    // 노치의 실제 크기가 일정해진다(각도 그대로 쓰면 r 큰 커터에서
                    // 현 길이가 폭주해 링 껍데기/부스러기 NG 발생 — 260724 수정).
                    float arcHalf = lerp(_InNotchSpanMin, _InNotchSpanMax, h12) * DEG2RAD * R;
                    float halfSpan = arcHalf / r;
                    float margin = halfSpan + 0.18 * R / r;
                    float lo = psiC + margin, hi = TWO_PI - psiC - margin;
                    if (hi > lo)
                    {
                        float psiN = lerp(lo, hi, h13);
                        float2 v = float2(cos(psiN), sin(psiN));
                        float2 A = C + r * v;                        // 커터1 호 위 기준점
                        // A에서 v(바깥) 방향으로 몸통 원까지의 두께
                        float bq = dot(A, v);
                        float t = -bq + sqrt(max(bq * bq - (dot(A, A) - R * R), 0.0));
                        float chordDrop = r * (1.0 - cos(halfSpan));
                        float hHalf = r * sin(halfSpan);
                        float gMax = _InNotchDepthK * max(t, 0.0) + chordDrop;
                        float g = clamp(gMax * lerp(0.45, 1.0, h7 + h13 - floor(h7 + h13)),
                                        0.18 * hHalf, max(gMax, 0.18 * hHalf));
                        float r2 = (hHalf * hHalf + g * g) / (2.0 * g);
                        float2 M = C + r * cos(halfSpan) * v;
                        float2 c2 = M - (r2 - g) * v;                // 디스크가 바깥쪽으로 부풀도록
                        sdf = max(sdf, -(length(p - c2) - r2));
                    }
                }
                return float2(sdf, length(p) - R);
            }

            half4 frag(Varyings IN):SV_Target{
                float2 g = IN.uv * float2(_GridCols, _GridRows);
                float2 cellId = floor(g);
                float2 p = frac(g) - 0.5;

                float2 res = glyphSdf(p, cellId * 7.77 + 3.1);
                // 셀 경계 도함수 노이즈 가드: 글리프에서 충분히 먼 픽셀은 즉시 제외
                if (res.x > _RadiusA * 0.6) discard;
                // 스트레치 왜곡 보정: 화면 도함수로 실거리 근사(외곽선 폭 균일화)
                float gradS = length(float2(ddx(res.x), ddy(res.x)));
                float gradP = length(float2(ddx(p.x), ddy(p.x)));
                float sdf = res.x * gradP / max(gradS, 1e-5);

                float inside = smoothstep(_EdgeSoft, -_EdgeSoft, sdf);
                float edgeBand = smoothstep(_EdgeSoft, -_EdgeSoft, abs(sdf + _EdgeWidth * 0.5) - _EdgeWidth * 0.5);
                // 글로우는 몸통 원 바깥(볼록면)만: 오목면·베어문 자리까지 두르면
                // 커터 원판 내부가 "어두운 구멍"으로 드러나는 음영 아티팩트가 생긴다
                float outsideBody = step(0.0, res.y + 0.02 * _RadiusA);
                float glow = (_GlowRange > 0.001)
                    ? exp(-max(sdf, 0.0) / _GlowRange * 3.0) * _GlowStrength * step(0.0, sdf) * outsideBody
                    : 0.0;

                half3 col = lerp(_FillColor.rgb, _EdgeColor.rgb, edgeBand);
                half a = saturate(max(inside, glow)) * _Opacity;
                if (a < 0.01) discard;
                if (inside < 0.01) col = _EdgeColor.rgb;
                return half4(col * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
