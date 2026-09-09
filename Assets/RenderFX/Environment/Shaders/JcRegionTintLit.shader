// ★260908 멘토링(260902) 반영 — 좌표 기반 오버레이 컬러링 (P1).
//
// 모델은 색을 굽지 않는다(무채색 명도만). 색은 두 축이 결정한다:
//   A축 (오브젝트 내부, 로컬 Y) — 하단색→상단색 그라데이션 + 하단 음영. 스케일 지터와 무관하게 "나무 한 그루" 안에서 일정.
//   B축 (지역 필드, 월드 XZ)  — JcRegionPaletteController 가 전역 텍스처 _JcRegionTex 로 밀어 넣는 지역색.
//                                RGB = 목표 색조, A = 존 커버리지(페더). 명도는 A축 결과를 보존하고 색조만 갈아탄다(hue transfer).
// 라이팅은 URP Lit 그대로(UniversalFragmentPBR) — 그림자·SH 앰비언트·반사 프로브·SSAO 전부 받는다.
// ShadowCaster/DepthOnly/DepthNormals 는 Lit 의 패스를 UsePass 로 빌려 쓴다(KJ_ToonBase 와 같은 관행).
Shader "JC/Environment/Region Tint Lit"
{
    Properties
    {
        [Header(Base  Achromatic)]
        [MainTexture] _BaseMap ("명도 맵 (무채색, 비우면 흰색)", 2D) = "white" {}
        [MainColor]   _BaseColor ("기본 곱색 (보통 흰색 유지)", Color) = (1, 1, 1, 1)

        [Header(A  Object Height Gradient)]
        _BottomColor ("하단색", Color) = (0.13, 0.32, 0.15, 1)
        _TopColor ("상단색", Color) = (0.66, 0.86, 0.32, 1)
        _GradientHeight ("그라데이션 높이 (메시 로컬 단위)", Float) = 2.5
        _GradientBias ("하단 기준 오프셋 (피벗이 중앙인 메시 보정)", Float) = 0
        _GradientPower ("곡률 (1=선형, >1 하단 넓게)", Range(0.2, 4)) = 1.2
        _BottomShade ("하단 음영 (0=없음)", Range(0, 1)) = 0.35

        [Header(B  World Region Tint)]
        _RegionStrength ("지역색 강도", Range(0, 1)) = 1
        _SaturationCap ("채도 상한", Range(0, 1)) = 0.75

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // ── URP 키워드 (Lit 과 동일 세트) ──
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            // 전역(B축) — 재질이 아니라 JcRegionPaletteController 가 소유. _JcRegionBounds.z <= 0 이면 미결선 → B축 비활성.
            TEXTURE2D(_JcRegionTex);  SAMPLER(sampler_JcRegionTex);
            float4 _JcRegionBounds;   // x,y = 월드 min XZ / z,w = 1/width, 1/height

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _BottomColor;
                half4  _TopColor;
                float  _GradientHeight;
                float  _GradientBias;
                float  _GradientPower;
                half   _BottomShade;
                half   _RegionStrength;
                half   _SaturationCap;
                half   _Smoothness;
                half   _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  heightT    : TEXCOORD3;   // A축 0~1 (로컬 Y 기반)
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(input.normalOS);

                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS   = vn.normalWS;
                o.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor  = ComputeFogFactor(vp.positionCS.z);

                // A축: 메시 로컬 Y → 0~1. 스케일 지터가 들어가도 "그루 안의 비율"은 그대로.
                float h = (input.positionOS.y + _GradientBias) / max(_GradientHeight, 1e-3);
                o.heightT = saturate(h);
                return o;
            }

            // 채도 상한: (max-min)/max 가 cap 을 넘으면 회색(명도 보존) 쪽으로 눌러 준다.
            half3 ClampSaturation(half3 c, half cap)
            {
                half mx = max(c.r, max(c.g, c.b));
                half mn = min(c.r, min(c.g, c.b));
                half sat = (mx - mn) / max(mx, 1e-4h);
                half k = sat > cap ? cap / sat : 1.0h;
                half3 g = Luminance(c).xxx;
                return lerp(g, c, k);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ── A축: 명도(무채색 맵) × 높이 그라데이션 × 하단 음영 ──
                half  t     = pow(input.heightT, _GradientPower);
                half3 grey  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 col   = grey * lerp(_BottomColor.rgb, _TopColor.rgb, t);
                col *= lerp(1.0h - _BottomShade, 1.0h, t);

                // ── B축: 월드 XZ → 지역 텍스처. 색조만 갈아타고 명도는 보존(hue transfer) ──
                if (_JcRegionBounds.z > 0.0)
                {
                    float2 ruv = (input.positionWS.xz - _JcRegionBounds.xy) * _JcRegionBounds.zw;
                    half4 region = SAMPLE_TEXTURE2D(_JcRegionTex, sampler_JcRegionTex, ruv);
                    half lum  = Luminance(col);
                    half lumR = max(Luminance(region.rgb), 1e-3h);
                    half3 swapped = region.rgb * (lum / lumR);
                    col = lerp(col, swapped, region.a * _RegionStrength);
                }

                col = ClampSaturation(col, _SaturationCap);

                // ── URP PBR ──
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS   = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord    = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI     = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask  = half4(1, 1, 1, 1);

                SurfaceData surf = (SurfaceData)0;
                surf.albedo     = col;
                surf.alpha      = 1.0h;
                surf.metallic   = _Metallic;
                surf.specular   = half3(0, 0, 0);
                surf.smoothness = _Smoothness;
                surf.normalTS   = half3(0, 0, 1);
                surf.occlusion  = 1.0h;
                surf.emission   = half3(0, 0, 0);

                half4 color = UniversalFragmentPBR(inputData, surf);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
