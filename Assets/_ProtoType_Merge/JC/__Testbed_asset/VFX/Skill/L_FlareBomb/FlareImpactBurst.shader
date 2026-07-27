// 루미나 「플레어 봄」 탄착 스타버스트 — 시선축 빌보드 쿼드 위 방사 스파이크.
// 참조: 백열 중심 → 크림 → 골드 방사 스파이크(대/소 2계층) + 센터 플래시.
// - _Progress(0..1)를 C#(FlareBombImpact)이 MPB로 구동하는 원샷 이펙트.
//   성장 구간(_GrowFrac)에 easeOutCubic으로 뻗고, _FadeStart부터 전체 페이드.
// - 스파이크는 밑동이 넓고 끝이 예리한 삼각 프로파일(pow(1-t, sharp)).
//   가로/세로 확장은 쿼드 스케일로 제어(지면 버스트=가로로 넓게, 수직 스파이크=세로로 길게).
// - 가산 블렌드, 큐 3005(오브·레인 위 최상단).
Shader "Testbed/FlareBomb/FlareImpactBurst"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorHot  ("Hot Core Color",  Color) = (1.7, 1.6, 1.4, 1)
        [HDR] _ColorGold ("Outer Gold Color", Color) = (1.2, 0.72, 0.22, 1)
        _Emission ("Emission", Range(0,6)) = 1.5
        _HotCore ("Hot Core Extent (spike-local)", Range(0,1)) = 0.4

        [Header(Progress)]
        _Progress ("Progress (C# driven)", Range(0,1)) = 0.35
        _GrowFrac ("Grow Fraction (of progress)", Range(0.05,1)) = 0.35
        _FadeStart ("Fade Start (progress)", Range(0,1)) = 0.5

        [Header(Main Spikes)]
        _SpikeCount ("Spike Count", Range(4,48)) = 22
        _SpikeLen ("Spike Max Length (uv)", Range(0.1,1)) = 0.9
        _LenJitter ("Length Jitter", Range(0,1)) = 0.55
        _SpikeWidth ("Spike Base Half Width (uv)", Range(0.005,0.2)) = 0.05
        _WidthJitter ("Width Jitter", Range(0,1)) = 0.5
        _TaperSharp ("Tip Taper Sharpness", Range(0.5,6)) = 2.2

        [Header(Sub Spikes)]
        _SubCount ("Sub Spike Count", Range(0,96)) = 44
        _SubLen ("Sub Spike Max Length (uv)", Range(0.05,1)) = 0.45
        _SubWidth ("Sub Spike Base Half Width (uv)", Range(0.003,0.15)) = 0.025

        [Header(Randomness)]
        _PosJitter ("Angular Position Jitter (cell)", Range(0,1)) = 0.7
        _Seed ("Random Seed", Range(0,64)) = 0

        [Header(Center Flash)]
        _FlashSize ("Flash Radius (uv)", Range(0.02,0.8)) = 0.24
        _FlashIntensity ("Flash Intensity", Range(0,6)) = 2.2
        _FlashFade ("Flash Fade End (progress)", Range(0.05,1)) = 0.45

        [Header(Global)]
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareImpactBurst"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TAU 6.2831853

            float ihash(float a, float b){ return frac(sin(a * 127.1 + b * 269.5 + 74.7) * 43758.5453); }

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorHot,_ColorGold;
                half _Emission,_HotCore;
                half _Progress,_GrowFrac,_FadeStart;
                half _SpikeCount,_SpikeLen,_LenJitter,_SpikeWidth,_WidthJitter,_TaperSharp;
                half _SubCount,_SubLen,_SubWidth;
                half _PosJitter,_Seed;
                half _FlashSize,_FlashIntensity,_FlashFade;
                half _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                // 시선축 정렬 빌보드(FlareAuraSprite와 동일)
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

            // 한 스파이크 계층 평가. ang01 = 0..1 각도, r = 중심 거리, grow = 성장 배율.
            // 반환: 스파이크 마스크(0..1)와 스파이크-로컬 진행도(tt, 색 그라데이션용).
            void EvalSpikes(float ang01, float r, float grow, float count, float lenBase, float widthBase,
                            float seedOff, inout float best, inout float bestTT)
            {
                if (count < 0.5) return;
                float a = ang01 * count;
                float id = floor(a);
                float cellAng = TAU / count;
                [unroll]
                for (int o = -1; o <= 1; o++)
                {
                    float lid = id + o;
                    float lidW = lid - count * floor(lid / count);   // 각도 랩
                    float h1 = ihash(lidW + seedOff, _Seed);
                    float h2 = ihash(lidW + seedOff + 17.3, _Seed);
                    float h3 = ihash(lidW + seedOff + 41.9, _Seed);
                    float L = lenBase * (1.0 - _LenJitter * h1) * grow;
                    if (L < 0.005) continue;
                    float tt = r / L;
                    if (tt >= 1.0) continue;
                    float center = 0.5 + (h2 - 0.5) * _PosJitter;
                    float lat = (a - lid - center) * cellAng * max(r, 0.02);   // 물리 가로 거리
                    float widthJ = 1.0 + (h3 - 0.5) * _WidthJitter * 2.0;
                    float hw = widthBase * widthJ * pow(saturate(1.0 - tt), _TaperSharp);
                    float soft = max(hw * 0.55, 0.002);
                    float s = smoothstep(hw, hw - soft, abs(lat));
                    if (s > best) { best = s; bestTT = tt; }
                }
            }

            half4 frag(Varyings IN):SV_Target{
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float ang01 = atan2(p.x, -p.y) / TAU + 0.5;

                // 성장(easeOutCubic) + 전체 페이드
                float g = saturate(_Progress / max(_GrowFrac, 0.05));
                float grow = 1.0 - pow(1.0 - g, 3.0);
                float fade = 1.0 - smoothstep(_FadeStart, 1.0, _Progress);

                float best = 0.0, bestTT = 0.0;
                EvalSpikes(ang01, r, grow, _SpikeCount, _SpikeLen, _SpikeWidth, 0.0, best, bestTT);
                EvalSpikes(ang01, r, grow, _SubCount, _SubLen, _SubWidth, 101.7, best, bestTT);

                // 색: 스파이크-로컬 밑동은 백열, 끝은 골드
                float hotMask = 1.0 - smoothstep(_HotCore * 0.5, _HotCore, bestTT);
                half3 col = lerp(_ColorGold.rgb, _ColorHot.rgb, hotMask);
                float a = best * fade;

                // 센터 플래시(초반 급감쇠 가우시안)
                float flashEnv = 1.0 - smoothstep(0.0, _FlashFade, _Progress);
                float flash = _FlashIntensity * exp(-(r * r) / max(_FlashSize * _FlashSize, 0.0004)) * flashEnv;
                col = col * a + _ColorHot.rgb * flash;
                a = saturate(a + flash);

                a *= _Opacity;
                if (a < 0.005) discard;
                return half4(col * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
