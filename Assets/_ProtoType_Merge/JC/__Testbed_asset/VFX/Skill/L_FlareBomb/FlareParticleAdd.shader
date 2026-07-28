// 루미나 「플레어 봄」 비행 궤적·불씨 공용 가산 셰이더.
// TrailRenderer/ParticleSystem 양쪽에서 사용(버텍스 컬러 × 텍스처 × 틴트).
// 큐 3003(오브 전면과 탄착 버스트 사이).
Shader "Testbed/FlareBomb/FlareParticleAdd"
{
    Properties
    {
        _BaseMap ("Base Map (soft dot)", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (1.2, 0.8, 0.3, 1)
        _Emission ("Emission", Range(0,6)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareParticleAdd"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _Emission;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target{
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 c = tex * IN.color * _Tint;
                c.rgb *= _Emission;
                if (c.a < 0.003) discard;
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
