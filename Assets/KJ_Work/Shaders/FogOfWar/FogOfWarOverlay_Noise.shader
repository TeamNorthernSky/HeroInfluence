Shader "Custom/FogOfWarOverlay_Noise"
{
    Properties
    {
        _FogVisitedColor("Visited Fog Color", Color) = (0.5, 0.5, 0.5, 0.8)
        _FogUnexploredColor("Unexplored Fog Color", Color) = (0.0, 0.0, 0.0, 1.0)
        
        //[Header("Noise Settings")]
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _NoiseOffset("Noise Offset", Vector) = (0, 0, 0, 0)
        _NoiseScale("Noise Scale", Vector) = (5.0, 5.0, 0, 0)
        _NoiseIntensity("Noise Intensity", Range(0, 1)) = 0.5
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
            
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_NoiseTex);

            float4 _FogMapBounds; 
            float4 _FogVisitedColor;
            float4 _FogUnexploredColor;
            
            float4 _NoiseOffset;
            float4 _NoiseScale;
            float _NoiseIntensity;

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

                // 노이즈 텍스처 샘플링 (스케일 및 오프셋 적용하여 UV 스크롤링)
                float2 noiseUV = fogUV * _NoiseScale.xy + _NoiseOffset.xy;
                // 노이즈 값 추출 (0.0 ~ 1.0)
                float noiseVal = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                
                // 중심 0.5를 기점으로 -0.5 ~ +0.5 사이로 범위를 바꿔주고 강도 곱셈
                float noiseVariance = (noiseVal - 0.5) * _NoiseIntensity;

                // 누적 시야 상태 기준 기본 컬러 혼합
                half4 baseFog = lerp(_FogUnexploredColor, _FogVisitedColor, visitedVis);
                
                // 안개 색상에도 약간의 노이즈 추가 (구름 빛깔 다변화)
                baseFog.rgb = saturate(baseFog.rgb + noiseVariance * 0.5);

                // 투명도(알파)에도 노이즈를 섞어 두께가 달라보이는 일렁임 효과 적용
                float alphaWithNoise = saturate(baseFog.a + noiseVariance);
                
                // 현재 시야(current) 가시거리 영역을 무시해줌 (1.0이면 투명하게)
                float finalAlpha = alphaWithNoise * (1.0 - currentVis);

                return half4(baseFog.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
