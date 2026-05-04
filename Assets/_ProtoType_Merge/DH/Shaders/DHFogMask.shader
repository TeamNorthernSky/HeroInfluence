Shader "Custom/DH/FogMask"
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
            Name "DHFogMaskPass"

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

            float _SmoothEdge;
            float4 _PlayerWorldPos;
            float _SightRadius;
            float4 _GridWorldSize;

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 worldPos2D = input.uv * _GridWorldSize.xy;
                float2 diff = worldPos2D - _PlayerWorldPos.xy;
                float dist = length(diff);
                float mask = 1.0 - smoothstep(_SightRadius - _SmoothEdge, _SightRadius, dist);
                return half4(mask, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
