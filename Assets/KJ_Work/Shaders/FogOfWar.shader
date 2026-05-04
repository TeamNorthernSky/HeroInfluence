Shader "Hidden/FogOfWar"
{
    Properties
    {
        _BlitTexture("Source", 2D) = "white" {}
        _FogVisitedColor("Visited Fog Color", Color) = (0.5, 0.5, 0.5, 0.8)
        _FogUnexploredColor("Unexplored Fog Color", Color) = (0.0, 0.0, 0.0, 1.0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "FogOfWarPass"
            
            HLSLPROGRAM
            #pragma vertex Vert // Blit.hlsl 에서 제공하는 Vert 사용
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_BlitTexture);

            // 전역 세팅 변수들 (FogOfWarManager가 C#에서 할당)
            TEXTURE2D(_FogCurrentRT);     SAMPLER(sampler_FogCurrentRT);
            TEXTURE2D(_FogVisitedRT);     SAMPLER(sampler_FogVisitedRT);

            float4 _FogMapBounds; // x: map minX, y: map minZ, z: width, w: height
            float4 _FogVisitedColor;
            float4 _FogUnexploredColor;
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // 1. Scene 기본 컬러 읽기
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                
                // 2. Depth 샘플링 후 스카이박스 제외
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                if (rawDepth < 0.000001) return sceneColor;
                #else
                if (rawDepth > 0.999999) return sceneColor;
                #endif

                // 3. 화면 픽셀의 월드 좌표 계산
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

                // 4. 월드 좌표를 기준삼아 Fog UV (0~1)로 변환
                float2 fogUV = float2(worldPos.x - _FogMapBounds.x, worldPos.z - _FogMapBounds.y) / _FogMapBounds.zw;

                // 맵 공간 밖이면 전장 밖이므로 완전 미탐험색으로 처리
                if(fogUV.x < 0.0 || fogUV.x > 1.0 || fogUV.y < 0.0 || fogUV.y > 1.0)
                {
                    half3 c = lerp(sceneColor.rgb, _FogUnexploredColor.rgb, _FogUnexploredColor.a);
                    return half4(c, sceneColor.a);
                }

                // 5. 시야 맵 및 누적 맵 상태 읽기 (r 채널)
                float currentVis = SAMPLE_TEXTURE2D(_FogCurrentRT, sampler_FogCurrentRT, fogUV).r;
                float visitedVis = SAMPLE_TEXTURE2D(_FogVisitedRT, sampler_FogVisitedRT, fogUV).r;

                // 6. 안개 농도 및 컬러 결정
                // 누적 시야(Visited) 기준 먼저 처리
                half4 baseFogColor = lerp(_FogUnexploredColor, _FogVisitedColor, visitedVis);
                
                // 현재 보이는 곳(Current)이면 안개 알파를 줄이기
                float finalFogAlpha = baseFogColor.a * (1.0 - currentVis);

                // 최종 색상
                half3 finalColor = lerp(sceneColor.rgb, baseFogColor.rgb, finalFogAlpha);

                return half4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
