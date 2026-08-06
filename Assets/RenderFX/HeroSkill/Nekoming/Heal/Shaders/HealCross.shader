// JC 힐 스킬 VFX — 녹색 십자(E-4): 절차적 플러스(+) 기호 빌보드.
// 가산·양면(Cull Off)·★ZTest Always(캐릭터 메시에 가리지 않고 항상 위에, 260806). 빌보드 회전은 스크립트(HealCrossBurst)가 처리.
// 두 개의 소프트 사각 바(가로/세로)를 max로 합쳐 십자. 텍스처 의존 없음.
Shader "JC/VFX/HealCross"
{
    Properties
    {
        [HDR]_Color ("Cross Color", Color) = (0.35, 1.0, 0.45, 1)
        _Intensity ("Intensity", Range(0,8)) = 2.2
        _BarWidth ("Bar Width (팔 두께)", Range(0.02,0.5)) = 0.13
        _BarLength ("Bar Length (팔 길이)", Range(0.1,0.5)) = 0.42
        _Softness ("Edge Softness", Range(0.001,0.3)) = 0.06
        _FadeMul ("Fade Multiplier", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest Always
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
                float _Intensity; float _BarWidth; float _BarLength; float _Softness;
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
                float2 d = abs(IN.uv - 0.5);
                float s = max(_Softness, 1e-4);
                // 가로 바: |y|<width & |x|<length. 세로 바: |x|<width & |y|<length.
                float hb = (1.0 - smoothstep(_BarWidth, _BarWidth + s, d.y))
                         * (1.0 - smoothstep(_BarLength, _BarLength + s, d.x));
                float vb = (1.0 - smoothstep(_BarWidth, _BarWidth + s, d.x))
                         * (1.0 - smoothstep(_BarLength, _BarLength + s, d.y));
                float cross = max(hb, vb);
                float3 col = _Color.rgb * _Intensity * cross * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
