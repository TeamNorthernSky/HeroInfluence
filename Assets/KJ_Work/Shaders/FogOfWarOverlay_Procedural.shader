Shader "Custom/FogOfWarOverlay_Procedural"
{
    Properties
    {
        _FogVisitedColor("Visited Fog Color", Color) = (0.5, 0.5, 0.5, 0.8)
        _FogUnexploredColor("Unexplored Fog Color", Color) = (0.0, 0.0, 0.0, 1.0)
        
        // [Header("Procedural Cloud Settings")]
        _NoiseScale("Noise Scale", Float) = 3.0
        _NoiseSpeed("Noise Speed", Vector) = (0.2, 0.2, 0, 0)
        _WarpStrength("Domain Warp Strength", Float) = 0.5
        _DetailStrength("Detail Strength", Float) = 0.3
        _CloudCoverage("Cloud Coverage", Range(0.0, 1.0)) = 0.5
        _CloudSoftness("Cloud Softness", Range(0.01, 1.0)) = 0.3
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

            float4 _FogMapBounds; 
            float4 _FogVisitedColor;
            float4 _FogUnexploredColor;
            
            float _NoiseScale;
            float4 _NoiseSpeed;
            float _WarpStrength;
            float _DetailStrength;
            float _CloudCoverage;
            float _CloudSoftness;
            
            // --- PROCEDURAL NOISE FUNCTIONS ---
            
            float random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            float2 perlinFade(float2 t)
            {
                // 6t^5 - 15t^4 + 10t^3 (Smootherstep interpolation)
                return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
            }

            float noise(float2 uv)
            {
                float2 cellID = floor(uv);
                float2 fracUV = frac(uv);

                float a = random(cellID);
                float b = random(cellID + float2(1.0, 0.0));
                float c = random(cellID + float2(0.0, 1.0));
                float d = random(cellID + float2(1.0, 1.0));

                float2 u = perlinFade(fracUV);

                float top = lerp(a, b, u.x);
                float bottom = lerp(c, d, u.x);
                return lerp(top, bottom, u.y);
            }

            float fbm(float2 uv)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                // 옥타브 루프 (세밀함을 결정)
                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * noise(uv * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            float2 warp(float2 uv)
            {
                float2 offset1 = float2(5.2, 1.3);
                float2 offset2 = float2(1.7, 9.2);

                float wx = fbm(uv + offset1);
                float wy = fbm(uv + offset2);

                return (float2(wx, wy) * 2.0 - 1.0);
            }

            float cloudMask(float2 uv)
            {
                float2 w = warp(uv) * _WarpStrength;
                float warped = fbm(uv + w);

                float detail = fbm((uv + w) * 2.5);
                warped = warped + (detail - 0.5) * _DetailStrength;

                float threshold = 1.0 - _CloudCoverage;
                float m = smoothstep(threshold - _CloudSoftness, threshold + _CloudSoftness, warped);

                return saturate(m);
            }
            // ------------------------------------

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 fogUV = float2(input.positionWS.x - _FogMapBounds.x, input.positionWS.z - _FogMapBounds.y) / _FogMapBounds.zw;

                if (fogUV.x < 0.0 || fogUV.x > 1.0 || fogUV.y < 0.0 || fogUV.y > 1.0)
                {
                    return _FogUnexploredColor;
                }

                float currentVis = saturate(SAMPLE_TEXTURE2D(_FogCurrentRT, sampler_FogCurrentRT, fogUV).r);
                float visitedVis = saturate(SAMPLE_TEXTURE2D(_FogVisitedRT, sampler_FogVisitedRT, fogUV).r);

                // 절차적 구름 노이즈 생성
                float2 noiseUV = fogUV * _NoiseScale + _NoiseSpeed.xy * _Time.y;
                float proceduralCloud = cloudMask(noiseUV);

                half4 baseFog = lerp(_FogUnexploredColor, _FogVisitedColor, visitedVis);
                
                // 구름 노이즈 값을 기반으로 안개 색상의 투명도나 밝기를 일렁이게 보정
                float cloudVariation = (proceduralCloud - 0.5);
                baseFog.rgb = saturate(baseFog.rgb + cloudVariation * 0.2);

                // 구름 형태(노이즈)를 투명도에 최종 혼합 (탐험 안 한 곳일수록 두께 차이 체감이 심하게)
                // visitedVis가 높을수록 덜 두껍고, 낮을수록 구름이 두껍게 보이게.
                float alphaCloud = saturate(baseFog.a + cloudVariation * 0.8);
                
                float finalAlpha = alphaCloud * (1.0 - currentVis);

                return half4(baseFog.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
