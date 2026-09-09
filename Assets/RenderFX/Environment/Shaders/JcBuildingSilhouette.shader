Shader "JC/Environment/Building Silhouette"
{
    Properties
    {
        [MainColor] _FillColor ("실루엣 색상 (A = 불투명도 배율)", Color) = (0.38, 0.48, 0.58, 1)
        _JcOcclusionAlpha ("불투명도 (런타임에는 DH 가림 설정 사용)", Range(0, 1)) = 0.45
        _DepthBias ("앞쪽 물체 깊이 비교 여유 (월드 단위)", Range(0, 0.05)) = 0.002
        _OutlineColor ("윤곽선 색상 (불투명도는 건물과 동일)", Color) = (0.04, 0.055, 0.07, 1)
        _OutlineWidth ("윤곽선 두께 (화면 px, 0 = 끄기)", Range(0, 12)) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

        // 일반 투명 패스에서는 아무것도 기록하지 않는다. 별도 마스크 패스에서만 건물을 그린다.
        Pass
        {
            Name "SilhouettePlaceholder"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            { return TransformObjectToHClip(positionOS.xyz); }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "SilhouetteMask"
            Tags { "LightMode"="JcBuildingSilhouetteMask" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One One
            BlendOp Max
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                float _JcOcclusionAlpha;
                float _DepthBias;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            // 카메라별 설정은 머티리얼 밖에서 받는다. 공유 에셋은 수정하지 않는다.
            float4 _JcBuildingTuning; // x: controller enabled, y: opacity, z: depth bias, w: outline width
            half4 _JcBuildingFill;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float eyeDepth : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.eyeDepth = -TransformWorldToView(world).z;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                float sceneDepth = unity_OrthoParams.w > 0.5
                    ? LinearDepthToEyeDepth(rawDepth)
                    : LinearEyeDepth(rawDepth, _ZBufferParams);
                // 앞쪽 유닛/지형은 보존한다. 건물 자체는 카메라 깊이에 기록하지 않는다.
                float bias = _JcBuildingTuning.x > 0.5 ? _JcBuildingTuning.z : _DepthBias;
                clip(sceneDepth + bias - input.eyeDepth);
                // 모양을 알파와 분리해야 채움이 투명해도 윤곽선이 남는다.
                // 역깊이의 Max는 가장 가까운 표면을 선택한다. 카메라 깊이 버퍼는 변경하지 않는다.
                float alpha = _JcBuildingTuning.x > 0.5 ? _JcBuildingTuning.y : _JcOcclusionAlpha;
                return half4(1, saturate(alpha), rcp(max(input.eyeDepth, 0.0001)), 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SilhouetteComposite"
            Tags { "LightMode"="JcBuildingSilhouetteComposite" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, Zero One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                float _JcOcclusionAlpha;
                float _DepthBias;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            float4 _JcBuildingTuning;
            half4 _JcBuildingFill;
            half4 _JcBuildingOutline;
            float4 _JcBuildingPixelSize;
            float4 _JcBuildingOutlineSettings; // opacity, world width, width mode (0=world / 1=pixels)

            half ReadMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 maskData = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb;
                half mask = maskData.r;
                if (mask <= 0.0001h) return 0;
                half4 fill = _JcBuildingTuning.x > 0.5 ? _JcBuildingFill : _FillColor;
                half4 outline = _JcBuildingTuning.x > 0.5 ? _JcBuildingOutline : _OutlineColor;
                float width = _JcBuildingTuning.x > 0.5 ? _JcBuildingTuning.w : _OutlineWidth;
                float fillOpacity = _JcBuildingTuning.x > 0.5 ? _JcBuildingTuning.y : maskData.g / mask;
                fillOpacity = saturate(fillOpacity * fill.a);
                float outlineOpacity = _JcBuildingTuning.x > 0.5 ? _JcBuildingOutlineSettings.x : fillOpacity;
                if (_JcBuildingTuning.x > 0.5 && _JcBuildingOutlineSettings.z < 0.5)
                {
                    // 마스크 비율로 깊이를 복원하여 경계의 선형 필터 영향을 상쇄한다.
                    float buildingDepth = mask / max(maskData.b, 0.000001);
                    float distanceScale = _JcBuildingPixelSize.w > 0.5 ? 1.0 : buildingDepth;
                    width = _JcBuildingOutlineSettings.y * _JcBuildingPixelSize.z / distanceScale;
                }
                half inner = mask;
                if (width > 0.0)
                {
                    // 원형 이웃으로 마스크를 축소하여 경계 안쪽 띠를 얻는다.
                    // 개별 메시의 노멀/깊이를 쓰지 않으므로 부품 경계가 다시 나타나지 않는다.
                    float2 radius = _JcBuildingPixelSize.xy * width;
                    [unroll] for (int i = 0; i < 16; i++)
                    {
                        float angle = i * (6.28318530718 / 16.0);
                        float2 direction = float2(cos(angle), sin(angle));
                        inner = min(inner, ReadMask(input.texcoord + direction * radius));
                    }
                }
                half edge = saturate((mask - inner) / mask);
                // 경계의 AA를 프리멀티플라이드 색으로 보간한 뒤 한 번만 합성한다.
                // 채움 0 / 윤곽선 1도 가능하고, 두 알파가 같으면 기존 결과와 같다.
                half fillWeight = (1 - edge) * fillOpacity;
                half outlineWeight = edge * saturate(outlineOpacity);
                half alpha = fillWeight + outlineWeight;
                half3 color = (fill.rgb * fillWeight + outline.rgb * outlineWeight) / max(alpha, 0.0001h);
                return half4(color, saturate(mask * alpha));
            }
            ENDHLSL
        }
        // 화면의 채움/윤곽선 알파와 무관하게 원래 메시로 그림자를 기록한다.
        // 기존 패스 인덱스를 유지하기 위해 마지막에 추가한다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            // URP 표준 그림자 바이어스/클리핑을 사용한다. 알파 클립은 적용하지 않는다.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowlessComposite"
            Tags { "LightMode"="JcShadowlessComposite" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_JcShadowRemovalMask);
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // 기존 마스크는 건물보다 앞인 표면을 깊이 검사로 제외한 상태다.
                half mask = SAMPLE_TEXTURE2D_X(_JcShadowRemovalMask, sampler_LinearClamp, input.texcoord).r;
                half4 surface = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                return half4(surface.rgb, mask * surface.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
