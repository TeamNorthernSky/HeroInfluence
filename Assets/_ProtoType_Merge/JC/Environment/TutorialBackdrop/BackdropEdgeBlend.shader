Shader "JC/Environment/BackdropEdgeBlend"
{
    Properties
    {
        _BaseMap("배경 이미지", 2D) = "white" {}
        _EdgeFade("좌우 연결 혼합 폭 (UV 비율, 0은 혼합 없음)", Range(0,0.2)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _EdgeFade;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float edgeDistance = min(input.uv.x, 1.0 - input.uv.x);
                color.a *= _EdgeFade > 0 ? smoothstep(0.0, _EdgeFade, edgeDistance) : 1.0;
                return color;
            }
            ENDHLSL
        }
    }
}
