// 루미나 「프리즘 익스플로전」 연기 파티클용 알파 블렌드 셰이더.
// FlareParticleAdd(가산)의 알파 블렌드 변형 — 어두운 연기가 배경을 덮는다.
// 버텍스 컬러 × 텍스처 × 틴트. 큐 3002(폭발 버스트 아래).
Shader "Testbed/PrismExplosion/FlareParticleAlpha"
{
    Properties
    {
        _BaseMap ("Base Map (soft dot)", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.28, 0.25, 0.36, 0.6)
        _Emission ("Emission", Range(0,3)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareParticleAlpha"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
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
