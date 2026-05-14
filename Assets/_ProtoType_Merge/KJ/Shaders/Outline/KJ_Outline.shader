Shader "Custom/KJ/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.5)) = 0.02
        [Space(10)]
        [KeywordEnum(Normal, Color, UV2)] _NormalSource ("Normal Source", Float) = 0
        [KeywordEnum(Screen, World)] _WidthMode ("Width Mode", Float) = 0
        _DepthBias ("Depth Bias", Range(-0.01, 0.05)) = 0.001
        [Toggle(_USE_VERTEX_ALPHA_WIDTH)] _UseVertexAlphaWidth ("Use Vertex Color Alpha as Width Multiplier", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+1" }
        LOD 100

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front      // Inverted Hull: 뒤집힌 메쉬만 렌더링
            ZWrite Off
            ZTest LEqual    // 깊이 비교 → 다른 오브젝트에 의해 가려지면 외곽선도 안 보임

            // 스텐실 1이 기록된 픽셀(캐릭터 내부)은 건너뜀
            // → 실루엣 바깥 픽셀(외곽선 영역)에만 그림
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _NORMALSOURCE_NORMAL _NORMALSOURCE_COLOR _NORMALSOURCE_UV2
            #pragma multi_compile _WIDTHMODE_SCREEN _WIDTHMODE_WORLD
            #pragma shader_feature _USE_VERTEX_ALPHA_WIDTH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float4 uv2        : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half  _OutlineWidth;
                float _DepthBias;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float widthMult = 1.0;
#if defined(_USE_VERTEX_ALPHA_WIDTH)
                widthMult = input.color.a;
#endif

                // 클립공간 기준 position
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float4 clipPos  = TransformWorldToHClip(worldPos);

                // 노멀 소스 선택 (Smoothed Normal 적용)
                float3 outlineNormal = input.normalOS;
#if defined(_NORMALSOURCE_COLOR)
                // Color 데이터는 보통 0~1로 베이크되므로 -1~1 범위로 매핑
                outlineNormal = input.color.rgb * 2.0 - 1.0;
#elif defined(_NORMALSOURCE_UV2)
                outlineNormal = input.uv2.xyz;
#endif

#if defined(_WIDTHMODE_WORLD)
                // 1. 월드 공간(World Space) 기준 확장
                float3 worldNormal = TransformObjectToWorldNormal(outlineNormal);
                worldPos += worldNormal * (_OutlineWidth * widthMult);
                clipPos = TransformWorldToHClip(worldPos);
#else
                // 2. 화면 공간(Screen Space) 기준 확장
                float3 worldNormal = TransformObjectToWorldNormal(outlineNormal);
                float3 clipNormal  = TransformWorldToHClipDir(worldNormal);

                float2 offset = clipNormal.xy;
                float len = length(offset);

                // 스파이크(NaN) 방지
                if (len > 0.0001)
                {
                    offset = offset / len; 
                }
                else
                {
                    offset = float2(0, 0);
                }

                // 화면 비율(Aspect Ratio) 보정
                offset.x *= _ScreenParams.y / _ScreenParams.x;

                // 실루엣 확장 적용
                clipPos.xy += offset * _OutlineWidth * widthMult * clipPos.w;
#endif

                // Z-Fighting(끊김/파묻힘) 방지 및 Depth Bias 적용
                clipPos.z -= _DepthBias * clipPos.w;

                output.positionCS = clipPos;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
