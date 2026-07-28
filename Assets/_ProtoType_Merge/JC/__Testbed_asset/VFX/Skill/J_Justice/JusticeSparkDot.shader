// 저스티스 분사 파편 입자.
// 가운데 광점(코어) + 바깥 붉은 몸통 구조를 가지며, 수명에 따라 **코어 영역 비율이 커진다**.
// 후쿠다 연출식 분사 파편: 붉게 태어나 → 속이 밝아지며 → 작아지고 옅어져 사라진다.
//
// 수명 정보는 ParticleSystemRenderer의 Custom Vertex Stream(AgePercent)으로 받는다.
// 스트림 구성: Position, Color, UV(xy) + AgePercent(z) → uv.z 가 0..1 나이.
Shader "Testbed/Justice/SparkDot"
{
    Properties
    {
        [HDR] _BodyColor ("Body Color (몸통)", Color) = (1, 0.1, 0.11, 1)
        [HDR] _CoreColor ("Core Color (광점)", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 8)) = 1

        _CoreStart ("Core Ratio Start (초기 광점 비율)", Range(0, 1)) = 0.18
        _CoreEnd   ("Core Ratio End (말기 광점 비율)", Range(0, 1)) = 0.95
        _CoreSharp ("Core Edge Sharpness", Range(0.01, 1)) = 0.35
        _BodySoft  ("Body Edge Softness", Range(0.01, 1)) = 0.30
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "JusticeSparkDot"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor;
                half4 _CoreColor;
                half  _Emission;
                half  _CoreStart;
                half  _CoreEnd;
                half  _CoreSharp;
                half  _BodySoft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv         : TEXCOORD0;   // xy=UV, z=AgePercent
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 쿼드 중심 기준 반경 0(중심)~1(가장자리)
                float d = length(IN.uv.xy - 0.5) * 2.0;

                float age = saturate(IN.uv.z);

                // 몸통 — 부드러운 원형 디스크
                float body = 1.0 - smoothstep(1.0 - _BodySoft, 1.0, d);

                // 광점 — 나이가 들수록 반경이 커져 몸통을 잠식한다
                float coreR = lerp(_CoreStart, _CoreEnd, age);
                float coreInner = coreR * (1.0 - _CoreSharp);
                float core = 1.0 - smoothstep(coreInner, coreR, d);

                half3 rgb = lerp(_BodyColor.rgb, _CoreColor.rgb, core);
                rgb *= _Emission * IN.color.rgb;

                // 알파는 파티클 시스템의 colorOverLifetime이 지배(전체 옅어짐)
                half a = body * IN.color.a;
                if (a < 0.003) discard;

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
