// 루미나 「체인 라이팅」 감전 잔류 아우라 — 시선축 빌보드 쿼드.
// 몸 주변 타원 대역에서 짧은 아크 조각들이 플리커 리롤(_Seed)로 명멸한다.
// 머즐 구체(작은 번개 구체)도 반경·_CoreGlow만 달리해 같은 셰이더 재사용.
// C#(LightningShock/ChainLightningVfx)이 _Seed·_Opacity를 구동. 가산 블렌드, 큐 3005.
Shader "Testbed/ChainLightning/ShockAura"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorCore ("Arc Core Color", Color) = (1.6, 1.5, 1.9, 1)
        [HDR] _ColorGlow ("Glow Color", Color) = (0.65, 0.42, 1.6, 1)
        _Emission ("Emission", Range(0,6)) = 1.5

        [Header(Driven by CSharp)]
        _Seed ("Flicker Seed", Range(0,512)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1

        [Header(Ring)]
        _Radius ("Ring Radius (uv)", Range(0.1,0.9)) = 0.5
        _EllipseK ("Vertical Ratio (1=circle)", Range(0.5,2)) = 1.25
        _RadJitter ("Per-arc Radius Jitter", Range(0,0.6)) = 0.25

        [Header(Arcs)]
        _ArcCount ("Arc Sectors", Range(4,24)) = 11
        _Density ("Arc Density (visible ratio)", Range(0,1)) = 0.65
        _ArcWidth ("Arc Half Width (uv)", Range(0.005,0.15)) = 0.035
        _HighlightRatio ("Arc Highlight Width Ratio", Range(0,1)) = 0.45
        _WobbleAmp ("Arc Wobble Amplitude (uv)", Range(0,0.25)) = 0.07
        _WobbleFreq ("Arc Wobble Segments", Range(4,48)) = 22

        [Header(Inner Glow)]
        _CoreGlow ("Inner Glow Amount", Range(0,3)) = 0.35

        [Header(Global)]
        _GlowAmt ("Arc Glow Amount", Range(0,2)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "ShockAura"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TAU 6.2831853

            float shash2c(float a, float b){ return frac(sin(a * 127.1 + b * 269.5 + 74.7) * 43758.5453); }

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorCore,_ColorGlow;
                half _Emission;
                half _Seed,_Opacity;
                half _Radius,_EllipseK,_RadJitter;
                half _ArcCount,_Density,_ArcWidth,_HighlightRatio,_WobbleAmp,_WobbleFreq;
                half _CoreGlow,_GlowAmt;
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

            half4 frag(Varyings IN):SV_Target{
                float2 p = IN.uv * 2.0 - 1.0;
                float2 q = float2(p.x, p.y / max(_EllipseK, 0.1));
                float r = length(q);
                float ang01 = atan2(q.x, -q.y) / TAU + 0.5;

                // 아크 섹터 + 이웃 평가
                float a = ang01 * _ArcCount;
                float id = floor(a);
                float bestBody = 0.0, bestCore = 0.0;
                [unroll]
                for (int o = -1; o <= 1; o++)
                {
                    float lid = id + o;
                    float lidW = lid - _ArcCount * floor(lid / _ArcCount);   // 각도 랩
                    float h = shash2c(lidW, _Seed);
                    if (h > _Density) continue;
                    float rc = _Radius * (1.0 + (shash2c(lidW + 7.7, _Seed) - 0.5) * 2.0 * _RadJitter);
                    float span = 0.32 + 0.4 * shash2c(lidW + 13.1, _Seed);   // 셀 단위 반스팬
                    float angLoc = a - lid - 0.5;
                    if (abs(angLoc) > span) continue;
                    float endFade = smoothstep(span, span * 0.45, abs(angLoc));
                    // 각진 워블(세그먼트 해시 선형 연결 — 볼트와 동일 기법)
                    float k = ang01 * _WobbleFreq;
                    float ks = floor(k); float kf = frac(k);
                    float wob = lerp(shash2c(ks + lidW * 3.7, _Seed + 91.0),
                                     shash2c(ks + 1.0 + lidW * 3.7, _Seed + 91.0), kf);
                    wob = (wob - 0.5) * 2.0 * _WobbleAmp;
                    float d = abs(r - (rc + wob));
                    // 2톤: 색 몸통(넓게) + 백열 하이라이트(좁게) — 입체감
                    float body = smoothstep(_ArcWidth, _ArcWidth * 0.35, d) * endFade;
                    float hw = _ArcWidth * _HighlightRatio;
                    float core = smoothstep(hw, hw * 0.3, d) * endFade;
                    bestBody = max(bestBody, body);
                    bestCore = max(bestCore, core);
                }

                float innerGlow = _CoreGlow * exp(-(r * r) / max(_Radius * _Radius * 0.4, 1e-4));
                float glow = bestBody * _GlowAmt + innerGlow;

                float aOut = saturate(bestBody + glow * 0.6) * _Opacity;
                half3 col = (_ColorGlow.rgb * (bestBody + glow) + _ColorCore.rgb * bestCore) * _Emission;
                if (aOut < 0.004) discard;
                return half4(col, aOut);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
