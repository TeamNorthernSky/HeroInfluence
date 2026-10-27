// 저스티스 — 코어 획 (Stroke Core)
//
// 트레일(Stretch 모드)의 단면에 「몸통 + 중심 코어」를 그린다.
// 트레일 UV: x = 길이 방향, ★y = 폭 방향(0~1, 0.5가 중심선).
//
// 하이라이트 3축 정리:
//   길이 방향(머리→꼬리)  colorOverTrail / 정점색
//   시간 방향(수명)        colorOverLifetime(정점색으로 유입)
//   ★폭 방향(중심선)      이 셰이더            ← 플레어 봄 등고선획과 같은 프로파일 구조
//
// 본체 색은 정점색(수명 그라데이션 head/mid/tail)이 전담하고,
// 코어는 셰이더 uniform이라 본체와 완전히 독립이다.
Shader "Testbed/Justice/StrokeCore"
{
    Properties
    {
        _CoreColor ("Core Color (중심선 하이라이트 · 발광 포함)", Color) = (2, 2, 2, 1)
        _CoreWidth ("Core Width (폭 대비 비율 0~1)", Float) = 0.35
        _CoreSharp ("Core Sharpness (경계 날카로움)", Float) = 2.0
        _BodyFalloff ("Body Falloff (단면 감쇠 지수 — 클수록 샤프)", Float) = 2.5
        _EndFade ("End Fade (획 양끝 페이드 구간 0~0.5)", Float) = 0.15
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One          // 가산
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _CoreColor;
            float _CoreWidth, _CoreSharp, _BodyFalloff, _EndFade;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = abs(IN.uv.y - 0.5) * 2.0;              // 0=중심선, 1=가장자리

                // 몸통 — ★종(bell) 모양 단면: 중심 정점에서 가장자리로 지수 감쇠.
                // 처음엔 평탄한 밴드 + 가장자리 smoothstep이었는데, 폭의 40%가 완전
                // 불투명해 리본 덩어리로 보였다(샤프함 소실 회귀). 구 재질의 spark_dot
                // 텍스처가 주던 종 모양 프로파일을 절차식으로 재현한 것이 이 형태다.
                float body = pow(saturate(1.0 - d), max(_BodyFalloff, 0.1));

                // 획 양끝(길이 방향) 페이드 — 머리·꼬리가 뚝 잘리지 않게.
                float ef = max(_EndFade, 0.001);
                float ends = smoothstep(0.0, ef, IN.uv.x) * smoothstep(1.0, 1.0 - ef, IN.uv.x);

                // 코어 — 중심 밴드. 본체와 독립된 자체 색·발광.
                float core = pow(saturate(1.0 - d / max(_CoreWidth, 0.01)), max(_CoreSharp, 0.1));

                // 가산: 몸통 위에 코어가 얹힌다. 알파(수명 감쇠 × 단면 × 양끝)가 전체를 다스린다.
                half3 rgb = IN.color.rgb + _CoreColor.rgb * core;
                half a = body * ends * IN.color.a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
