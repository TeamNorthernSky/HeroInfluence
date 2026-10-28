// JC PawForYou VFX — 광선/스트릭 공용 셰이더: 축 방향 쿼드(uv.y=축, 0=시작/1=끝).
// 코어(고휘도 중심선) + 글로우(외곽 감쇠) 2층 + 폭 펄스 + 세로 스크롤 노이즈 + 양끝 소프트 캡.
// _Extend: 시작(uv.y=0)→끝(uv.y=1)으로 신장하는 진행도. 워프 스트릭과 수직 빔이 프리셋만 달리해 공용.
// 축 정렬·길이·빌보드는 스크립트(PawBeam)가 처리. 가산 블렌드.
Shader "JC/VFX/PawBeam"
{
    Properties
    {
        [HDR]_CoreColor ("Core Color", Color) = (1, 1, 0.9, 1)
        [HDR]_GlowColor ("Glow Color", Color) = (1, 0.8, 0.25, 1)
        _Intensity ("Intensity", Range(0,10)) = 2.5
        _CoreWidth ("Core Width", Range(0.01,0.6)) = 0.18
        _GlowFalloff ("Glow Falloff", Range(0.5,8)) = 2.2
        _EdgeSoft ("Edge Softness", Range(0.005,0.3)) = 0.06
        [Header(Noise Pulse)]
        _NoiseScale ("Noise Scale", Range(0,20)) = 6
        _NoiseScroll ("Noise Scroll", Range(-10,10)) = 3
        _NoiseAmount ("Noise Amount", Range(0,1)) = 0.25
        _PulseAmp ("Pulse Amp", Range(0,0.5)) = 0.12
        _PulseFreq ("Pulse Freq", Range(0,20)) = 7
        [Header(Caps Extend)]
        _CapSoftStart ("Cap Soft Start", Range(0,0.5)) = 0.08
        _CapSoftEnd ("Cap Soft End", Range(0,0.5)) = 0.12
        _FrontSoft ("Front Softness", Range(0.005,0.3)) = 0.05
        _Extend ("Extend", Range(0,1)) = 1.0
        _FadeMul ("Fade Multiplier", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
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
                float4 _CoreColor; float4 _GlowColor;
                float _Intensity; float _CoreWidth; float _GlowFalloff; float _EdgeSoft;
                float _NoiseScale; float _NoiseScroll; float _NoiseAmount;
                float _PulseAmp; float _PulseFreq;
                float _CapSoftStart; float _CapSoftEnd; float _FrontSoft; float _Extend;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash1(float n) { return frac(sin(n) * 43758.5453); }
            float noise1(float x)
            {
                float i = floor(x); float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(hash1(i), hash1(i + 1.0), f);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float y = IN.uv.y;
                float x2 = abs(IN.uv.x - 0.5) * 2.0;

                // 폭 변조: 펄스(주기 호흡) + 세로 스크롤 노이즈(에너지 흐름)
                float n = noise1(y * _NoiseScale - _Time.y * _NoiseScroll);
                float widthMod = 1.0 + _PulseAmp * sin(_Time.y * _PulseFreq + y * 3.0)
                                     + _NoiseAmount * (n - 0.5);
                float lat = x2 / max(widthMod, 0.2);

                float glow = pow(saturate(1.0 - lat), _GlowFalloff);
                float core = 1.0 - smoothstep(_CoreWidth, _CoreWidth + _EdgeSoft, lat);

                // 양끝 소프트 캡 + 신장 프론트(시작쪽부터 끝쪽으로 자람)
                float capA = _CapSoftStart > 1e-4 ? smoothstep(0.0, _CapSoftStart, y) : 1.0;
                float capB = _CapSoftEnd > 1e-4 ? smoothstep(0.0, _CapSoftEnd, 1.0 - y) : 1.0;
                float front = 1.0 - smoothstep(_Extend - _FrontSoft, _Extend + _FrontSoft, y);
                // 신장 중 프론트 하이라이트(도착 순간의 밝은 머리)
                float tip = exp(-abs(y - _Extend) / max(_FrontSoft, 1e-3)) * step(_Extend, 0.999);

                float3 col = (_CoreColor.rgb * (core + tip * 0.8) + _GlowColor.rgb * glow) * _Intensity;
                col *= capA * capB * front * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
