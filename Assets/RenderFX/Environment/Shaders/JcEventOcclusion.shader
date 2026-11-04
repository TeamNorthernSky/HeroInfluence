Shader "JC/Environment/Event Occlusion"
{
    Properties
    {
        _Opacity("가림 표시 불투명도", Range(0,1)) = .25
        _GhostMap("Alpha source", 2D) = "white" {}
        _GhostTint("Alpha tint", Color) = (1,1,1,1)
        _GhostCutoff("Alpha cutoff", Float) = .001
        _GhostCull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-30" "RenderType"="Transparent" }
        // JC | 2026-11-04 | 원래 색·텍스처를 기록한 이미지에서 건물에 가려진 픽셀만 합성합니다.
        Pass
        {
            Name "EventOcclusionComposite"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_JcEventGhostColor); SAMPLER(sampler_JcEventGhostColor);
            TEXTURE2D(_JcEventGhostDepth); SAMPLER(sampler_JcEventGhostDepth);
            TEXTURE2D(_JcEventSurfaceDepth); SAMPLER(sampler_JcEventSurfaceDepth);
            float _JcEventGhostActive, _JcEventGhostOpacity;
            CBUFFER_START(UnityPerMaterial)
                float _Opacity; float4 _GhostMap_ST, _GhostTint; float _GhostCutoff, _GhostCull;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=float4(v.positionOS.xy,0,1); return o; }
            half4 Frag(Varyings v):SV_Target
            {
                clip(_JcEventGhostActive-.5);
                float2 uv=GetNormalizedScreenSpaceUV(v.positionCS);
                half4 color=SAMPLE_TEXTURE2D(_JcEventGhostColor,sampler_JcEventGhostColor,uv);
                clip(color.a-.001);
                float raw=SampleSceneDepth(uv);
                float objectRaw=SAMPLE_TEXTURE2D(_JcEventGhostDepth,sampler_JcEventGhostDepth,uv).r;
                float scene=unity_OrthoParams.w>.5?LinearDepthToEyeDepth(raw):LinearEyeDepth(raw,_ZBufferParams);
                float objectDepth=unity_OrthoParams.w>.5?LinearDepthToEyeDepth(objectRaw):LinearEyeDepth(objectRaw,_ZBufferParams);
                clip(objectDepth-scene-.004);
                float4 surface=SAMPLE_TEXTURE2D(_JcEventSurfaceDepth,sampler_JcEventSurfaceDepth,uv);
                float tolerance=max(.004,scene*.00003);
                // JC | 2026-11-04 | 인물·지면에 의한 가림은 유지하고 실제 앞쪽 표면이 건물일 때만 표시합니다.
                float nearest=min(abs(surface.r-scene),min(abs(surface.b-scene),abs(surface.a-scene)));
                clip(tolerance-nearest);
                // JC | 2026-11-04 | 렌더 준비 이후 변경된 초안도 같은 프레임에 적용하도록 카메라별 합성 배율을 사용합니다.
                return color*saturate(_JcEventGhostOpacity);
            }
            ENDHLSL
        }
        Pass
        {
            Name "EventObjectDepth"
            Tags { "LightMode"="JcEventObjectDepth" }
            Cull [_GhostCull] ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_GhostMap); SAMPLER(sampler_GhostMap);
            CBUFFER_START(UnityPerMaterial)
                float _Opacity; float4 _GhostMap_ST, _GhostTint; float _GhostCutoff, _GhostCull;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=TRANSFORM_TEX(v.uv,_GhostMap); return o; }
            half4 Frag(Varyings v):SV_Target { clip(SAMPLE_TEXTURE2D(_GhostMap,sampler_GhostMap,v.uv).a*_GhostTint.a-_GhostCutoff); return 0; }
            ENDHLSL
        }
    }
}
