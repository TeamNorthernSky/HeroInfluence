// JC | 2026-11-11 | 월드 후방 거리로 연속 착란원을 계산합니다. 전해상도 색/반경 준비 후 깊이가 선명한 픽셀을 제외하여 원형으로 적분합니다.
Shader "Hidden/JC/Battle/Two Stage Rear Blur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X(_JcRearSource);
        float4 _RearPlane, _DepthBands, _Radii, _SourceTexelSize;
        float Radius(float2 uv)
        {
            float raw = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, UnityStereoTransformScreenSpaceTex(uv), 0).r;
            // JC | 2026-11-11 | 하늘은 카메라 원평면에서 후방 여부를 판정하여 먼 울타리의 실루엣도 자연스럽게 흐리게 합니다.
            #if UNITY_REVERSED_Z
                raw = max(raw, 0.000001);
            #else
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, min(raw, 0.999999));
            #endif
            float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
            float distance = dot(_RearPlane.xyz, world) + _RearPlane.w;
            float near = smoothstep(_DepthBands.x, _DepthBands.y, distance);
            float far = smoothstep(_DepthBands.z, _DepthBands.w, distance);
            float mild = _Radii.x * near;
            return lerp(mild, max(mild, _Radii.y), far * _Radii.z) * _Radii.w;
        }
        float4 Prepare(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float radius = Radius(uv);
            float3 original = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
            if (radius <= .001) return float4(original, 0);
            // JC | 2026-11-11 | 큰 착란원의 희소 샘플로 얇은 도색/울타리가 점·줄로 반복되지 않게 1픽셀 텐트 필터를 준비합니다.
            // 색/깊이는 같은 픽셀에서 읽고 중앙 가중치 4를 항상 유지하므로 작은 알파로 나누는 흰점 증폭 경로가 없습니다.
            float3 sum = original * 4;
            float total = 4;
            [unroll] for (int y = -1; y <= 1; y++) [unroll] for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                float2 tap = uv + float2(x,y) * _SourceTexelSize.xy;
                float w = (x == 0 || y == 0 ? 2 : 1) * saturate(Radius(tap));
                sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, tap, 0).rgb * w;
                total += w;
            }
            return float4(lerp(original, sum / total, saturate(radius - 1)), radius);
        }
        float4 Bokeh(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float4 original = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            float4 center = SAMPLE_TEXTURE2D_X_LOD(_JcRearSource, sampler_PointClamp, uv, 0);
            float radius = center.a;
            if (radius <= .001) return original;
            float3 sum = center.rgb;
            float total = 1;
            #if defined(_HIGH_QUALITY)
                const int count = 96;
            #else
                const int count = 24;
            #endif
            [loop] for (int i = 0; i < count; i++)
            {
                float r = sqrt((i + .5) / count);
                float angle = i * 2.39996323;
                float2 offset = float2(cos(angle), sin(angle)) * (radius * r);
                float diskWeight = exp(-2 * r * r);
                float2 sampleUV = uv + offset * _SourceTexelSize.xy;
                if (any(sampleUV < 0) || any(sampleUV > 1)) continue;
                float4 smoothSource = SAMPLE_TEXTURE2D_X_LOD(_JcRearSource, sampler_LinearClamp, sampleUV, 0);
                float maximum = lerp(_Radii.x, max(_Radii.x, _Radii.y), _Radii.z) * _Radii.w;
                // JC | 2026-11-11 | 반경이 포화된 배경은 네 이웃 모두 흐림 영역이므로 하드웨어 보간 한 번으로 처리합니다.
                // 전경 경계/전환 구간만 아래의 개별 보간을 사용하여 높은 품질의 비용을 제한합니다.
                if (smoothSource.a >= maximum - .001)
                {
                    sum += smoothSource.rgb * diskWeight;
                    total += diskWeight;
                    continue;
                }
                float2 pixel = (uv + offset * _SourceTexelSize.xy) * _SourceTexelSize.zw - .5;
                float2 basePixel = floor(pixel);
                float2 fraction = frac(pixel);
                // JC | 2026-11-11 | 색과 반경을 4개 동일 픽셀에서 수동 보간합니다. 선명한 캐릭터의 색이 배경 보케에 섞이는 것을 깊이만으로 억제합니다.
                [unroll] for (int y = 0; y <= 1; y++) [unroll] for (int x = 0; x <= 1; x++)
                {
                    float2 tap = (basePixel + float2(x,y) + .5) * _SourceTexelSize.xy;
                    if (any(tap < 0) || any(tap > 1)) continue;
                    float4 source = SAMPLE_TEXTURE2D_X_LOD(_JcRearSource, sampler_PointClamp, tap, 0);
                    float reach = min(radius, source.a);
                    float w = (x == 0 ? 1-fraction.x : fraction.x) * (y == 0 ? 1-fraction.y : fraction.y);
                    w *= saturate(reach - length((tap - uv) * _SourceTexelSize.zw) + 1) * saturate(source.a) * diskWeight;
                    sum += source.rgb * w;
                    total += w;
                }
            }
            return float4(sum / total, original.a);
        }
        ENDHLSL
        Pass
        {
            Name "WorldCircleOfConfusion"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Prepare
            ENDHLSL
        }
        Pass
        {
            Name "WorldRearBokeh"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Bokeh
            #pragma multi_compile_local_fragment _ _HIGH_QUALITY
            ENDHLSL
        }
    }
}
