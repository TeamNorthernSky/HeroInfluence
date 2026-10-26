Shader "Hidden/JC/Environment/Cloud Shadow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "CloudShadowComposite"
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "JcCloudShadowCommon.hlsl"
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o; o.positionCS = GetFullScreenTriangleVertexPosition(vertexID); return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float depth=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if(depth<=.00001) return 0;
                #else
                    if(depth>=.99999) return 0;
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 world=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                return half4(_JcCloudColor.rgb,JcCloudMask(world));
            }
            ENDHLSL
        }
    }
}
