// 루미나 「솔라 프리즘」 바닥 반사광 — 지면에 눕힌 쿼드의 부드러운 글로우 디스크.
// _Opacity는 C#(소환 페이드 + 꼭지점 플레어 맥동)이 MPB 구동. 가산 블렌드, 큐 2999.
Shader "Testbed/SolarPrism/GlowDisc"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.2, 1.15, 0.95, 1)
        _Emission ("Emission", Range(0,6)) = 1.2
        _Opacity ("Opacity (C# driven)", Range(0,1)) = 1
        _Falloff ("Radial Falloff", Range(0.5,8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "GlowDisc"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Emission,_Opacity,_Falloff;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target{
                float2 p = IN.uv * 2.0 - 1.0;
                float r = saturate(length(p));
                float a = pow(1.0 - r, _Falloff) * _Opacity;
                if (a < 0.004) discard;
                return half4(_Color.rgb * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
