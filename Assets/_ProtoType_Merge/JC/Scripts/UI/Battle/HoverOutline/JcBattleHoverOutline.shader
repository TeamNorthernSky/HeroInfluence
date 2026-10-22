Shader "JC/Battle/Hover Outline"
{
    Properties
    {
        _BaseMap ("알파 판정 원본 텍스처", 2D) = "white" {}
        _AlphaClip ("원본 알파 잘라내기 사용", Float) = 0
        _Cutoff ("원본 알파 임계값", Float) = 0.5
        _SourceAlpha ("원본 색상 알파", Float) = 1
        _OutlineColor ("호버 외곽선 색상", Color) = (1,0.08,0.06,1)
        _Convergence ("수렴 거리와 글로우 세기 (런타임 계산)", Vector) = (0,0,0,0)
        _OutlineStep ("화면 픽셀 두께의 UV 변환값", Vector) = (0.001,0.001,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "HoverMask"
            Cull Off ZWrite Off ZTest Always
            Blend One One
            BlendOp Max
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MaskVert
            #pragma fragment MaskFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float _AlphaClip, _Cutoff, _SourceAlpha;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float eyeDepth : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings MaskVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.eyeDepth = -TransformWorldToView(world).z;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }
            half4 MaskFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_AlphaClip > 0.5) clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _SourceAlpha - _Cutoff);
                float depth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                float sceneDepth = unity_OrthoParams.w > 0.5 ? LinearDepthToEyeDepth(depth) : LinearEyeDepth(depth, _ZBufferParams);
                clip(sceneDepth + 0.003 - input.eyeDepth);
                return 1;
            }
            ENDHLSL
        }
        Pass
        {
            Name "HoverComposite"
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment CompositeFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 _OutlineColor;
            float4 _OutlineStep;
            float4 _Convergence;
            half ReadMask(float2 uv)
            {
                // 화면 밖을 clamp하면 화면 경계에 잘못된 선이 생길 수 있습니다.
                if (any(uv < 0) || any(uv > 1)) return 0;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            }
            half4 CompositeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half center = ReadMask(input.texcoord);
                half expanded = center;
                half waveOuter = center, waveInner = center, halo = center;
                [unroll] for (int i = 0; i < 16; i++)
                {
                    float angle = i * (6.28318530718 / 16.0);
                    float2 stepUV = float2(cos(angle), sin(angle)) * _OutlineStep.xy;
                    expanded = max(expanded, ReadMask(input.texcoord + stepUV));
                    expanded = max(expanded, ReadMask(input.texcoord + stepUV * 0.5));
                    if (_Convergence.z > 0.001)
                    {
                        float radius = 1 + _Convergence.x;
                        waveOuter = max(waveOuter, ReadMask(input.texcoord + stepUV * radius));
                        waveInner = max(waveInner, ReadMask(input.texcoord + stepUV * max(1, radius - _Convergence.y)));
                        halo = max(halo, ReadMask(input.texcoord + stepUV * (radius + _Convergence.y)));
                    }
                }
                half edge = saturate(expanded - center);
                half wave = saturate(waveOuter - max(waveInner, expanded));
                half softGlow = saturate(halo - max(waveOuter, expanded)) * 0.3;
                half alpha = saturate(edge + (wave * 0.8 + softGlow) * _Convergence.z);
                return half4(_OutlineColor.rgb, alpha * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
