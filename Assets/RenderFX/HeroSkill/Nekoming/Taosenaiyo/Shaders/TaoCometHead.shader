// JC Taosenaiyo VFX — 혜성 머리 코마: 구형 메시 프레넬 셸.
// 어느 각도에서 봐도 동그란 윤곽 — 평면 리본 위 링이 정면에서 무너지는 문제의 해법.
// 필(중심, |NdotV|↑) + 림(실루엣, 1-|NdotV|↑) 2층. 페이드는 ProjectileVfx.fadeRenderers(_FadeMul) 편승.
Shader "JC/VFX/TaoCometHead"
{
    Properties
    {
        [HDR]_FillColor ("Fill Color", Color) = (1, 0.98, 0.85, 1)
        _FillIntensity ("Fill Intensity", Range(0,8)) = 0.8
        _FillPower ("Fill Power", Range(0.3,8)) = 1.5
        [HDR]_RimColor ("Rim Color", Color) = (1, 0.95, 0.7, 1)
        _RimIntensity ("Rim Intensity", Range(0,10)) = 2.5
        _RimPower ("Rim Power", Range(0.3,16)) = 2.5
        _FadeMul ("Fade Multiplier", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor; float _FillIntensity; float _FillPower;
                float4 _RimColor; float _RimIntensity; float _RimPower;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float ndv = saturate(abs(dot(normalize(IN.normalWS), normalize(IN.viewDirWS))));
                float fill = pow(ndv, _FillPower) * _FillIntensity;
                float rim = pow(1.0 - ndv, _RimPower) * _RimIntensity;
                float3 col = (_FillColor.rgb * fill + _RimColor.rgb * rim) * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
