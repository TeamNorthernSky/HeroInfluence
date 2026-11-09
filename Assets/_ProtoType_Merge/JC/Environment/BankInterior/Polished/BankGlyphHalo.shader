// JC | 2026-11-05 | 문양 실루엣의 거리 마스크로 벽에 흰 네온 번짐을 만듭니다. 깊이 검사를 유지해 문양/유닛 앞을 덮지 않습니다.
Shader "JC/Bank/GlyphWallHalo"
{
    Properties
    {
        _MainTex ("문양 거리 마스크", 2D) = "white" {}
        _Tint ("후광 색상", Color) = (1,1,1,1)
        _Intensity ("후광 밝기", Range(0,4)) = 0
        _Width ("번짐 폭", Range(0.03,0.5)) = 0.23
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Intensity, _Width;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.uv=a.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                // JC | 2026-11-05 | R은 윤곽까지 거리(1m 정규화)이며 실루엣 내부는 0입니다.
                float distance=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).r;
                float falloff=exp2(-4*distance*distance/max(.0009,_Width*_Width));
                return half4(_Tint.rgb*_Intensity*falloff,0);
            }
            ENDHLSL
        }
    }
}
