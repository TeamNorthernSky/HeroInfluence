// JC Taosenaiyo VFX — 깃털: 절차 SDF 빌보드(베시카 실루엣 + 샤프트 + 깃가지 결).
// 베시카(두 원 교집합)=잎사귀형 깃털, 도메인 벤드로 살짝 휨. 추후 텍스처 교체 전제(실루엣만 치환).
// 빌보드/모션/페이드는 스크립트(TaoFeatherBurst)가 처리. 가산 블렌드.
Shader "JC/VFX/TaoFeather"
{
    Properties
    {
        [HDR]_Color ("Feather Color", Color) = (1.0, 0.9, 0.55, 1)
        [HDR]_CoreColor ("Shaft Color", Color) = (1, 1, 0.9, 1)
        [HDR]_RimColor ("Rim Color", Color) = (1.0, 0.8, 0.3, 1)
        _Intensity ("Intensity", Range(0,8)) = 2.2
        [Header(Shape)]
        _CircleOff ("Vesica Circle Offset", Range(0.1,0.9)) = 0.55
        _CircleR ("Vesica Circle Radius", Range(0.3,1.2)) = 0.95
        _Bend ("Bend", Range(-0.6,0.6)) = 0.22
        _EdgeSoft ("Edge Softness", Range(0.002,0.2)) = 0.03
        [Header(Detail)]
        _ShaftWidth ("Shaft Width", Range(0.005,0.15)) = 0.035
        _BarbFreq ("Barb Frequency", Range(0,60)) = 22
        _BarbAmount ("Barb Amount", Range(0,0.8)) = 0.3
        _RimWidth ("Rim Width", Range(0.005,0.15)) = 0.04
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
                float4 _Color; float4 _CoreColor; float4 _RimColor;
                float _Intensity;
                float _CircleOff; float _CircleR; float _Bend; float _EdgeSoft;
                float _ShaftWidth; float _BarbFreq; float _BarbAmount; float _RimWidth;
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
                // 도메인 벤드: 깃대가 살짝 휘어진 곡선
                float2 q = p;
                q.x += _Bend * q.y * q.y;

                // 베시카(두 원 교집합) — 세로로 긴 잎/깃털 실루엣
                float d = max(length(q - float2(_CircleOff, 0.0)) - _CircleR,
                              length(q + float2(_CircleOff, 0.0)) - _CircleR);
                float fill = 1.0 - smoothstep(0.0, max(_EdgeSoft, 1e-4), d);
                float rim = 1.0 - smoothstep(_RimWidth * 0.4, _RimWidth, abs(d));

                // 샤프트(깃대) + 깃가지 결(사선 줄무늬 미세 변조)
                float shaft = 1.0 - smoothstep(_ShaftWidth, _ShaftWidth * 2.0, abs(q.x));
                float barb = 0.5 + 0.5 * sin(q.y * _BarbFreq + abs(q.x) * _BarbFreq * 0.8);
                float barbMod = lerp(1.0 - _BarbAmount, 1.0 + _BarbAmount, barb);

                float3 col = (_Color.rgb * fill * barbMod
                            + _CoreColor.rgb * shaft * fill
                            + _RimColor.rgb * rim)
                           * _Intensity * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
