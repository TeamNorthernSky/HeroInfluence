Shader "Custom/JC/FogStencilPrepass"
{
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FogStencilPrepass"

            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask 0

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_VisibilityCurrentTex);
            SAMPLER(sampler_VisibilityCurrentTex);
            TEXTURE2D(_VisibilityExploredTex);
            SAMPLER(sampler_VisibilityExploredTex);

            float4 _GridWorldSize; // (worldW, worldH, 1/worldW, 1/worldH)
            float _FogHidableLowThreshold; // Low 레이어 visibility가 이 값 이하이면 완전 Fogged 취급

            // Fullscreen triangle (SV_VertexID 기반, 메시 불필요)
            Varyings vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                float2 xy = float2((vertexID == 1) ? 3.0 : -1.0,
                                   (vertexID == 2) ? 3.0 : -1.0);
                o.positionCS = float4(xy, 0.0, 1.0);
                o.uv = float2((vertexID == 1) ? 2.0 : 0.0,
                              (vertexID == 2) ? 2.0 : 0.0);
                #if UNITY_UV_STARTS_AT_TOP
                    o.uv.y = 1.0 - o.uv.y;
                #endif
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float depth = SampleSceneDepth(i.uv);

                #if UNITY_REVERSED_Z
                    bool isSky = depth < 0.0001;
                #else
                    bool isSky = depth > 0.9999;
                #endif
                if (isSky) discard;

                float2 posNDC = i.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    posNDC.y = -posNDC.y;
                #endif

                float4 worldPos4 = mul(UNITY_MATRIX_I_VP, float4(posNDC, depth, 1.0));
                float3 worldPos = worldPos4.xyz / worldPos4.w;

                float2 gridUV = worldPos.xz * _GridWorldSize.zw;

                // 맵 밖 → Fogged 취급
                bool inside = gridUV.x >= 0.0 && gridUV.x <= 1.0
                           && gridUV.y >= 0.0 && gridUV.y <= 1.0;

                if (!inside) return half4(0, 0, 0, 0);

                // 현재 Revealer 시야 내 → 렌더 (stencil 안 씀)
                float current = SAMPLE_TEXTURE2D(_VisibilityCurrentTex, sampler_VisibilityCurrentTex, gridUV).r;
                if (current > 0.5) discard;

                // Low 레이어 visibility 체크
                // explored.rgb = per-layer visibility (1=fresh, 0=fully decayed)
                // explored.r = Low 레이어
                // Low가 충분히 감쇠 완료 → 완전 Fogged → stencil 씀
                // Low가 아직 fresh 또는 감쇠 중 → 유닛 렌더 (stencil 안 씀)
                float lowVis = SAMPLE_TEXTURE2D(_VisibilityExploredTex, sampler_VisibilityExploredTex, gridUV).r;
                if (lowVis >= _FogHidableLowThreshold) discard;

                // 완전 Fogged 확정 → stencil = 1
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
