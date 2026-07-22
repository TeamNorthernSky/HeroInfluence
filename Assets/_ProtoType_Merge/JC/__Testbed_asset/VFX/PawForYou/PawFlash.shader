// JC PawForYou VFX — 원샷 플래시: 방사형 글로우 + N포인트 별 페탈. 워프 소멸/재등장·광선 착탄에 공용.
// 색은 호출부(PawFlash.Flash)가 MPB로 주입(발 변형/광선 변형 색 매칭). 가산 블렌드.
Shader "JC/VFX/PawFlash"
{
    Properties
    {
        [HDR]_Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0,10)) = 3.0
        _Falloff ("Radial Falloff", Range(0.5,8)) = 2.5
        _StarAmount ("Star Amount", Range(0,1)) = 0.5
        _StarPoints ("Star Points", Range(2,8)) = 4
        _StarSharp ("Star Sharpness", Range(1,16)) = 6
        _FadeMul ("Fade Multiplier", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity; float _Falloff;
                float _StarAmount; float _StarPoints; float _StarSharp;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);

                float radial = pow(saturate(1.0 - r), _Falloff);
                float petals = pow(abs(cos(ang * _StarPoints * 0.5)), _StarSharp);
                float star = petals * pow(saturate(1.0 - r), max(_Falloff * 0.6, 0.5));

                float v = radial + _StarAmount * star;
                float3 col = _Color.rgb * _Intensity * v * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
