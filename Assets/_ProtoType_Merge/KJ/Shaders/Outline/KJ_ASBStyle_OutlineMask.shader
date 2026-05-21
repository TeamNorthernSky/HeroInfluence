Shader "Custom/KJ/ASBStyle/Outline Mask"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 8
        _StencilRef ("Stencil Ref", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            ColorMask 0

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
