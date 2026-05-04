Shader "Custom/JC/FogOfWar"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.75, 0.78, 0.85, 1.0)
        _FogDensityLow ("Fog Density - Low (Explored/Refogged)", Range(0, 1)) = 0.7
        _FogDensityLowUnexplored ("Fog Density - Low (Unexplored)", Range(0, 1)) = 1.0
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
            Name "FogOfWarPass"

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

            // Visibility 텍스처 2장 (PlayFogManager가 Shader.SetGlobalTexture로 설정)
            //   _VisibilityCurrentTex  : R8, 현재 프레임 시야 (0~1, smoothstep 경계)
            //   _VisibilityExploredTex : RGBA32, 탐색 누적
            //     R = 최하단 레이어 (5초 복원)
            //     G = 중간 레이어   (7초 복원)
            //     B = 최상단 레이어 (9초 복원)
            TEXTURE2D(_VisibilityCurrentTex);
            SAMPLER(sampler_VisibilityCurrentTex);
            TEXTURE2D(_VisibilityExploredTex);
            SAMPLER(sampler_VisibilityExploredTex);

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 그리드 월드 사이즈 (x,y = width*CellSize, height*CellSize / z,w = 1/x, 1/y)
            // PlayGridManager.Initialize()에서 Shader.SetGlobalVector로 설정
            float4 _GridWorldSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogDensityLow;
                float _FogDensityLowUnexplored;
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
            CBUFFER_END

            // ========== 노이즈 ==========

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

            // ========== 메인 ==========

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

                // Depth → 월드 좌표
                float depth = SampleSceneDepth(input.uv);

                #if UNITY_REVERSED_Z
                    bool isSky = depth < 0.0001;
                #else
                    bool isSky = depth > 0.9999;
                #endif
                if (isSky) return sceneColor;

                float2 posNDC = input.uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    posNDC.y = -posNDC.y;
                #endif

                float4 worldPos4 = mul(UNITY_MATRIX_I_VP, float4(posNDC, depth, 1.0));
                float3 worldPos = worldPos4.xyz / worldPos4.w;

                float2 gridUV = saturate(worldPos.xz * _GridWorldSize.zw);

                // Visibility 2장 샘플링
                //   current : 현재 시야 (0~1, smoothstep 경계)
                //   explored.rgb : 레이어별 탐색 누적 (R=low, G=mid, B=high)
                float current = SAMPLE_TEXTURE2D(_VisibilityCurrentTex, sampler_VisibilityCurrentTex, gridUV).r;
                float4 exploredFull = SAMPLE_TEXTURE2D(_VisibilityExploredTex, sampler_VisibilityExploredTex, gridUV);
                float3 explored = exploredFull.rgb;
                // explored.a = elapsed time since last seen. >0 means "this cell was explored at least once".
                float isExplored = step(0.001, exploredFull.a);

                // 레이어별 visibility = max(current, explored[layer])
                float visLow  = max(current, explored.r);
                float visMid  = max(current, explored.g);
                float visHigh = max(current, explored.b);

                // ===== 볼륨 기반 3단 전환 =====
                // Low  = 볼륨 내부 (y < ceiling - softness)   → 완전 불투명
                // Mid  = 볼륨 표면 (y ≈ ceiling)              → 구름 상단 패턴 (cloud 변조)
                // High = 볼륨 위   (y > ceiling + softness)   → 투명 (산 돌출)
                float worldY = worldPos.y;
                float s = _HeightTransition;
                float ceiling = _FogCeilingY;

                float lowFactor  = 1.0 - smoothstep(ceiling - s, ceiling, worldY);
                float highFactor = smoothstep(ceiling, ceiling + s, worldY);
                float midFactor  = max(0.0, 1.0 - lowFactor - highFactor);

                // 셀 기반 안개 유무 (visLow 우선)
                float cellFog = 1.0 - visLow;

                // Early out: 셀이 완전 시야 내
                if (cellFog < 0.001) return sceneColor;

                // ===== 노이즈: 운해 =====
                float t = _Time.y;
                float2 nUV = worldPos.xz;

                // 노이즈 주파수는 그리드 월드 크기(x축 기준)에 비례 정규화
                // → 맵이 커져도 "맵 안에서 _NoiseScale번 반복" 유지
                float gridInvX = _GridWorldSize.z;

                // Distortion
                float2 dIn = nUV * _NoiseScale3 * gridInvX + float2(t * _FlowSpeed3, t * _FlowSpeed3 * 0.7);
                float2 dist = float2(
                    GradientNoise(dIn) - 0.5,
                    GradientNoise(dIn + float2(43, 17)) - 0.5
                ) * _DistortionStrength;

                // Base
                float2 bIn = nUV * _NoiseScale1 * gridInvX + dist + float2(t * _FlowSpeed1, t * _FlowSpeed1 * 0.3);
                float nBase = FBM(bIn, 4);

                // Detail
                float2 dIn2 = nUV * _NoiseScale2 * gridInvX + dist * 0.5 + float2(-t * _FlowSpeed2 * 0.5, t * _FlowSpeed2 * 0.8);
                float nDetail = FBM(dIn2, 3);

                // 합성
                float noise = nBase * 0.55 + nDetail * 0.45;
                noise = saturate((noise - 0.5) * _NoiseContrast + 0.5);

                // 구름 밀도 (노이즈에 의한 두꺼움/얇음)
                float cloud = lerp(0.2, 1.0, noise);

                // ===== alpha 계산 =====
                // alpha는 오직 "얼마나 가리는가"만 담당. cloud 변조는 color로 이동했음.
                // Low 레이어는 탐색 여부에 따라 다른 density 사용:
                //   - Unexplored (한 번도 안 본 곳): 완전 불투명 (기본 1.0) → 지형 완전 은폐
                //   - Explored (탐색 후 refog됨): 반투명 (기본 0.7) → 지형이 fog 너머로 비침
                float effectiveLowDensity = lerp(_FogDensityLowUnexplored, _FogDensityLow, isExplored);
                float lowFog  = lowFactor  * effectiveLowDensity;
                float midFog  = midFactor  * _FogDensityMid;
                float highFog = highFactor * _FogDensityHigh;
                float alpha = saturate((lowFog + midFog + highFog) * cellFog);

                // ===== color 계산 =====
                // 레이어별 명도(아래 어두움, 위 밝음) × cloud 노이즈 변조
                // cloud 변조 범위 = (1 - contrast) ~ (1 + contrast)
                float layerBrightness = lowFactor  * _BrightnessLow
                                      + midFactor  * _BrightnessMid
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
