Shader "Custom/DH/FogDecay"
{
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "DHFogDecayPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_ExploredTex);
            SAMPLER(sampler_ExploredTex);

            TEXTURE2D(_CurrentTex);
            SAMPLER(sampler_CurrentTex);

            float4 _RestoreDelays;
            float _RestoreDuration;
            float _FogDeltaTime;

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 prev = SAMPLE_TEXTURE2D(_ExploredTex, sampler_ExploredTex, input.uv);
                float3 prevRGB = prev.rgb;
                float prevElapsed = prev.a;
                float current = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, input.uv).r;

                if (current > 0.001)
                    return half4(1, 1, 1, 0);

                bool hasBeenSeen = (prevRGB.r > 0.001) || (prevRGB.g > 0.001) || (prevRGB.b > 0.001) || (prevElapsed > 0.001);
                if (!hasBeenSeen)
                    return half4(0, 0, 0, 0);

                float elapsed = min(prevElapsed + _FogDeltaTime, 100000.0);
                float duration = max(0.01, _RestoreDuration);
                float3 progress = saturate((elapsed.xxx - _RestoreDelays.xyz) / duration);
                float3 visibility = float3(1, 1, 1) - progress;
                return half4(visibility, elapsed);
            }
            ENDHLSL
        }
    }
}
