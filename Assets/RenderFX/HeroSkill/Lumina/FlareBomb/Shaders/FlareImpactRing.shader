// 루미나 「플레어 봄」 탄착 지면 충격 링 — 지면(XZ)에 눕힌 쿼드 위 확장 링.
// _Progress(0..1)를 C#(FlareBombImpact)이 MPB로 구동. 반경이 easeOutCubic으로 확장하며
// _FadeStart부터 페이드. 가산 블렌드, 큐 3004(버스트 바로 아래).
Shader "Testbed/FlareBomb/FlareImpactRing"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorRing ("Ring Color", Color) = (1.25, 0.78, 0.3, 1)
        _Emission ("Emission", Range(0,6)) = 1.4

        [Header(Progress)]
        _Progress ("Progress (C# driven)", Range(0,1)) = 0.35
        _FadeStart ("Fade Start (progress)", Range(0,1)) = 0.35

        [Header(Ring)]
        _RingMaxR ("Ring Max Radius (uv)", Range(0.1,1)) = 0.85
        _RingWidth ("Ring Band Width (uv)", Range(0.01,0.4)) = 0.09
        _InnerGlow ("Inner Glow Amount", Range(0,1)) = 0.25

        [Header(Global)]
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareImpactRing"
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
                half4 _ColorRing;
                half _Emission;
                half _Progress,_FadeStart;
                half _RingMaxR,_RingWidth,_InnerGlow;
                half _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target{
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);

                float grow = 1.0 - pow(1.0 - saturate(_Progress), 3.0);   // easeOutCubic
                float R = _RingMaxR * grow;
                float fade = 1.0 - smoothstep(_FadeStart, 1.0, _Progress);

                // 확장 링(가우시안 밴드) + 내부 잔광
                float d = (r - R) / max(_RingWidth, 0.005);
                float band = exp(-d * d);
                float inner = _InnerGlow * saturate(1.0 - r / max(R, 0.01)) * grow;
                float a = saturate(band + inner) * fade * _Opacity;

                if (a < 0.005) discard;
                return half4(_ColorRing.rgb * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
