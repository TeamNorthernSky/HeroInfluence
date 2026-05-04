Shader "Custom/FogOfWarOverlay"
{
    Properties
    {
        _FogVisitedColor("Visited Fog Color", Color) = (0.5, 0.5, 0.5, 0.8)
        _FogUnexploredColor("Unexplored Fog Color", Color) = (0.0, 0.0, 0.0, 1.0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            SAMPLER(sampler_FogCurrentRT);
            SAMPLER(sampler_FogVisitedRT);
            TEXTURE2D(_FogCurrentRT);
            TEXTURE2D(_FogVisitedRT);

            float4 _FogMapBounds; // x: minX, y: minZ, z: width, w: height
            float4 _FogVisitedColor;
            float4 _FogUnexploredColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 월드 기준의 자가 UV 계산
                float2 fogUV = float2(input.positionWS.x - _FogMapBounds.x, input.positionWS.z - _FogMapBounds.y) / _FogMapBounds.zw;

                // 텍스처 밖이면 완전 미탐험 지역 컬러
                if (fogUV.x < 0.0 || fogUV.x > 1.0 || fogUV.y < 0.0 || fogUV.y > 1.0)
                {
                    return _FogUnexploredColor;
                }

                // RT에서 시야 정보 샘플링
                float currentVis = saturate(SAMPLE_TEXTURE2D(_FogCurrentRT, sampler_FogCurrentRT, fogUV).r);
                float visitedVis = saturate(SAMPLE_TEXTURE2D(_FogVisitedRT, sampler_FogVisitedRT, fogUV).r);

                // 누적 시야 상태 기준 컬러 혼합
                half4 baseFog = lerp(_FogUnexploredColor, _FogVisitedColor, visitedVis);
                
                // 현재 시야(current) 안개 투명도 빼주기. 1.0 이 보인다는 뜻
                float finalAlpha = baseFog.a * (1.0 - currentVis);

                return half4(baseFog.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
