// JC PawForYou VFX — 고양이 발 빌보드: SDF 절차 셰이더.
// 실루엣 = 손바닥 타원 + 발가락 원 N개의 smooth-min 합성. 내부 젤리 패드 프린트(큰 패드+발가락 패드)는 별도 SDF로 발광.
// 레이어 = 반투명 필(내부 그라데이션) + 림(경계 밴드) + 외곽 글로우(외부 감쇠) + 패드 발광.
// Blend One OneMinusSrcAlpha(프리멀티): 필 부분은 배경을 가리고, 림/글로우는 가산처럼 얹힘.
// 빌보드 회전은 스크립트(PawSprite)가 처리. 텍스처 의존 없음.
Shader "JC/VFX/PawSprite"
{
    Properties
    {
        [Header(Canvas)]
        _CanvasScale ("Canvas Scale", Range(0.2,1)) = 0.55

        [Header(Shape)]
        _ToeCount ("Toe Count", Range(2,6)) = 4
        _ToeSpread ("Toe Spread Deg", Range(30,160)) = 95
        _ToeDist ("Toe Distance", Range(0.2,0.9)) = 0.52
        _ToeRadius ("Toe Radius", Range(0.05,0.35)) = 0.16
        _PalmRadiusX ("Palm Radius X", Range(0.1,0.8)) = 0.42
        _PalmRadiusY ("Palm Radius Y", Range(0.1,0.8)) = 0.34
        _PalmOffsetY ("Palm Offset Y", Range(-0.6,0.2)) = -0.25
        _Fusion ("Fusion Smin", Range(0.01,0.3)) = 0.08

        [Header(Pad Print)]
        _PadMainRadius ("Pad Main Radius", Range(0,0.4)) = 0.2
        _PadMainSquash ("Pad Main Squash", Range(0.5,1.2)) = 0.8
        _PadToeRadius ("Pad Toe Radius", Range(0,0.2)) = 0.08
        _PadToeDistMul ("Pad Toe Dist Mul", Range(0.3,1)) = 0.62
        [HDR]_PadColor ("Pad Color", Color) = (1, 0.95, 0.8, 1)
        _PadIntensity ("Pad Intensity", Range(0,8)) = 2.0

        [Header(Layers)]
        [HDR]_FillColor ("Fill Color", Color) = (0.85, 0.9, 1.0, 1)
        _FillOpacity ("Fill Opacity", Range(0,1)) = 0.8
        _InnerGrad ("Inner Edge Brighten", Range(0,3)) = 0.8
        [HDR]_RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimIntensity ("Rim Intensity", Range(0,8)) = 2.5
        _RimWidth ("Rim Width", Range(0.005,0.2)) = 0.05
        [HDR]_GlowColor ("Glow Color", Color) = (0.8, 0.9, 1.0, 1)
        _GlowIntensity ("Glow Intensity", Range(0,8)) = 1.5
        _GlowRange ("Glow Range", Range(0.02,0.6)) = 0.25

        [Header(Motion)]
        _WobbleAmp ("Wobble Amp", Range(0,0.1)) = 0.015
        _WobbleSpeed ("Wobble Speed", Range(0,10)) = 2.0
        _FadeMul ("Fade Multiplier", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float _CanvasScale;
                float _ToeCount; float _ToeSpread; float _ToeDist; float _ToeRadius;
                float _PalmRadiusX; float _PalmRadiusY; float _PalmOffsetY; float _Fusion;
                float _PadMainRadius; float _PadMainSquash; float _PadToeRadius; float _PadToeDistMul;
                float4 _PadColor; float _PadIntensity;
                float4 _FillColor; float _FillOpacity; float _InnerGrad;
                float4 _RimColor; float _RimIntensity; float _RimWidth;
                float4 _GlowColor; float _GlowIntensity; float _GlowRange;
                float _WobbleAmp; float _WobbleSpeed; float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 다항 smooth-min: 두 SDF를 부드럽게 융합(발가락이 손바닥에 이어 붙음)
            float smin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / max(k, 1e-4));
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            // 타원 SDF 근사(스케일 공간): 정확하진 않지만 밴드/글로우 용도로 충분
            float sdEllipse(float2 p, float2 c, float rx, float ry)
            {
                float2 q = (p - c) / float2(max(rx, 1e-4), max(ry, 1e-4));
                return (length(q) - 1.0) * min(rx, ry);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 캔버스 여백: 발 형태가 쿼드의 _CanvasScale 비율만 차지 → 글로우가 경계 전에 감쇠할 공간 확보
                float2 p = (IN.uv * 2.0 - 1.0) / max(_CanvasScale, 0.2);
                // 저주파 일렁임(도메인 워블) — 유령 장갑의 흐물거림
                p += _WobbleAmp * float2(
                    sin(_Time.y * _WobbleSpeed + p.y * 5.3),
                    cos(_Time.y * _WobbleSpeed * 0.83 + p.x * 4.7));

                float2 palmC = float2(0.0, _PalmOffsetY);

                // ── 실루엣 SDF: 손바닥 타원 + 발가락 원들 smooth-min ──
                float d = sdEllipse(p, palmC, _PalmRadiusX, _PalmRadiusY);
                int n = (int)round(_ToeCount);
                float dpad = 1e5;   // 패드 프린트 SDF(발가락 패드는 실루엣 루프에서 같이 계산)
                for (int i = 0; i < 6; i++)
                {
                    if (i >= n) break;
                    float t = n > 1 ? (float)i / (n - 1) : 0.5;
                    float a = radians(90.0 + _ToeSpread * (t - 0.5));
                    float2 dir = float2(cos(a), sin(a));
                    float2 c = palmC + dir * _ToeDist;
                    d = smin(d, length(p - c) - _ToeRadius, _Fusion);
                    float2 cp = palmC + dir * (_ToeDist * _PadToeDistMul);
                    dpad = min(dpad, length(p - cp) - _PadToeRadius);
                }

                // ── 패드 프린트: 큰 패드(살짝 눌린 타원) + 발가락 패드들 ──
                float dMain = sdEllipse(p, palmC + float2(0.0, -0.02), _PadMainRadius, _PadMainRadius * _PadMainSquash);
                dpad = min(dpad, dMain);
                float padMask = 1.0 - smoothstep(0.0, 0.03, dpad);

                // ── 레이어 합성 ──
                float fillMask = 1.0 - smoothstep(-0.01, 0.01, d);
                float edgeProx = saturate(1.0 + d / 0.3);                      // 경계 근접도(내부): 경계 1 → 깊은 안쪽 0
                float inner = 1.0 + _InnerGrad * edgeProx;
                float rim = 1.0 - smoothstep(_RimWidth * 0.5, _RimWidth, abs(d));
                float glow = exp(-max(d, 0.0) / max(_GlowRange, 1e-3)) * (1.0 - fillMask);

                float3 col = _FillColor.rgb * _FillOpacity * inner * fillMask
                           + _PadColor.rgb * _PadIntensity * padMask * fillMask
                           + _RimColor.rgb * _RimIntensity * rim
                           + _GlowColor.rgb * _GlowIntensity * glow;
                float alpha = saturate(fillMask * _FillOpacity);

                // 쿼드 경계 강제 소멸: exp 글로우는 0에 도달하지 않으므로 가장자리에서 반드시 죽임
                float2 b = abs(IN.uv * 2.0 - 1.0);
                float borderKill = 1.0 - smoothstep(0.82, 0.99, max(b.x, b.y));

                col *= _FadeMul * borderKill;
                alpha *= _FadeMul * borderKill;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
