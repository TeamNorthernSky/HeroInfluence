// JC PawForYou VFX — 워프 블링크: 한줄기 빛기둥(록맨 텔레포트풍).
// 단면 = 코어(고휘도 중심) + 글로우(외곽 감쇠). 수직 = _Sweep(하단 경계가 위로 빠져나간 정도) + 상단 소프트.
// 타임라인(플래시 → 위로 슈릭 소멸)은 스크립트(PawWarpPillar)가 _Sweep/_FadeMul로 구동. 가산 블렌드.
Shader "JC/VFX/PawWarpPillar"
{
    Properties
    {
        [HDR]_CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        [HDR]_GlowColor ("Glow Color", Color) = (0.45, 0.8, 1, 1)
        _Intensity ("Intensity", Range(0,10)) = 3.0
        _CoreWidth ("Core Width", Range(0.01,0.6)) = 0.22
        _GlowFalloff ("Glow Falloff", Range(0.5,8)) = 2.2
        _EdgeSoft ("Core Edge Softness", Range(0.005,0.3)) = 0.08
        [Header(Vertical)]
        _Sweep ("Sweep Up Progress", Range(0,1)) = 0.0
        _SweepSoft ("Sweep Edge Softness", Range(0.01,0.6)) = 0.18
        _TopFade ("Top Fade", Range(0.01,0.6)) = 0.2
        _FadeMul ("Fade Multiplier", Range(0,3)) = 1.0
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
                float4 _CoreColor; float4 _GlowColor;
                float _Intensity; float _CoreWidth; float _GlowFalloff; float _EdgeSoft;
                float _Sweep; float _SweepSoft; float _TopFade;
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
                float x2 = abs(IN.uv.x - 0.5) * 2.0;
                float glow = pow(saturate(1.0 - x2), _GlowFalloff);
                float core = 1.0 - smoothstep(_CoreWidth, _CoreWidth + _EdgeSoft, x2);

                // 하단 경계가 _Sweep 높이까지 올라가며 빛이 위로 빠져나감 + 상단 소프트
                float vert = smoothstep(_Sweep, _Sweep + _SweepSoft, IN.uv.y)
                           * smoothstep(0.0, _TopFade, 1.0 - IN.uv.y);

                float3 col = (_CoreColor.rgb * core + _GlowColor.rgb * glow)
                           * _Intensity * vert * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
