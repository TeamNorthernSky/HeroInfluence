Shader "Custom/KJ/ASBStyle/Outline Fill"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 2
        _StencilRef ("Stencil Ref", Float) = 1
        [KeywordEnum(Normal, UV2, UV3)] _NormalSource ("Normal Source", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+110"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "Fill"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _NORMALSOURCE_NORMAL _NORMALSOURCE_UV2 _NORMALSOURCE_UV3

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 uv2 : TEXCOORD1;
                float4 uv3 : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            float3 GetOutlineNormalOS(Attributes input)
            {
                float3 outlineNormal = input.normalOS;

            #if defined(_NORMALSOURCE_UV2)
                outlineNormal = input.uv2.xyz;
            #elif defined(_NORMALSOURCE_UV3)
                outlineNormal = input.uv3.xyz;
            #endif

                return dot(outlineNormal, outlineNormal) > 0.000001
                    ? normalize(outlineNormal)
                    : normalize(input.normalOS);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 normalOS = GetOutlineNormalOS(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);

                float3 viewPosition = mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).xyz;
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));

                viewPosition += viewNormal * -viewPosition.z * (_OutlineWidth / 1000.0);

                output.positionCS = mul(UNITY_MATRIX_P, float4(viewPosition, 1.0));
                output.color = _OutlineColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
