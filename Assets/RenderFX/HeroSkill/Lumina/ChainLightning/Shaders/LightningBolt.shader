// 루미나 「체인 라이팅」 번개 볼트 — 축 정렬 리본 쿼드(광선 방식, 투사체 아님).
// C#이 트랜스폼(시작점=피벗, X축=방향, scale.x=길이)과 MPB(_Progress/_Seed/_Opacity)를 구동.
// - 지그재그: 길이를 _SegCount 세그먼트로 나눠 (세그먼트,시드) 해시로 꺾임점을 뽑고
//   구간 선형 연결(piecewise linear) — 각진 킹크. 미세 지그(_MicroJag) 2옥타브 중첩.
// - 빠르게 그어지는 선: x < _Progress만 표시 + 헤드 플래시가 스윕.
// - 플리커: C#이 _Seed를 주기 리롤 → 볼트·가지 형상 전체가 명멸.
// - 가지: 꺾임점에서 짧은 분기 스트로크(끝으로 갈수록 소멸).
// - 폭 방향만 카메라를 향하는 원통 빌보드. 가산 블렌드, 큐 3006.
Shader "Testbed/ChainLightning/LightningBolt"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorCore ("Core Color", Color) = (1.6, 1.5, 1.9, 1)
        [HDR] _ColorGlow ("Glow Color", Color) = (0.65, 0.42, 1.6, 1)
        _Emission ("Emission", Range(0,6)) = 1.6

        [Header(Driven by CSharp)]
        _Progress ("Draw Progress (0..1)", Range(0,1)) = 1
        _Seed ("Flicker Seed", Range(0,512)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1

        [Header(Shape)]
        _Width ("Half Width (m)", Range(0.02,1)) = 0.18
        _SegCount ("Kink Segments", Range(3,32)) = 14
        _JitterAmp ("Kink Amplitude (width units)", Range(0,0.85)) = 0.5
        _MicroJag ("Micro Jag Amplitude", Range(0,0.5)) = 0.18
        _MicroFreq ("Micro Jag Freq (x segments)", Range(1,6)) = 3
        _EndPin ("End Pin Range (0..0.5)", Range(0.01,0.5)) = 0.12
        _CoreWidth ("Core Half Width (width units)", Range(0.01,0.5)) = 0.09
        _GlowWidth ("Glow Falloff (width units)", Range(0.05,1)) = 0.42

        [Header(Head)]
        _HeadSize ("Head Flash Size (x units)", Range(0.01,0.3)) = 0.06
        _HeadBoost ("Head Flash Boost", Range(0,6)) = 2.2

        [Header(Branches)]
        _BranchCount ("Branch Count (0..3)", Range(0,3)) = 2
        _BranchLen ("Branch Length (x units)", Range(0.02,0.4)) = 0.14
        _BranchSlope ("Branch Slope (width per x)", Range(0,6)) = 3.2
        _BranchWidthMul ("Branch Width Mul", Range(0.1,1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "LightningBolt"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float bhash(float a, float b){ return frac(sin(a * 127.1 + b * 269.5 + 74.7) * 43758.5453); }

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 xw:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorCore,_ColorGlow;
                half _Emission;
                half _Progress,_Seed,_Opacity;
                half _Width,_SegCount,_JitterAmp,_MicroJag,_MicroFreq,_EndPin,_CoreWidth,_GlowWidth;
                half _HeadSize,_HeadBoost;
                half _BranchCount,_BranchLen,_BranchSlope,_BranchWidthMul;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                // 원통 빌보드: 피벗=시작점, 로컬 X축(스케일 포함)=볼트 축, 폭만 카메라를 향함
                float3 startWS = TransformObjectToWorld(float3(0,0,0));
                float3 axisFull = float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20);
                float3 axisDir = normalize(axisFull + 1e-6);
                float x01 = IN.uv.x;
                float wSign = IN.uv.y * 2.0 - 1.0;
                float3 alongWS = startWS + axisFull * x01;
                float3 view = alongWS - GetCameraPositionWS();
                float3 side = cross(axisDir, view);
                float sideLen = length(side);
                side = sideLen > 1e-4 ? side / sideLen : float3(0,1,0);
                float3 posWS = alongWS + side * wSign * _Width;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.xw = float2(x01, wSign);
                return OUT;
            }

            float Pin(float x){ return smoothstep(0.0, _EndPin, x) * smoothstep(0.0, _EndPin, 1.0 - x); }
            float NodeY(float k, float n, float amp, float seedOff)
            {
                float x = k / n;
                return (bhash(k + seedOff, _Seed) - 0.5) * 2.0 * amp * Pin(x);
            }

            half4 frag(Varyings IN):SV_Target{
                float x = IN.xw.x;
                float w = IN.xw.y;   // [-1,1], 1 = _Width(m)

                // 중심선: 대 꺾임 + 미세 지그(2옥타브)
                float n1 = _SegCount;
                float s1 = floor(x * n1); float f1 = frac(x * n1);
                float center = lerp(NodeY(s1, n1, _JitterAmp, 0.0), NodeY(s1 + 1.0, n1, _JitterAmp, 0.0), f1);
                float n2 = _SegCount * _MicroFreq;
                float s2 = floor(x * n2); float f2 = frac(x * n2);
                center += lerp(NodeY(s2, n2, _MicroJag, 51.7), NodeY(s2 + 1.0, n2, _MicroJag, 51.7), f2);

                float d = abs(w - center);
                float core = smoothstep(_CoreWidth, _CoreWidth * 0.35, d);
                float glow = exp(-d * d / max(_GlowWidth * _GlowWidth, 1e-4));

                // 가지: 대 꺾임점에서 분기, 끝으로 갈수록 가늘어지며 소멸
                [unroll]
                for (int b = 0; b < 3; b++)
                {
                    if ((float)b >= _BranchCount) break;
                    float fb = (float)b;
                    float hb = bhash(fb * 7.3 + 3.1, _Seed + 13.0);
                    float nodeK = floor(hb * (n1 - 2.0)) + 1.0;
                    float nx = nodeK / n1;
                    float ny = NodeY(nodeK, n1, _JitterAmp, 0.0);
                    float sgn = bhash(fb + 5.7, _Seed + 29.0) < 0.5 ? -1.0 : 1.0;
                    float slope = sgn * _BranchSlope * (0.5 + bhash(fb + 9.1, _Seed + 41.0));
                    float bl = _BranchLen * (0.6 + 0.8 * hb);
                    float t = (x - nx) / max(bl, 1e-3);
                    if (t > 0.0 && t < 1.0)
                    {
                        float by = ny + slope * (x - nx);
                        float bw = max(_CoreWidth * _BranchWidthMul * (1.0 - t), 0.004);
                        float bd = abs(w - by);
                        float bcore = smoothstep(bw, bw * 0.3, bd) * (1.0 - t);
                        core = max(core, bcore * 0.85);
                        glow += exp(-bd * bd / max(_GlowWidth * _GlowWidth * 0.25, 1e-4)) * (1.0 - t) * 0.5;
                    }
                }

                // 그어지는 선(헤드 스윕) + 헤드 플래시(선두 광점 — 중심선 주변만)
                float vis = smoothstep(_Progress + 0.005, _Progress - 0.02, x);
                float head = exp(-pow((x - _Progress) / max(_HeadSize, 1e-3), 2.0))
                           * exp(-d * d / max(_GlowWidth * _GlowWidth * 0.35, 1e-4))
                           * _HeadBoost
                           * (1.0 - step(0.999, _Progress) * 0.7);   // 완주 후 헤드 약화

                float a = saturate(core + glow * 0.7 + head * 0.5) * vis * _Opacity;
                half3 col = (_ColorCore.rgb * (core + head) + _ColorGlow.rgb * glow) * _Emission;
                if (a < 0.004) discard;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
