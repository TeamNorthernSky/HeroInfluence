// ============================================================================
// FogOfWarHI  [JC 신설 260706 — RenderFX 라이브러리 소유 / 260714 상태별 2스택 개편]
// DH FogOfWar_PointSoftEdge 포크. 가시성 샘플링을 SDF 등고선으로 교체한 풀스크린 합성.
//   - _FogDistanceTex(RG): R=IsExplored 경계 / G=IsVisible 경계 (FogDistanceField가 푸시)
//   - Unexplored 스택(무접두 프로퍼티)과 Fogged 스택(_Fg* 프로퍼티)이 같은 수식을
//     각자의 파라미터로 평가하고, R 등고선 마스크로 두 "완성된 룩"을 크로스페이드
//     → 상태 경계에서 가산 합성 밝은 띠가 구조적으로 생기지 않는다.
//   - 경계 파라미터(_EdgeWidthWorld/_EdgeNoise*/_EdgeFadeWidth/_EdgeLayerSpreadWorld)와
//     시야 배율(_SightBoostWorld)은 두 스택 공유 — 상태 간 이음새는 한 벌이어야 한다.
//   - _FogZoneTex (전역, RenderFXManager 푸시): R=밀도 배율, G/B/A=low/mid/high 레이어 활성
// 데이터 소스는 DH FogRenderManager의 _FogVisibilityTex/_FogGridWorld* 전역 그대로 소비.
// ============================================================================
Shader "Custom/HI/FogOfWar"
{
    Properties
    {
        // ===== Unexplored 스택 =====
        _FogColor ("UE Fog Color - Low", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogColorMid ("UE Fog Color - Mid", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogColorHigh ("UE Fog Color - High", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogDensityLow ("UE Fog Density - Low", Range(0, 1)) = 1.0
        _FogDensityMid ("UE Fog Density - Mid", Range(0, 1)) = 0.85
        _FogDensityHigh ("UE Fog Density - High", Range(0, 1)) = 0.50
        _NoiseScale1 ("UE Noise Scale (Base)", Float) = 6.0
        _NoiseScale2 ("UE Noise Scale (Detail)", Float) = 18.0
        _NoiseScale3 ("UE Noise Scale (Distortion)", Float) = 3.0
        _FlowSpeed1 ("UE Flow Speed (Base)", Float) = 0.15
        _FlowSpeed2 ("UE Flow Speed (Detail)", Float) = 0.25
        _FlowSpeed3 ("UE Flow Speed (Distortion)", Float) = 0.08
        _DistortionStrength ("UE Distortion Strength", Float) = 0.3
        _NoiseContrast ("UE Noise Contrast", Range(0.5, 8.0)) = 3.5
        _HeightTransition ("UE Height Band Softness", Range(0.1, 2.0)) = 0.5
        _FogLowTopY ("UE Low Top Y", Float) = 2.0
        _FogHighStartY ("UE High Start Y", Float) = 2.0
        _BrightnessLow ("UE Brightness - Low", Range(0.3, 1.5)) = 0.75
        _BrightnessMid ("UE Brightness - Mid", Range(0.3, 1.5)) = 1.00
        _BrightnessHigh ("UE Brightness - High", Range(0.3, 1.5)) = 1.25
        _CloudContrast ("UE Cloud Contrast", Range(0.0, 0.5)) = 0.15
        _CloudCoverage ("UE Cloud Coverage", Range(0.0, 1.0)) = 1.0
        _CloudDensityEffect ("UE Cloud Density Effect", Range(0.0, 1.0)) = 0.0
        _SheetOpacity ("UE Cloud Sheet Opacity (0=off)", Range(0.0, 1.0)) = 0.0
        _SheetHeightY ("UE Cloud Sheet Height Y (world)", Float) = 2.0
        _SheetColor ("UE Cloud Sheet Color", Color) = (0.75, 0.78, 0.85, 1.0)
        _SheetEdgeShiftWorld ("UE Cloud Sheet Edge Shift (world, C# computed)", Float) = 0.0
        _SheetFadeWidthWorld ("UE Cloud Sheet Fade Half-Width (world)", Range(0.01, 10.0)) = 1.0
        _SheetGroundAlign ("UE Cloud Sheet Ground Align", Range(0.0, 1.0)) = 0.5

        // ===== Fogged 스택 (탐사됨·비가시 지역) =====
        _FgFogColor ("FG Fog Color - Low", Color) = (0.75, 0.78, 0.85, 1.0)
        _FgFogColorMid ("FG Fog Color - Mid", Color) = (0.75, 0.78, 0.85, 1.0)
        _FgFogColorHigh ("FG Fog Color - High", Color) = (0.75, 0.78, 0.85, 1.0)
        _FgFogDensityLow ("FG Fog Density - Low", Range(0, 1)) = 0.0
        _FgFogDensityMid ("FG Fog Density - Mid", Range(0, 1)) = 0.85
        _FgFogDensityHigh ("FG Fog Density - High", Range(0, 1)) = 0.50
        _FgNoiseScale1 ("FG Noise Scale (Base)", Float) = 6.0
        _FgNoiseScale2 ("FG Noise Scale (Detail)", Float) = 18.0
        _FgNoiseScale3 ("FG Noise Scale (Distortion)", Float) = 3.0
        _FgFlowSpeed1 ("FG Flow Speed (Base)", Float) = 0.15
        _FgFlowSpeed2 ("FG Flow Speed (Detail)", Float) = 0.25
        _FgFlowSpeed3 ("FG Flow Speed (Distortion)", Float) = 0.08
        _FgDistortionStrength ("FG Distortion Strength", Float) = 0.3
        _FgNoiseContrast ("FG Noise Contrast", Range(0.5, 8.0)) = 3.5
        _FgHeightTransition ("FG Height Band Softness", Range(0.1, 2.0)) = 0.5
        _FgFogLowTopY ("FG Low Top Y", Float) = 2.0
        _FgFogHighStartY ("FG High Start Y", Float) = 2.0
        _FgBrightnessLow ("FG Brightness - Low", Range(0.3, 1.5)) = 0.75
        _FgBrightnessMid ("FG Brightness - Mid", Range(0.3, 1.5)) = 1.00
        _FgBrightnessHigh ("FG Brightness - High", Range(0.3, 1.5)) = 1.25
        _FgCloudContrast ("FG Cloud Contrast", Range(0.0, 0.5)) = 0.15
        _FgCloudCoverage ("FG Cloud Coverage", Range(0.0, 1.0)) = 1.0
        _FgCloudDensityEffect ("FG Cloud Density Effect", Range(0.0, 1.0)) = 0.0
        _FgSheetOpacity ("FG Cloud Sheet Opacity (0=off)", Range(0.0, 1.0)) = 0.0
        _FgSheetHeightY ("FG Cloud Sheet Height Y (world)", Float) = 2.0
        _FgSheetColor ("FG Cloud Sheet Color", Color) = (0.75, 0.78, 0.85, 1.0)
        _FgSheetEdgeShiftWorld ("FG Cloud Sheet Edge Shift (world, C# computed)", Float) = 0.0
        _FgSheetFadeWidthWorld ("FG Cloud Sheet Fade Half-Width (world)", Range(0.01, 10.0)) = 1.0
        _FgSheetGroundAlign ("FG Cloud Sheet Ground Align", Range(0.0, 1.0)) = 0.5

        // ===== 공유 (상태 간 이음새) =====
        _EdgeSoftness ("Edge Softness (cell ratio, non-SDF fallback)", Range(0.01, 0.49)) = 0.18
        _EdgeWidthWorld ("Edge Width - blur (world units)", Range(0.01, 10.0)) = 1.0
        _EdgeNoiseStrength ("Edge Noise Strength - roughness (world units)", Range(0.0, 5.0)) = 0.5
        _EdgeNoiseScale ("Edge Noise Scale", Float) = 0.35
        _EdgeNoiseSpeed ("Edge Noise Speed", Float) = 0.1
        _EdgeFadeWidth ("Edge Density Ramp Width (world units, 0=off)", Range(0.0, 20.0)) = 3.0
        _EdgeLayerSpreadWorld ("Edge Layer Spread - taper (world units, 0=off)", Range(0.0, 10.0)) = 1.0
        _SightBoostWorld ("Sight Boost (world units, visual only)", Float) = 0.0
        _StateBlendWidthWorld ("State Blend Half-Width (world units)", Range(0.01, 20.0)) = 1.0

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

            // 가시성 경계 SDF (FogDistanceField가 전역 푸시. R=IsExplored, G=IsVisible, 셀 단위 부호 거리, +=안개 쪽)
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
                float _CloudCoverage;
                float _CloudDensityEffect;
                float _SheetOpacity;
                float _SheetHeightY;
                float4 _SheetColor;
                float _SheetEdgeShiftWorld;
                float _SheetFadeWidthWorld;
                float _SheetGroundAlign;

                float4 _FgFogColor;
                float4 _FgFogColorMid;
                float4 _FgFogColorHigh;
                float _FgFogDensityLow;
                float _FgFogDensityMid;
                float _FgFogDensityHigh;
                float _FgNoiseScale1;
                float _FgNoiseScale2;
                float _FgNoiseScale3;
                float _FgFlowSpeed1;
                float _FgFlowSpeed2;
                float _FgFlowSpeed3;
                float _FgDistortionStrength;
                float _FgNoiseContrast;
                float _FgHeightTransition;
                float _FgFogLowTopY;
                float _FgFogHighStartY;
                float _FgBrightnessLow;
                float _FgBrightnessMid;
                float _FgBrightnessHigh;
                float _FgCloudContrast;
                float _FgCloudCoverage;
                float _FgCloudDensityEffect;
                float _FgSheetOpacity;
                float _FgSheetHeightY;
                float4 _FgSheetColor;
                float _FgSheetEdgeShiftWorld;
                float _FgSheetFadeWidthWorld;
                float _FgSheetGroundAlign;

                float _EdgeSoftness;
                float _EdgeWidthWorld;
                float _EdgeNoiseStrength;
                float _EdgeNoiseScale;
                float _EdgeNoiseSpeed;
                float _EdgeFadeWidth;
                float _EdgeLayerSpreadWorld;
                float _SightBoostWorld;
                float _StateBlendWidthWorld;
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

            // ===== 안개 스택 파라미터 묶음 (Unexplored/Fogged 각 1벌) =====
            struct FogStack
            {
                half3 colLow; half3 colMid; half3 colHigh;
                float densLow; float densMid; float densHigh;
                float ns1; float ns2; float ns3;
                float fs1; float fs2; float fs3;
                float distortion; float contrast;
                float heightTransition; float lowTopY; float highStartY;
                float brightLow; float brightMid; float brightHigh;
                float cloudContrast;
                float coverage; float densityEffect;
            };

            FogStack MakeUnexploredStack()
            {
                FogStack s;
                s.colLow = _FogColor.rgb; s.colMid = _FogColorMid.rgb; s.colHigh = _FogColorHigh.rgb;
                s.densLow = _FogDensityLow; s.densMid = _FogDensityMid; s.densHigh = _FogDensityHigh;
                s.ns1 = _NoiseScale1; s.ns2 = _NoiseScale2; s.ns3 = _NoiseScale3;
                s.fs1 = _FlowSpeed1; s.fs2 = _FlowSpeed2; s.fs3 = _FlowSpeed3;
                s.distortion = _DistortionStrength; s.contrast = _NoiseContrast;
                s.heightTransition = _HeightTransition; s.lowTopY = _FogLowTopY; s.highStartY = _FogHighStartY;
                s.brightLow = _BrightnessLow; s.brightMid = _BrightnessMid; s.brightHigh = _BrightnessHigh;
                s.cloudContrast = _CloudContrast;
                s.coverage = _CloudCoverage; s.densityEffect = _CloudDensityEffect;
                return s;
            }

            FogStack MakeFoggedStack()
            {
                FogStack s;
                s.colLow = _FgFogColor.rgb; s.colMid = _FgFogColorMid.rgb; s.colHigh = _FgFogColorHigh.rgb;
                s.densLow = _FgFogDensityLow; s.densMid = _FgFogDensityMid; s.densHigh = _FgFogDensityHigh;
                s.ns1 = _FgNoiseScale1; s.ns2 = _FgNoiseScale2; s.ns3 = _FgNoiseScale3;
                s.fs1 = _FgFlowSpeed1; s.fs2 = _FgFlowSpeed2; s.fs3 = _FgFlowSpeed3;
                s.distortion = _FgDistortionStrength; s.contrast = _FgNoiseContrast;
                s.heightTransition = _FgHeightTransition; s.lowTopY = _FgFogLowTopY; s.highStartY = _FgFogHighStartY;
                s.brightLow = _FgBrightnessLow; s.brightMid = _FgBrightnessMid; s.brightHigh = _FgBrightnessHigh;
                s.cloudContrast = _FgCloudContrast;
                s.coverage = _FgCloudCoverage; s.densityEffect = _FgCloudDensityEffect;
                return s;
            }

            // 노이즈 구름. x = 구름값(0.2~1, 색 명암용) / y = 커버리지 마스크(0~1, 농도·시트용)
            // coverage 1이면 y는 전 영역 1(현행 동치), 낮출수록 노이즈 낮은 곳부터 뚫린다
            float2 CloudValue(float2 xz, float gridInvX, float t, FogStack s)
            {
                float2 dIn = xz * s.ns3 * gridInvX + float2(t * s.fs3, t * s.fs3 * 0.7);
                float2 dist = float2(
                    GradientNoise(dIn) - 0.5,
                    GradientNoise(dIn + float2(43, 17)) - 0.5
                ) * s.distortion;

                float2 bIn = xz * s.ns1 * gridInvX + dist + float2(t * s.fs1, t * s.fs1 * 0.3);
                float nBase = FBM(bIn, 4);
                float2 dIn2 = xz * s.ns2 * gridInvX + dist * 0.5 + float2(-t * s.fs2 * 0.5, t * s.fs2 * 0.8);
                float nDetail = FBM(dIn2, 3);

                float noise = saturate(((nBase * 0.55 + nDetail * 0.45) - 0.5) * s.contrast + 0.5);

                float cutLow = 1.0 - s.coverage * 1.3;   // coverage 1 → cutLow -0.3 → 마스크 상시 1
                float mask = smoothstep(cutLow, cutLow + 0.3, noise);
                return float2(lerp(0.2, 1.0, noise), mask);
            }

            // 스택 1벌 평가: 등고선 3대역 마스크 + 높이 대역 + 구름 → 안개 색/알파
            void EvaluateStack(FogStack s, float3 worldPos, float distW, float ew,
                               float zoneDensity, float3 zoneLayerOn, float gridInvX, float t,
                               out half3 fogColor, out float fogAlpha)
            {
                float visL = 1.0 - smoothstep(-ew, ew, distW);
                float visM = 1.0 - smoothstep(-ew, ew, distW - _EdgeLayerSpreadWorld);
                float visH = 1.0 - smoothstep(-ew, ew, distW - 2.0 * _EdgeLayerSpreadWorld);

                float lowFactor = 1.0 - smoothstep(s.lowTopY - s.heightTransition, s.lowTopY, worldPos.y);
                float highFactor = smoothstep(s.highStartY, s.highStartY + s.heightTransition, worldPos.y);
                float midFactor = max(0.0, 1.0 - lowFactor - highFactor);

                float2 cv = CloudValue(worldPos.xz, gridInvX, t, s);

                float lowFog = lowFactor * s.densLow * (1.0 - visL) * zoneLayerOn.x;
                float midFog = midFactor * s.densMid * (1.0 - visM) * zoneLayerOn.y;
                float highFog = highFactor * s.densHigh * (1.0 - visH) * zoneLayerOn.z;

                // 경계 농도 램프: 안개 알파가 0에서 출발하는 지점(d=-ew)부터 안쪽으로 차오름 (공유 파라미터)
                float edgeFade = 1.0;
                if (_EdgeFadeWidth > 0.001)
                    edgeFade = saturate((distW + ew) / _EdgeFadeWidth);

                // 구름의 농도 관여: densityEffect 0=색 명암만(현행), 1=커버리지 틈은 투명
                float cloudDensity = lerp(1.0, cv.y, s.densityEffect);

                fogAlpha = saturate((lowFog + midFog + highFog) * zoneDensity * edgeFade * cloudDensity);

                half3 layerColor = lowFactor * s.colLow * s.brightLow
                                 + midFactor * s.colMid * s.brightMid
                                 + highFactor * s.colHigh * s.brightHigh;
                float cloudMod = lerp(1.0 - s.cloudContrast, 1.0 + s.cloudContrast, cv.x);
                fogColor = layerColor * cloudMod;
            }

            // 구름 시트: 고정 높이 평면과의 레이 교차점 기준 반투명층. channelSel로 SDF 채널 선택 (R=UE, G=FG)
            half3 ApplyCloudSheet(half3 result, FogStack s,
                                  float opacity, float heightY, half3 sheetColor,
                                  float edgeShift, float fadeW, float groundAlign,
                                  float2 channelSel, float3 worldPos, float gridInvX, float t)
            {
                if (opacity <= 0.001)
                    return result;

                float3 camPos = _WorldSpaceCameraPos;
                float3 rayVec = worldPos - camPos;
                float tSurf = length(rayVec);
                float3 rayDir = rayVec / max(tSurf, 0.0001);

                if (abs(rayDir.y) <= 0.001)
                    return result;

                float tPlane = (heightY - camPos.y) / rayDir.y;
                if (tPlane <= 0.0 || tPlane >= tSurf)
                    return result;

                float3 planeHit = camPos + rayDir * tPlane;

                // 마스크 샘플 좌표: 물리적 구름 위치(평면 XZ) ↔ 지면 경계 정렬(표면 XZ) 블렌드
                float2 maskXZ = lerp(planeHit.xz, worldPos.xz, groundAlign);
                float2 maskOffset = maskXZ - _FogGridWorldMin.xy;
                float2 maskUV = saturate(float2(
                    maskOffset.x / max(_FogGridWorldSize.x, 0.0001),
                    maskOffset.y / max(_FogGridWorldSize.y, 0.0001)));

                float dSheet = dot(SAMPLE_TEXTURE2D_LOD(_FogDistanceTex, sampler_FogDistanceTex, maskUV, 0).rg, channelSel)
                               * max(_FogCellSize, 0.0001);
                dSheet -= _SightBoostWorld + edgeShift;
                float2 enSheet = maskXZ * _EdgeNoiseScale + _Time.y * _EdgeNoiseSpeed;
                dSheet += (GradientNoise(enSheet) - 0.5) * 2.0 * _EdgeNoiseStrength;

                float fw = max(fadeW, 0.001);
                float sheetMask = smoothstep(-fw, fw, dSheet);

                float4 zoneSheet = SAMPLE_TEXTURE2D_LOD(_FogZoneTex, sampler_FogZoneTex, maskUV, 0);
                sheetMask *= lerp(1.0, zoneSheet.r, _FogZoneTexBound);

                float2 cv = CloudValue(planeHit.xz, gridInvX, t, s);
                float cloudMod = lerp(1.0 - s.cloudContrast, 1.0 + s.cloudContrast, cv.x);
                // 커버리지 마스크로 시트에 구멍 — coverage 1이면 현행 동치
                float sheetAlpha = saturate(opacity * sheetMask * cv.x * cv.y);
                return lerp(result, sheetColor * cloudMod, sheetAlpha);
            }

            // 셀 인덱스(정수) 중심에서 point 샘플. 텍스처 필터 모드와 무관하게 결정적.
            float SampleCellVisibility(float2 cell, float2 texelSize)
            {
                float2 uv = saturate((cell + 0.5) * texelSize);
                return SAMPLE_TEXTURE2D_LOD(_FogVisibilityTex, sampler_FogVisibilityTex, uv, 0).r;
            }

            // 폴백: 4텍셀 수동 보간 + smoothstep 성형 (SDF 미바인딩 시)
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
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

                float distW = 0.0;      // R: IsExplored 경계 (Unexplored 스택)
                float distVisW = 0.0;   // G: IsVisible 경계 (Fogged 스택)
                float ueCellFogLow = 0.0;
                float fgCellFogLow = 0.0;
                float stateBlend = 0.0; // Fogged↔Unexplored 룩 크로스페이드 가중 (전용 폭 _StateBlendWidthWorld)
                float ew = max(_EdgeWidthWorld, 0.001);
                bool useSdf = _FogDistanceTexBound > 0.5;

                if (useSdf)
                {
                    float2 dCellsRG = SAMPLE_TEXTURE2D_LOD(_FogDistanceTex, sampler_FogDistanceTex, gridUV, 0).rg;
                    distW = dCellsRG.r * max(_FogCellSize, 0.0001);
                    distVisW = dCellsRG.g * max(_FogCellSize, 0.0001);

                    // 시야 배율(시각 전용) + 경계 거칠기 노이즈 — 두 등고선에 동일 적용
                    float2 en = worldPos.xz * _EdgeNoiseScale + _Time.y * _EdgeNoiseSpeed;
                    float edgeNoise = (GradientNoise(en) - 0.5) * 2.0 * _EdgeNoiseStrength;
                    distW += edgeNoise - _SightBoostWorld;
                    distVisW += edgeNoise - _SightBoostWorld;

                    ueCellFogLow = smoothstep(-ew, ew, distW);
                    fgCellFogLow = smoothstep(-ew, ew, distVisW);

                    float sbw = max(_StateBlendWidthWorld, 0.001);
                    stateBlend = smoothstep(-sbw, sbw, distW);
                }
                else
                {
                    // 폴백: 4텍셀 보간 — Unexplored 스택만, Fogged 스택 없음 (foggedValue 텍스처가 유일 표현)
                    float vis = SampleVisibilitySmooth(worldOffset, texelSize);
                    ueCellFogLow = 1.0 - vis;
                    stateBlend = ueCellFogLow;
                    distW = (ueCellFogLow - 0.5) * 2.0 * ew;   // 근사 거리 (edgeFade용)
                }

                #if defined(_DEBUGMODE_ON)
                    return half4((1.0 - ueCellFogLow).xxx, 1.0);
                #endif

                bool ueSheetOn = useSdf && _SheetOpacity > 0.001;
                bool fgSheetOn = useSdf && _FgSheetOpacity > 0.001;
                if (stateBlend < 0.001 && fgCellFogLow < 0.001 && !ueSheetOn && !fgSheetOn)
                    return sceneColor;

                // 영역 지정 제어: R=밀도 배율, G/B/A=low/mid/high 활성 (미바인딩 시 전부 1)
                float4 zone = SAMPLE_TEXTURE2D_LOD(_FogZoneTex, sampler_FogZoneTex, gridUV, 0);
                float zoneDensity = lerp(1.0, zone.r, _FogZoneTexBound);
                float3 zoneLayerOn = lerp(float3(1.0, 1.0, 1.0), zone.gba, _FogZoneTexBound);

                float t = _Time.y;
                float gridInvX = 1.0 / max(_FogGridWorldSize.x, 0.0001);

                // ===== Unexplored 스택 (짙은 안개 룩) =====
                half3 ueResult = sceneColor.rgb;
                if (stateBlend > 0.001 || ueSheetOn)
                {
                    FogStack ue = MakeUnexploredStack();
                    half3 fogColor; float fogAlpha;
                    EvaluateStack(ue, worldPos, distW, ew, zoneDensity, zoneLayerOn, gridInvX, t, fogColor, fogAlpha);
                    ueResult = lerp(sceneColor.rgb, fogColor, fogAlpha);
                    if (ueSheetOn)
                        ueResult = ApplyCloudSheet(ueResult, ue, _SheetOpacity, _SheetHeightY, _SheetColor.rgb,
                                                   _SheetEdgeShiftWorld, _SheetFadeWidthWorld, _SheetGroundAlign,
                                                   float2(1.0, 0.0), worldPos, gridInvX, t);
                }

                // ===== Fogged 스택 (탐사됨·비가시 룩) — 짙은 층이 완전히 덮는 픽셀은 평가 생략 =====
                half3 fgResult = sceneColor.rgb;
                if (stateBlend < 0.999 && (fgCellFogLow > 0.001 || fgSheetOn))
                {
                    FogStack fg = MakeFoggedStack();
                    half3 fogColor; float fogAlpha;
                    EvaluateStack(fg, worldPos, distVisW, ew, zoneDensity, zoneLayerOn, gridInvX, t, fogColor, fogAlpha);
                    fgResult = lerp(sceneColor.rgb, fogColor, fogAlpha);
                    if (fgSheetOn)
                        fgResult = ApplyCloudSheet(fgResult, fg, _FgSheetOpacity, _FgSheetHeightY, _FgSheetColor.rgb,
                                                   _FgSheetEdgeShiftWorld, _FgSheetFadeWidthWorld, _FgSheetGroundAlign,
                                                   float2(0.0, 1.0), worldPos, gridInvX, t);
                }

                // ===== 크로스페이드: 두 "완성된 룩"을 R 등고선 기준 전용 폭으로 섞는다 (가산 아님 → 경계 띠 없음) =====
                half3 result = lerp(fgResult, ueResult, stateBlend);
                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
}
