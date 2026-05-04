// ============================================================================
// FogMaskJC
// 플레이어 시야의 원형 마스크를 RT_Current에 Blit하는 용도.
// _PlayerWorldPos, _SightRadius, _SmoothEdge를 입력 받아 R8 텍스처에 0~1 값 출력.
// smoothstep으로 경계가 부드러워져 그리드 셀 계단을 완화.
// ============================================================================
Shader "Custom/JC/FogMask"
{
    Properties
    {
        _MainTex ("Main Tex (unused, blit source placeholder)", 2D) = "black" {}
        _SmoothEdge ("Smooth Edge Width (world units)", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "FogMaskPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Material-level
            float _SmoothEdge;

            // 외부 입력 (PlayFogManager가 material에 직접 세팅)
            float4 _PlayerWorldPos; // xy = world pos (XZ), zw unused
            float _SightRadius;

            // Global (PlayGridManager.Initialize가 설정)
            float4 _GridWorldSize; // (worldW, worldH, 1/worldW, 1/worldH)

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // RT의 UV (0~1)를 월드 2D 좌표(XZ 평면)로 변환
                float2 worldPos2D = input.uv * _GridWorldSize.xy;

                float2 diff = worldPos2D - _PlayerWorldPos.xy;
                float dist = length(diff);

                // smoothstep: (radius - edge)에서 1.0, radius에서 0.0
                float mask = 1.0 - smoothstep(_SightRadius - _SmoothEdge, _SightRadius, dist);

                return half4(mask, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
