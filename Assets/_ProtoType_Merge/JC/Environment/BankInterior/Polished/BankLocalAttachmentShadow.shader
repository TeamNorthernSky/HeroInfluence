// JC | 2026-11-13 | 벽 부착물은 실내 스팟·포인트 그림자만 투사합니다.
// 표시용 재질과 공용 URP 설정을 바꾸지 않고 ShadowsOnly 메시에서만 사용합니다.
Shader "JC/Bank/Local Attachment Shadow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment LocalShadowFragment
            #pragma multi_compile_instancing
            // JC | 2026-11-13 | URP가 광원별로 설정하는 키워드를 프래그먼트에도 전달하여 주광 깊이 기록을 차단합니다.
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            half4 LocalShadowFragment(Varyings input) : SV_TARGET
            {
                #if !defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    clip(-1);
                #endif
                return ShadowPassFragment(input);
            }
            ENDHLSL
        }
    }
}
