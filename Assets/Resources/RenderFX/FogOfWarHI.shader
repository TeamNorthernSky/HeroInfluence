// ============================================================================
// FogOfWarHI  [JC 신설 260706 — RenderFX 라이브러리 소유]
// DH FogOfWar_PointSoftEdge 포크. 노이즈/높이볼륨/컬러부는 동일, 가시성 샘플링부 교체:
//   - 이진 임계값(>=0.75)·이웃 trim 제거 → 4텍셀 수동 보간 + smoothstep 성형(_EdgeSoftness)
//     : depth 정밀도 노이즈에 의한 셀 경계 플리커(z-fight성 지글거림) 구조적 제거 (JC FogOfWarJC 철학)
//   - _EdgeLayerSpread: 가시 경계에서 high→mid→low 순으로 먼저 걷히는 레이어 테이퍼
//   - _FogZoneTex (전역, RenderFXManager 푸시): R=밀도 배율, G/B/A=low/mid/high 레이어 활성
//     _FogZoneTexBound=0이면 무시(기본 전부 활성/배율 1)
// 데이터 소스는 DH FogRenderManager의 _FogVisibilityTex/_FogGridWorld* 전역 그대로 소비.
// ============================================================================
Shader "Custom/HI/FogOfWar"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogDensityLow ("Fog Density - Low (Y<1)", Range(0, 1)) = 1.0
        _FogDensityMid ("Fog Density - Mid (Y 1~2)", Range(0, 1)) = 0.85
        _FogDensityHigh ("Fog Density - High (Y>2)", Range(0, 1)) = 0.50

        _NoiseScale1 ("Noise Scale (Base)", Float) = 6.0
        _NoiseScale2 ("Noise Scale (Detail)", Float) = 18.0
        _NoiseScale3 ("Noise Scale (Distortion)", Float) = 3.0

        _FlowSpeed1 ("Flow Speed (Base)", Float) = 0.15
        _FlowSpeed2 ("Flow Speed (Detail)", Float) = 0.25
        _FlowSpeed3 ("Flow Speed (Distortion)", Float) = 0.08

        _DistortionStrength ("Distortion Strength", Float) = 0.3
        _NoiseContrast ("Noise Contrast", Range(0.5, 8.0)) = 3.5

        _HeightTransition ("Volume Ceiling Softness", Range(0.1, 2.0)) = 0.5
        _FogCeilingY ("Fog Ceiling Y (volume top height)", Float) = 2.0

        _BrightnessLow ("Brightness - Low (volume interior)", Range(0.3, 1.5)) = 0.75
        _BrightnessMid ("Brightness - Mid (volume surface)", Range(0.3, 1.5)) = 1.00
        _BrightnessHigh ("Brightness - High (volume top)", Range(0.3, 1.5)) = 1.25
        _CloudContrast ("Cloud Contrast (color modulation range)", Range(0.0, 0.5)) = 0.15
        _EdgeSoftness ("Edge Softness (cell ratio)", Range(0.01, 0.49)) = 0.18
        _EdgeLayerSpread ("Edge Layer Spread (taper, 0=off)", Range(0.0, 0.45)) = 0.15

        [Toggle] _DebugMode ("Debug Mode (show visibility)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "FogOfWarHIPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _DEBUGMODE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

            TEXTURE2D(_FogVisibilityTex);
            SAMPLER(sampler_FogVisibilityTex);

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 영역 지정 안개 제어 (RenderFXManager가 전역 푸시. 미바인딩 시 _FogZoneTexBound=0)
            TEXTURE2D(_FogZoneTex);
            SAMPLER(sampler_FogZoneTex);
            float _FogZoneTexBound;

            float4 _FogGridMin;
            float4 _FogGridMax;
            float4 _FogGridWorldMin;
            float4 _FogGridWorldSize;
            float _FogCellSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogDensityLow;
                float _FogDensityMid;
                float _FogDensityHigh;

                float _NoiseScale1;
                float _NoiseScale2;
                float _NoiseScale3;

                float _FlowSpeed1;
                float _FlowSpeed2;
                float _FlowSpeed3;

                float _DistortionStrength;
                float _NoiseContrast;

                float _HeightTransition;
                float _FogCeilingY;

                float _BrightnessLow;
                float _BrightnessMid;
                float _BrightnessHigh;
                float _CloudContrast;
                float _EdgeSoftness;
                float _EdgeLayerSpread;
            CBUFFER_END

            float2 HashGradient(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float GradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float n00 = dot(HashGradient(i), f);
                float n10 = dot(HashGradient(i + float2(1, 0)), f - float2(1, 0));
                float n01 = dot(HashGradient(i + float2(0, 1)), f - float2(0, 1));
                float n11 = dot(HashGradient(i + float2(1, 1)), f - float2(1, 1));

                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y) * 0.5 + 0.5;
            }

            float FBM(float2 p, int octaves)
            {
                float val = 0.0;
                float amp = 0.5;
                float freq = 1.0;
                for (int i = 0; i < octaves; i++)
                {
                    val += amp * GradientNoise(p * freq);
                    freq *= 2.0;
                    amp *= 0.5;
                }
                return val;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // 셀 인덱스(정수) 중심에서 point 샘플. 텍스처 필터 모드와 무관하게 결정적.
            float SampleCellVisibility(float2 cell, float2 texelSize)
            {
                float2 uv = saturate((cell + 0.5) * texelSize);
                return SAMPLE_TEXTURE2D_LOD(_FogVisibilityTex, sampler_FogVisibilityTex, uv, 0).r;
            }

            // 4텍셀 수동 보간 + smoothstep 성형. 임계값 없는 연속값 → 시간적 안정.
            float SampleVisibilitySmooth(float2 worldOffset, float2 texelSize)
            {
                float2 gridCoord = worldOffset / max(_FogCellSize, 0.0001);
                float2 p = gridCoord - 0.5;
                float2 baseCell = floor(p);
                float2 f = frac(p);

                float es = clamp(_EdgeSoftness, 0.01, 0.49);
                float2 w = float2(
                    smoothstep(0.5 - es, 0.5 + es, f.x),
                    smoothstep(0.5 - es, 0.5 + es, f.y));

                float v00 = SampleCellVisibility(baseCell, texelSize);
                float v10 = SampleCellVisibility(baseCell + float2(1, 0), texelSize);
                float v01 = SampleCellVisibility(baseCell + float2(0, 1), texelSize);
                float v11 = SampleCellVisibility(baseCell + float2(1, 1), texelSize);

                return lerp(lerp(v00, v10, w.x), lerp(v01, v11, w.x), w.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sceneColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                float depth = SampleSceneDepth(input.uv);

                #if UNITY_REVERSED_Z
                    bool isSky = depth < 0.0001;
                #else
                    bool isSky = depth > 0.9999;
                #endif
                if (isSky)
                    return sceneColor;

                float2 posNDC = input.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    posNDC.y = -posNDC.y;
                #endif

                float4 worldPos4 = mul(UNITY_MATRIX_I_VP, float4(posNDC, depth, 1.0));
                float3 worldPos = worldPos4.xyz / worldPos4.w;

                float2 worldOffset = worldPos.xz - _FogGridWorldMin.xy;
                float2 texelSize = float2(
                    _FogCellSize / max(_FogGridWorldSize.x, 0.0001),
                    _FogCellSize / max(_FogGridWorldSize.y, 0.0001));

                float vis = SampleVisibilitySmooth(worldOffset, texelSize);

                // 레이어 테이퍼: 경계 그라디언트에서 high→mid→low 순으로 먼저 걷힘 (0=동시)
                float spread = _EdgeLayerSpread;
                float visLow = vis;
                float visMid = saturate(vis / max(1.0 - spread, 0.0001));
                float visHigh = saturate(vis / max(1.0 - 2.0 * spread, 0.0001));

                #if defined(_DEBUGMODE_ON)
                    return half4(vis.xxx, 1.0);
                #endif

                float cellFogLow = 1.0 - visLow;
                float cellFogMid = 1.0 - visMid;
                float cellFogHigh = 1.0 - visHigh;
                if (cellFogLow < 0.001)
                    return sceneColor;

                // 영역 지정 제어: R=밀도 배율, G/B/A=low/mid/high 활성 (미바인딩 시 전부 1)
                float2 gridUV = saturate(float2(
                    worldOffset.x / max(_FogGridWorldSize.x, 0.0001),
                    worldOffset.y / max(_FogGridWorldSize.y, 0.0001)));
                float4 zone = SAMPLE_TEXTURE2D_LOD(_FogZoneTex, sampler_FogZoneTex, gridUV, 0);
                float zoneDensity = lerp(1.0, zone.r, _FogZoneTexBound);
                float3 zoneLayerOn = lerp(float3(1.0, 1.0, 1.0), zone.gba, _FogZoneTexBound);

                float worldY = worldPos.y;
                float s = _HeightTransition;
                float ceiling = _FogCeilingY;

                float lowFactor = 1.0 - smoothstep(ceiling - s, ceiling, worldY);
                float highFactor = smoothstep(ceiling, ceiling + s, worldY);
                float midFactor = max(0.0, 1.0 - lowFactor - highFactor);

                float t = _Time.y;
                float2 nUV = worldPos.xz;
                float gridInvX = 1.0 / max(_FogGridWorldSize.x, 0.0001);

                float2 dIn = nUV * _NoiseScale3 * gridInvX + float2(t * _FlowSpeed3, t * _FlowSpeed3 * 0.7);
                float2 dist = float2(
                    GradientNoise(dIn) - 0.5,
                    GradientNoise(dIn + float2(43, 17)) - 0.5
                ) * _DistortionStrength;

                float2 bIn = nUV * _NoiseScale1 * gridInvX + dist + float2(t * _FlowSpeed1, t * _FlowSpeed1 * 0.3);
                float nBase = FBM(bIn, 4);

                float2 dIn2 = nUV * _NoiseScale2 * gridInvX + dist * 0.5 + float2(-t * _FlowSpeed2 * 0.5, t * _FlowSpeed2 * 0.8);
                float nDetail = FBM(dIn2, 3);

                float noise = nBase * 0.55 + nDetail * 0.45;
                noise = saturate((noise - 0.5) * _NoiseContrast + 0.5);

                float cloud = lerp(0.2, 1.0, noise);

                float lowFog = lowFactor * _FogDensityLow * cellFogLow * zoneLayerOn.x;
                float midFog = midFactor * _FogDensityMid * cellFogMid * zoneLayerOn.y;
                float highFog = highFactor * _FogDensityHigh * cellFogHigh * zoneLayerOn.z;
                float alpha = saturate((lowFog + midFog + highFog) * zoneDensity);

                float layerBrightness = lowFactor * _BrightnessLow
                                      + midFactor * _BrightnessMid
                                      + highFactor * _BrightnessHigh;
                float cloudMod = lerp(1.0 - _CloudContrast, 1.0 + _CloudContrast, cloud);
                half3 fogColorFinal = _FogColor.rgb * layerBrightness * cloudMod;

                half3 result = lerp(sceneColor.rgb, fogColorFinal, alpha);
                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
