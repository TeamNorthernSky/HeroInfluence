// JC Taosenaiyo VFX — 혜성 실루엣: 유성 구체를 감싸는 티어드롭 광채(머리 글로우 + 뒤로 갈수록 가늘어지는 꼬리).
// 쿼드 +Y = 진행 방향(머리). 축 정렬·빌보드는 스크립트(CometShell)가 처리. 가산 블렌드.
// 페이드는 ProjectileVfx의 fadeRenderers(_FadeMul MPB)에 편승 — 도착 버스트 페이드 자동 동기.
Shader "JC/VFX/TaoComet"
{
    Properties
    {
        [HDR]_ColorHead ("Color At Head (gradient start)", Color) = (1, 0.98, 0.85, 1)
        [HDR]_ColorTail ("Color At Tail End (gradient end)", Color) = (1, 0.82, 0.3, 1)
        _Intensity ("Master Intensity", Range(0,10)) = 1.7
        [Header(Head)]
        _HeadInset ("Head Inset From Top", Range(0,0.6)) = 0.03
        [Header(Tail)]
        _TailStartWidth ("Tail Width At Head", Range(0.05,0.8)) = 0.5
        _TailEndWidth ("Tail Width At End", Range(0.005,0.4)) = 0.04
        _TailTaper ("Tail Taper Curve", Range(0.3,4)) = 1.2
        _TailFade ("Tail Brightness Falloff", Range(0.3,6)) = 1.6
        [Header(Rim)]
        [HDR]_RimColor ("Rim Color", Color) = (1, 0.95, 0.7, 1)
        _RimIntensity ("Rim Intensity", Range(0,10)) = 3.0
        _RimPos ("Rim Lateral Position", Range(0.3,1.2)) = 0.8
        _RimSoft ("Rim Softness", Range(0.02,0.5)) = 0.12
        _RimFade ("Rim Tailward Fade", Range(0.3,6)) = 1.2
        [Header(Motion)]
        _FlickerAmp ("Flicker Amount", Range(0,0.6)) = 0.2
        _FlickerSpeed ("Flicker Speed", Range(0,20)) = 7
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorHead; float4 _ColorTail;
                float _Intensity;
                float _HeadInset;
                float _TailStartWidth; float _TailEndWidth; float _TailTaper; float _TailFade;
                float4 _RimColor; float _RimIntensity; float _RimPos; float _RimSoft; float _RimFade;
                float _FlickerAmp; float _FlickerSpeed;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;   // a = 리본 꼬리끝 페이드(스크립트가 정점에 기록)
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
                float2 p = IN.uv * 2.0 - 1.0;      // x=측면, y=진행축(+y=머리)
                float hy = 1.0 - _HeadInset * 2.0; // 꼬리 시작 높이
                // ★머리 코마/링은 구형 메시(JC/VFX/TaoCometHead)가 전담 — 리본은 꼬리+측면 림만.

                // 꼬리: 머리 뒤로 갈수록 폭·밝기 감쇠 + 미세 플리커
                float t = saturate((hy - p.y) / max(hy + 1.0, 1e-3));   // 0=머리, 1=꼬리 끝
                float w = lerp(_TailStartWidth, _TailEndWidth, pow(t, _TailTaper));
                float lat = p.x / max(w, 1e-3);
                float flick = 1.0 + _FlickerAmp * (noise1(t * 5.0 - _Time.y * _FlickerSpeed) - 0.5);
                float tail = exp(-lat * lat * 3.0) * pow(saturate(1.0 - t), _TailFade) * flick;
                float capTop = 1.0 - smoothstep(hy, 1.0, p.y);          // 머리 위쪽 여백 컷
                tail *= capTop;

                // 림: 꼬리 실루엣 양옆을 따라 흐르는 얇은 아웃라인(리본이 휘면 같이 휨)
                float rim = exp(-pow(abs(lat) - _RimPos, 2.0) / max(2.0 * _RimSoft * _RimSoft, 1e-4))
                          * pow(saturate(1.0 - t), _RimFade) * capTop;

                // 경계 하드컷 방지: 리본 uv 4변 보더 페이드 + 정점 알파(꼬리끝 동적 페이드)
                float border = smoothstep(0.0, 0.12, IN.uv.x) * smoothstep(0.0, 0.12, 1.0 - IN.uv.x)
                             * smoothstep(0.0, 0.05, 1.0 - IN.uv.y) * smoothstep(0.0, 0.05, IN.uv.y);

                // 색 그라데이션: 머리 색 → 꼬리 끝 색을 리본을 따라 보간
                float3 tailCol = lerp(_ColorHead.rgb, _ColorTail.rgb, t);

                float3 col = (tailCol * tail + _RimColor.rgb * _RimIntensity * rim * 0.35)
                           * _Intensity * border * IN.color.a * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
