Shader "Custom/KJ/StencilWrite"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "StencilWrite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // 컬러·깊이 출력 없이 스텐실 값만 기록
            ColorMask 0
            ZWrite Off
            ZTest LEqual   // 깊이 기준으로 가려지는 부분은 스텐실에도 기록하지 않음
            //Cull Back
            Cull off

            Stencil
            {
                Ref 1
                Comp Always   // 항상 통과
                Pass Replace  // 스텐실 버퍼에 Ref(1) 기록
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
