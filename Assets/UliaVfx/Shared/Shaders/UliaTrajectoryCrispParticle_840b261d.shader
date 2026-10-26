Shader "UliaVfx/VFX/Ulia Trajectory Crisp Particle"
{
    Properties
    {
        [MainTexture] _MainTex ("Particle Texture", 2D) = "white" {}
        [MainColor] _TintColor ("Tint", Color) = (0.5,0.5,0.5,0.5)
        _DetailThreshold ("Detail threshold", Range(0,1)) = 0.22
        _EdgeSoftness ("Edge softness", Range(0.01,1)) = 0.18
        _Intensity ("Intensity", Float) = 1
        _Frame ("Continuous sprite frame", Float) = 0
        _Columns ("Sprite columns", Float) = 4
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "UliaParticle"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                float _DetailThreshold;
                float _EdgeSoftness;
                float _Intensity;
                float _Frame;
                float _Columns;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float columns = max(1.0, _Columns);
                float first = fmod(floor(_Frame), columns);
                float second = fmod(first + 1.0, columns);
                float2 uvA = float2((input.uv.x + first) / columns, input.uv.y);
                float2 uvB = float2((input.uv.x + second) / columns, input.uv.y);
                half4 texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA);
                half4 texB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB);
                half contrast = 1.0h + _DetailThreshold * 0.5h;
                half inkA = pow(saturate(max(texA.r, max(texA.g, texA.b))), contrast) * texA.a;
                half inkB = pow(saturate(max(texB.r, max(texB.g, texB.b))), contrast) * texB.a;
                half ink = lerp(inkA, inkB, frac(_Frame));
                return half4(ink, ink, ink, 1) * input.color * _TintColor * _Intensity;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
