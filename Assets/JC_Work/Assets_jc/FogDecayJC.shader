// ============================================================================
// FogDecayJC
// RT_Explored(ARGBFloat)를 갱신한다.
// 채널 구성:
//   R = 최하단 레이어 복원 진행도 (1 = 탐색 유지, 0 = 완전 안개 복원)
//   G = 중간 레이어
//   B = 최상단 레이어
//   A = 탐색 종료 후 경과 시간(초). 시야 내이면 0으로 리셋.
//
// 동작:
//   - 시야 내(current>0): RGB=1, A=0으로 리셋
//   - 시야 밖이지만 이전에 탐색됨: A에 dt를 누적, 레이어별 "대기 + 선형 복원"
//   - 한 번도 탐색 안 됨: (0,0,0,0) 유지
//
// 복원 공식 (레이어별):
//   progress = saturate((elapsed - delay) / duration)
//   visibility = 1 - progress
//   → elapsed < delay:       visibility = 1 (대기 중, 값 유지)
//   → elapsed = delay + dur: visibility = 0 (완전 복원)
// ============================================================================
Shader "Custom/JC/FogDecay"
{
    Properties { }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "FogDecayPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_ExploredTex);
            SAMPLER(sampler_ExploredTex);

            TEXTURE2D(_CurrentTex);
            SAMPLER(sampler_CurrentTex);

            // 외부 입력 (PlayFogManager가 cmd.SetGlobalVector/Float로 전달)
            float4 _RestoreDelays;   // xyz = (low, mid, high) 대기 시간(초)
            float _RestoreDuration;  // 복원 시작~종료 소요 시간(초) - 모든 레이어 공통
            float _FogDeltaTime;     // 이번 프레임 dt(초)

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 prev = SAMPLE_TEXTURE2D(_ExploredTex, sampler_ExploredTex, input.uv);
                float3 prevRGB = prev.rgb;
                float prevElapsed = prev.a;
                float current = SAMPLE_TEXTURE2D(_CurrentTex, sampler_CurrentTex, input.uv).r;

                // 1) 현재 시야 안이면 모든 것 리셋
                if (current > 0.001)
                {
                    return half4(1, 1, 1, 0);
                }

                // 2) 한 번이라도 탐색된 적 있는지
                bool hasBeenSeen = (prevRGB.r > 0.001) || (prevRGB.g > 0.001) || (prevRGB.b > 0.001) || (prevElapsed > 0.001);

                if (!hasBeenSeen)
                {
                    // 한 번도 탐색 안 됨 → 완전 안개 유지
                    return half4(0, 0, 0, 0);
                }

                // 3) 탐색된 적 있음 → 경과 시간 누적, 레이어별 복원 계산
                float elapsed = min(prevElapsed + _FogDeltaTime, 100000.0);

                float duration = max(0.01, _RestoreDuration);
                float3 progress = saturate((elapsed.xxx - _RestoreDelays.xyz) / duration);
                float3 visibility = float3(1, 1, 1) - progress;

                return half4(visibility, elapsed);
            }
            ENDHLSL
        }
    }
}
