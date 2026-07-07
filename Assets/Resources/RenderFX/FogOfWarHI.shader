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
        _FogColor ("Fog Color - Low (volume interior)", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogColorMid ("Fog Color - Mid (volume surface)", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogColorHigh ("Fog Color - High (volume top)", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogDensityLow ("Fog Density - Low", Range(0, 1)) = 1.0
        _FogDensityMid ("Fog Density - Mid", Range(0, 1)) = 0.85
        _FogDensityHigh ("Fog Density - High", Range(0, 1)) = 0.50

        _NoiseScale1 ("Noise Scale (Base)", Float) = 6.0
        _NoiseScale2 ("Noise Scale (Detail)", Float) = 18.0
        _NoiseScale3 ("Noise Scale (Distortion)", Float) = 3.0

        _FlowSpeed1 ("Flow Speed (Base)", Float) = 0.15
        _FlowSpeed2 ("Flow Speed (Detail)", Float) = 0.25
        _FlowSpeed3 ("Flow Speed (Distortion)", Float) = 0.08

        _DistortionStrength ("Distortion Strength", Float) = 0.3
        _NoiseContrast ("Noise Contrast", Range(0.5, 8.0)) = 3.5

        _HeightTransition ("Height Band Softness", Range(0.1, 2.0)) = 0.5
        _FogLowTopY ("Low Top Y (Low->Mid boundary height)", Float) = 2.0
        _FogHighStartY ("High Start Y (Mid->High boundary height)", Float) = 2.0

        _BrightnessLow ("Brightness - Low (volume interior)", Range(0.3, 1.5)) = 0.75
        _BrightnessMid ("Brightness - Mid (volume surface)", Range(0.3, 1.5)) = 1.00
        _BrightnessHigh ("Brightness - High (volume top)", Range(0.3, 1.5)) = 1.25
        _CloudContrast ("Cloud Contrast (color modulation range)", Range(0.0, 0.5)) = 0.15
        _EdgeSoftness ("Edge Softness (cell ratio, non-SDF fallback)", Range(0.01, 0.49)) = 0.18

        _EdgeWidthWorld ("Edge Width - blur (world units)", Range(0.01, 10.0)) = 1.0
        _EdgeNoiseStrength ("Edge Noise Strength - roughness (world units)", Range(0.0, 5.0)) = 0.5
        _EdgeNoiseScale ("Edge Noise Scale", Float) = 0.35
        _EdgeNoiseSpeed ("Edge Noise Speed", Float) = 0.1
        _EdgeFadeWidth ("Edge Density Ramp Width (world units, 0=off)", Range(0.0, 20.0)) = 3.0
        _EdgeLayerSpreadWorld ("Edge Layer Spread - taper (world units, 0=off)", Range(0.0, 10.0)) = 1.0
        _SightBoostWorld ("Sight Boost (world units, visual only)", Float) = 0.0

        _SheetOpacity ("Cloud Sheet Opacity (0=off)", Range(0.0, 1.0)) = 0.0
        _SheetHeightY ("Cloud Sheet Height Y (world)", Float) = 2.0
        _SheetColor ("Cloud Sheet Color", Color) = (0.75, 0.78, 0.85, 1.0)
        _SheetEdgeShiftWorld ("Cloud Sheet Edge Shift (world, C# computed)", Float) = 0.0
        _SheetFadeWidthWorld ("Cloud Sheet Fade Half-Width (world)", Range(0.01, 10.0)) = 1.0
        _SheetGroundAlign ("Cloud Sheet Ground Align (0=physical, 1=aligned)", Range(0.0, 1.0)) = 0.5

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

            // 가시성 경계 SDF (FogDistanceField가 전역 푸시. r=셀 단위 부호 거리, +=안개 쪽)
            TEXTURE2D(_FogDistanceTex);
            SAMPLER(sampler_FogDistanceTex);
            float _FogDistanceTexBound;

            float4 _FogGridMin;
            float4 _FogGridMax;
            float4 _FogGridWorldMin;
            float4 _FogGridWorldSize;
            float _FogCellSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _FogColorMid;
                float4 _FogColorHigh;
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
                float _FogLowTopY;
                float _FogHighStartY;

                float _BrightnessLow;
                float _BrightnessMid;
                float _BrightnessHigh;
                float _CloudContrast;
                float _EdgeSoftness;
                float _EdgeWidthWorld;
                float _EdgeNoiseStrength;
                float _EdgeNoiseScale;
                float _EdgeNoiseSpeed;
                float _EdgeFadeWidth;
                float _EdgeLayerSpreadWorld;
                float _SightBoostWorld;
                float _SheetOpacity;
                float _SheetHeightY;
                float4 _SheetColor;
                float _SheetEdgeShiftWorld;
                float _SheetFadeWidthWorld;
                float _SheetGroundAlign;
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

                float2 gridUV = saturate(float2(
                    worldOffset.x / max(_FogGridWorldSize.x, 0.0001),
                    worldOffset.y / max(_FogGridWorldSize.y, 0.0001)));

                float visLow, visMid, visHigh;
                float distWorld = 0.0;
                bool useSdf = _FogDistanceTexBound > 0.5;

                if (useSdf)
                {
                    // SDF 경로: 셀 격자를 벗어난 영역 기반 등고선.
                    //   경계 폭(_EdgeWidthWorld)·거칠기 노이즈(_EdgeNoise*)·레이어 테이퍼(_EdgeLayerSpreadWorld) 전부 월드 유닛.
                    float dCells = SAMPLE_TEXTURE2D_LOD(_FogDistanceTex, sampler_FogDistanceTex, gridUV, 0).r;
                    distWorld = dCells * max(_FogCellSize, 0.0001);

                    // 시야 배율(시각 전용): 데이터 공개 범위는 그대로, 렌더 경계만 안개 쪽(+)/시야 쪽(-)으로 평행이동
                    distWorld -= _SightBoostWorld;

                    float2 en = worldPos.xz * _EdgeNoiseScale + _Time.y * _EdgeNoiseSpeed;
                    distWorld += (GradientNoise(en) - 0.5) * 2.0 * _EdgeNoiseStrength;

                    float ew = max(_EdgeWidthWorld, 0.001);
                    visLow  = 1.0 - smoothstep(-ew, ew, distWorld);
                    visMid  = 1.0 - smoothstep(-ew, ew, distWorld - _EdgeLayerSpreadWorld);
                    visHigh = 1.0 - smoothstep(-ew, ew, distWorld - 2.0 * _EdgeLayerSpreadWorld);
                }
                else
                {
                    // 폴백: SDF 미바인딩 시 4텍셀 보간 (테이퍼·거칠기·농도 램프 없음)
                    float vis = SampleVisibilitySmooth(worldOffset, texelSize);
                    visLow = vis;
                    visMid = vis;
                    visHigh = vis;
                }

                #if defined(_DEBUGMODE_ON)
                    return half4(visLow.xxx, 1.0);
                #endif

                float cellFogLow = 1.0 - visLow;
                float cellFogMid = 1.0 - visMid;
                float cellFogHigh = 1.0 - visHigh;
                // 시트가 켜져 있으면 가시 픽셀도 통과시킨다 — 사선 카메라에서 시트 교차점은
                // 표면과 다른 XZ라, 표면 기준으로 조기 탈출하면 경계 부근 시트에 이음선이 생김
                if (cellFogLow < 0.001 && _SheetOpacity <= 0.001)
                    return sceneColor;

                // 영역 지정 제어: R=밀도 배율, G/B/A=low/mid/high 활성 (미바인딩 시 전부 1)
                float4 zone = SAMPLE_TEXTURE2D_LOD(_FogZoneTex, sampler_FogZoneTex, gridUV, 0);
                float zoneDensity = lerp(1.0, zone.r, _FogZoneTexBound);
                float3 zoneLayerOn = lerp(float3(1.0, 1.0, 1.0), zone.gba, _FogZoneTexBound);

                // 높이 대역 판정: Low(지면 Y < LowTopY) / Mid(LowTopY~HighStartY) / High(> HighStartY)
                //   두 경계가 같으면(기본 2.0) 구 단일 ceiling 동작과 동치
                float worldY = worldPos.y;
                float s = _HeightTransition;

                float lowFactor = 1.0 - smoothstep(_FogLowTopY - s, _FogLowTopY, worldY);
                float highFactor = smoothstep(_FogHighStartY, _FogHighStartY + s, worldY);
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

                // 경계 농도 램프: 안개 알파가 0에서 출발하는 지점(d=-_EdgeWidthWorld)부터 안쪽으로
                // _EdgeFadeWidth에 걸쳐 농도가 차오름. d=0(등고선) 기준으로 자르면 블러 그라데이션의
                // 바깥 절반이 계단으로 잘려 fadeWidth가 작을 때 경계선이 도드라진다 (260707 수정)
                float edgeFade = 1.0;
                if (useSdf && _EdgeFadeWidth > 0.001)
                    edgeFade = saturate((distWorld + _EdgeWidthWorld) / _EdgeFadeWidth);

                float alpha = saturate((lowFog + midFog + highFog) * zoneDensity * edgeFade);

                // 레이어별 컬러 × 밝기 가중 합성 (세 컬러가 같으면 구 단일 컬러 수식과 동치)
                half3 layerColor = lowFactor  * _FogColor.rgb     * _BrightnessLow
                                 + midFactor  * _FogColorMid.rgb  * _BrightnessMid
                                 + highFactor * _FogColorHigh.rgb * _BrightnessHigh;
                float cloudMod = lerp(1.0 - _CloudContrast, 1.0 + _CloudContrast, cloud);
                half3 fogColorFinal = layerColor * cloudMod;

                half3 result = lerp(sceneColor.rgb, fogColorFinal, alpha);

                // ===== 구름 시트: 고정 높이 평면(Y=_SheetHeightY)과의 레이 교차점 기준 반투명층 =====
                // 표면이 아니라 평면 좌표로 SDF·구름을 샘플 → 벽/틈을 타고 흐르는 표면 밀착 티를 가림.
                // 표면이 평면보다 가까우면(솟은 구조물) 시트를 그리지 않아 구조물이 구름을 뚫고 나온다.
                if (_SheetOpacity > 0.001 && useSdf)
                {
                    float3 camPos = _WorldSpaceCameraPos;
                    float3 rayVec = worldPos - camPos;
                    float tSurf = length(rayVec);
                    float3 rayDir = rayVec / max(tSurf, 0.0001);

                    if (abs(rayDir.y) > 0.001)
                    {
                        float tPlane = (_SheetHeightY - camPos.y) / rayDir.y;
                        if (tPlane > 0.0 && tPlane < tSurf)
                        {
                            float3 planeHit = camPos + rayDir * tPlane;

                            // 마스크 샘플 좌표: 물리적 구름 위치(평면 XZ) ↔ 지면 경계 정렬(표면 XZ) 블렌드.
                            // 사선 카메라에서 시트 구멍이 지면 경계 대비 밀려 보이는 시차의 보정 노브.
                            float2 maskXZ = lerp(planeHit.xz, worldPos.xz, _SheetGroundAlign);
                            float2 maskOffset = maskXZ - _FogGridWorldMin.xy;
                            float2 maskUV = saturate(float2(
                                maskOffset.x / max(_FogGridWorldSize.x, 0.0001),
                                maskOffset.y / max(_FogGridWorldSize.y, 0.0001)));

                            // 시트 가장자리 = base 경계 파라미터에서 독립: 확대축소(_SheetEdgeShiftWorld,
                            // C#에서 (계수-1)×시야반경 환산)와 전용 페이드(_SheetFadeWidthWorld)만 사용.
                            // 거칠기 노이즈는 base와 공유해 두 층의 굴곡 질감을 일치시킨다.
                            float dSheet = SAMPLE_TEXTURE2D_LOD(_FogDistanceTex, sampler_FogDistanceTex, maskUV, 0).r
                                           * max(_FogCellSize, 0.0001);
                            dSheet -= _SightBoostWorld + _SheetEdgeShiftWorld;
                            float2 enSheet = maskXZ * _EdgeNoiseScale + _Time.y * _EdgeNoiseSpeed;
                            dSheet += (GradientNoise(enSheet) - 0.5) * 2.0 * _EdgeNoiseStrength;

                            float fwSheet = max(_SheetFadeWidthWorld, 0.001);
                            float sheetMask = smoothstep(-fwSheet, fwSheet, dSheet);

                            float4 zoneSheet = SAMPLE_TEXTURE2D_LOD(_FogZoneTex, sampler_FogZoneTex, maskUV, 0);
                            sheetMask *= lerp(1.0, zoneSheet.r, _FogZoneTexBound);

                            // 구름 질감 — 노이즈 파라미터는 base와 공유, 샘플 좌표만 평면 기준
                            float2 nUVSheet = planeHit.xz;
                            float2 dInSheet = nUVSheet * _NoiseScale3 * gridInvX + float2(t * _FlowSpeed3, t * _FlowSpeed3 * 0.7);
                            float2 distSheet = float2(
                                GradientNoise(dInSheet) - 0.5,
                                GradientNoise(dInSheet + float2(43, 17)) - 0.5
                            ) * _DistortionStrength;

                            float2 bInSheet = nUVSheet * _NoiseScale1 * gridInvX + distSheet + float2(t * _FlowSpeed1, t * _FlowSpeed1 * 0.3);
                            float nBaseSheet = FBM(bInSheet, 4);
                            float2 dIn2Sheet = nUVSheet * _NoiseScale2 * gridInvX + distSheet * 0.5 + float2(-t * _FlowSpeed2 * 0.5, t * _FlowSpeed2 * 0.8);
                            float nDetailSheet = FBM(dIn2Sheet, 3);
                            float noiseSheet = saturate(((nBaseSheet * 0.55 + nDetailSheet * 0.45) - 0.5) * _NoiseContrast + 0.5);
                            float cloudSheet = lerp(0.2, 1.0, noiseSheet);

                            float cloudModSheet = lerp(1.0 - _CloudContrast, 1.0 + _CloudContrast, cloudSheet);
                            float sheetAlpha = saturate(_SheetOpacity * sheetMask * cloudSheet);
                            result = lerp(result, _SheetColor.rgb * cloudModSheet, sheetAlpha);
                        }
                    }
                }

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
