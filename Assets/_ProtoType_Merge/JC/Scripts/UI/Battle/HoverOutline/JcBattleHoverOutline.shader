Shader "JC/Battle/Hover Outline"
{
    Properties
    {
        _BaseMap ("알파 판정 원본 텍스처", 2D) = "white" {}
        _AlphaClip ("원본 알파 잘라내기 사용", Float) = 0
        _Cutoff ("원본 알파 임계값", Float) = 0.5
        _SourceAlpha ("원본 색상 알파", Float) = 1
        // JC | 2026-11-06 | 회복 기둥용 마스크만 실제로 보이는 캐릭터 표면과 깊이 일치를 요구합니다.
        _RequireDepthMatch ("장면 깊이 일치 사용", Float) = 0
        _OutlineColor ("호버 외곽선 색상", Color) = (1,0.08,0.06,1)
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
            float _RequireDepthMatch;
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
                // JC | 2026-11-06 | 호버 경로는 유지하고 스킬 마스크에 가상 앞쪽 표면이 섞이지 않게 합니다.
                if (_RequireDepthMatch > .5) clip(.003 - abs(sceneDepth - input.eyeDepth));
                return 1;
            }
            ENDHLSL
        }
        // JC | 2026-11-16 | 16방향 실루엣 복사 대신 가로 최단 거리와 세로 합성을 분리합니다.
        // 가는 머리카락의 확장 복사본이 바깥 수렴선에 톱니처럼 반복되는 것을 막습니다. 원본 마스크 패스는 유지합니다.
        Pass
        {
            Name "HoverHorizontalDistance"
            Cull Off ZWrite Off ZTest Always
            Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment HorizontalFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _HoverMetrics, _HoverTexel, _HoverRect;
            float HorizontalFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float limit = _HoverMetrics.w + 1;
                // JC | 2026-11-16 | 영역 밖도 거리 상한으로 기록하여 이전 프레임의 흔적을 없애고 텍스처 탐색은 생략합니다.
                if (any(input.positionCS.xy < _HoverRect.xy) || any(input.positionCS.xy > _HoverRect.zw)) return limit;
                float nearest = limit;
                int radius = (int)_HoverMetrics.w;
                [loop] for (int x = -radius; x <= radius; x++)
                {
                    float2 uv = input.texcoord + float2(x * _HoverTexel.x, 0);
                    if (any(uv < 0) || any(uv > 1)) continue;
                    if (SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r > .5)
                        nearest = min(nearest, abs((float)x));
                }
                return nearest;
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
            // JC | 2026-11-16 | 본선·수렴선·후광이 하나의 연속 거리장을 공유하고 경계 약 한 픽셀만 부드럽게 합니다.
            float4 _HoverMetrics, _HoverTexel, _HoverRect;
            TEXTURE2D_X(_JcHoverSourceMask);
            half ReadMask(float2 uv)
            {
                // 화면 밖을 clamp하면 화면 경계에 잘못된 선이 생길 수 있습니다.
                if (any(uv < 0) || any(uv > 1)) return 0;
                return SAMPLE_TEXTURE2D_X(_JcHoverSourceMask, sampler_LinearClamp, uv).r;
            }
            half4 CompositeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (any(input.positionCS.xy < _HoverRect.xy) || any(input.positionCS.xy > _HoverRect.zw)) return 0;
                float nearestSquared = (_HoverMetrics.w + 1) * (_HoverMetrics.w + 1);
                int radius = (int)_HoverMetrics.w;
                [loop] for (int y = -radius; y <= radius; y++)
                {
                    float2 uv = input.texcoord + float2(0, y * _HoverTexel.y);
                    if (any(uv < 0) || any(uv > 1)) continue;
                    float dx = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
                    nearestSquared = min(nearestSquared, dx * dx + y * y);
                }
                float distance = max(0, sqrt(nearestSquared) - .5);
                float width = _HoverMetrics.x;
                float outerRadius = width + _HoverMetrics.y;
                half edge = 1 - smoothstep(width - .65, width + .65, distance);
                half outer = 1 - smoothstep(outerRadius - .65, outerRadius + .65, distance);
                half inner = 1 - smoothstep(max(width, outerRadius - _HoverTexel.z) - .65, max(width, outerRadius - _HoverTexel.z) + .65, distance);
                half halo = 1 - smoothstep(outerRadius + _HoverTexel.z - .65, outerRadius + _HoverTexel.z + .65, distance);
                // JC | 2026-11-16 | 원본 형상 전체를 흐리지 않고 안쪽 접점의 1픽셀 계단만 완화합니다.
                float2 d = _HoverTexel.xy * .35;
                half center = (ReadMask(input.texcoord + d) + ReadMask(input.texcoord - d) +
                    ReadMask(input.texcoord + float2(d.x, -d.y)) + ReadMask(input.texcoord + float2(-d.x, d.y))) * .25;
                half wave = saturate(outer - max(inner, edge));
                half softGlow = saturate(halo - max(outer, edge)) * .3;
                half alpha = saturate(edge + (wave * .8 + softGlow) * _HoverMetrics.z) * (1 - center);
                return half4(_OutlineColor.rgb, alpha * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
