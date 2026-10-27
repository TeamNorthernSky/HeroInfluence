// JC 블랙불릿 AimShot VFX — 스타버스트 공용 셰이더: 중심 코어 + N방향 스파이크(빌보드 쿼드, uv 중심 0.5).
// 레퍼런스에서 세 곳이 같은 형상을 공유하므로 한 셰이더를 프리셋만 달리해 재사용한다.
//   ① 탄두 머리   — 작고 날카로운 코어 + 십자 스파이크
//   ② 머즐 플래시 — 코어 + 좌우로 긴 수평 스파이크(_SpikeAspect로 가로 강조)
//   ③ 탄착 피격   — 4방향 스타버스트(폭발·링 없는 최소 형태)
// 절차 SDF만 사용(텍스처 0장). 스파이크는 축 정렬 가우시안 2개를 회전시켜 합성한다.
// 스케일 펀치·수명은 스크립트(KAimShotVfx)가 _Scale/_FadeMul로 구동. 가산 블렌드.
Shader "JC/VFX/KStarBurst"
{
    Properties
    {
        [HDR]_CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR]_GlowColor ("Glow Color", Color) = (0.8, 0.9, 1, 1)
        _Intensity ("Intensity", Range(0,20)) = 4.0

        [Header(Core)]
        _CoreSize ("Core Size", Range(0.005,0.5)) = 0.045
        _CoreSoft ("Core Softness", Range(0.005,0.5)) = 0.06

        [Header(Spikes)]
        _SpikeCount ("Spike Pairs (1=cross 2=8point)", Range(1,3)) = 2
        _SpikeLength ("Spike Length", Range(0.05,1)) = 0.45
        _SpikeThin ("Spike Thinness", Range(0.002,0.2)) = 0.016
        _SpikeAspect ("Spike Aspect (H/V)", Range(0.1,6)) = 1.0
        _SpikeBoost ("Spike Boost", Range(0,5)) = 1.2
        _SpikeAngle ("Spike Angle (deg)", Range(0,90)) = 0.0

        [Header(Halo)]
        _HaloSize ("Halo Size", Range(0.0,1)) = 0.22
        _HaloFalloff ("Halo Falloff", Range(0.5,8)) = 3.0
        _HaloBoost ("Halo Boost", Range(0,3)) = 0.5

        [Header(Edge)]
        _EdgeFade ("Edge Fade Start (radius)", Range(0.1,0.5)) = 0.38

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
                float _Intensity;
                float _CoreSize; float _CoreSoft;
                float _SpikeCount; float _SpikeLength; float _SpikeThin;
                float _SpikeAspect; float _SpikeBoost; float _SpikeAngle;
                float _HaloSize; float _HaloFalloff; float _HaloBoost;
                float _EdgeFade;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 축 정렬 스파이크 1쌍(수평+수직): 긴 축은 지수 감쇠, 짧은 축은 얇은 가우시안.
            float spikePair(float2 p, float len, float thin, float aspect)
            {
                float hx = exp(-abs(p.x) / max(len * aspect, 1e-4));
                float hy = exp(-(p.y * p.y) / max(thin * thin, 1e-6));
                float horizontal = hx * hy;

                float vx = exp(-(p.x * p.x) / max(thin * thin, 1e-6));
                float vy = exp(-abs(p.y) / max(len / max(aspect, 1e-3), 1e-4));
                float vertical = vx * vy;

                return max(horizontal, vertical);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                float d = length(p);

                // ── 코어: 중심 고휘도 점 ──
                float core = 1.0 - smoothstep(_CoreSize, _CoreSize + _CoreSoft, d);

                // ── 스파이크: 기본 쌍 + (_SpikeCount>=2면) 45도 회전 쌍 추가 = 8방향 ──
                float rad = radians(_SpikeAngle);
                float cs = cos(rad), sn = sin(rad);
                float2 pr = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
                float spike = spikePair(pr, _SpikeLength, _SpikeThin, _SpikeAspect);

                if (_SpikeCount >= 2.0)
                {
                    float rad2 = rad + radians(45.0);
                    float cs2 = cos(rad2), sn2 = sin(rad2);
                    float2 pr2 = float2(p.x * cs2 - p.y * sn2, p.x * sn2 + p.y * cs2);
                    // 대각 쌍은 짧고 약하게 — 주 십자가 형태를 잃지 않도록.
                    float diag = spikePair(pr2, _SpikeLength * 0.55, _SpikeThin * 0.8, 1.0);
                    spike = max(spike, diag * 0.6);
                }

                // ── 헤일로: 완만한 외곽 발광 ──
                float halo = pow(saturate(1.0 - d / max(_HaloSize, 1e-3)), _HaloFalloff) * _HaloBoost;

                // ── 외곽 페이드: 스파이크 끝이 쿼드 경계에서 각지게 잘리는 것을 막는다 ──
                float edgeMask = 1.0 - smoothstep(_EdgeFade, 0.5, d);

                float3 col = _CoreColor.rgb * core
                           + _GlowColor.rgb * (spike * _SpikeBoost + halo);
                col *= _Intensity * _FadeMul * edgeMask;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
