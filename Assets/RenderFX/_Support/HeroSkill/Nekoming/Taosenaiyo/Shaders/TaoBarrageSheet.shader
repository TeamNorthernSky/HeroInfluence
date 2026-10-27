// JC Taosenaiyo VFX — 배리지 경사 시트(tao_barrage, 260807).
// 원점(상공)에서 타격 영역으로 뻗는 쿼드 시트 1장에 「방사 수렴 스트릭 + 노이즈 베일」을 절차 생성.
// 규약: 쿼드 로컬 +Y = 빔 축(원점→지면). uv.y 0 = 원점 끝 / 1 = 지면 끝.
// ★수렴(_Converge): 스트릭의 가로 좌표를 v 로 나눠 원점 한 점으로 모이게 — 코어와 경계 없는 발산.
// ★베일(_VeilStrength): 같은 시트에 노이즈 일렁임 바탕을 겹침 — 별도 셰이더 없이 한 장으로.
// 가산·양면·ZWrite Off — TaoAuraFlare 컨벤션 계승. 엔벨로프는 _FadeMul(MPB).
Shader "JC/VFX/TaoBarrageSheet"
{
    Properties
    {
        [Header(Veil Base)]
        [HDR]_Color ("Veil Color", Color) = (1.0, 0.82, 0.3, 1)
        _Intensity ("Veil Intensity", Range(0,8)) = 0.8
        _NoiseScale ("Veil Noise Scale", Range(0.5,20)) = 6.0
        _NoiseSpeed ("Veil Noise Flow Speed", Range(0,8)) = 1.8
        _VeilStrength ("Veil Strength (0=off)", Range(0,2)) = 0.7
        [Header(Streaks)]
        [HDR]_LineColor ("Streak Color", Color) = (1.0, 0.9, 0.45, 1)
        _LineIntensity ("Streak Intensity", Range(0,12)) = 4.0
        _StreakCount ("Streak Column Count", Range(4,120)) = 46
        _StreakWidth ("Streak Width in Cell", Range(0.02,0.6)) = 0.16
        _StreakSharp ("Streak Edge Sharpness", Range(0.005,0.3)) = 0.04
        _DashFreq ("Dash Frequency (along beam)", Range(0.2,12)) = 2.6
        _DashDuty ("Dash Duty (fill)", Range(0.1,1)) = 0.7
        _ScrollSpeed ("Flow Speed (origin to ground)", Range(-8,8)) = 2.4
        _Converge ("Converge To Origin (0=parallel)", Range(0,1)) = 1.0
        [Header(Shape)]
        _EdgeFade ("Side Edge Fade (u)", Range(0.005,0.5)) = 0.18
        _TailFade ("Ground End Fade (v)", Range(0.01,0.9)) = 0.35
        _HeadBoost ("Origin End Brightness Boost", Range(0,6)) = 2.2
        _HeadBoostPow ("Origin Boost Concentration", Range(0.5,8)) = 2.6
        _Opacity ("Opacity", Range(0,1)) = 1.0
        [Header(Fade)]
        _FadeMul ("Fade Multiplier", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Intensity;
                float _NoiseScale; float _NoiseSpeed; float _VeilStrength;
                float4 _LineColor; float _LineIntensity;
                float _StreakCount; float _StreakWidth; float _StreakSharp;
                float _DashFreq; float _DashDuty; float _ScrollSpeed; float _Converge;
                float _EdgeFade; float _TailFade; float _HeadBoost; float _HeadBoostPow;
                float _Opacity; float _FadeMul;
            CBUFFER_END

            float hash1(float n) { return frac(sin(n) * 43758.5453); }
            float hash13(float3 p){ p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float noise3(float3 x){
                float3 i = floor(x); float3 f = frac(x); f = f*f*(3.0-2.0*f);
                float n000=hash13(i+float3(0,0,0)), n100=hash13(i+float3(1,0,0));
                float n010=hash13(i+float3(0,1,0)), n110=hash13(i+float3(1,1,0));
                float n001=hash13(i+float3(0,0,1)), n101=hash13(i+float3(1,0,1));
                float n011=hash13(i+float3(0,1,1)), n111=hash13(i+float3(1,1,1));
                return lerp(lerp(lerp(n000,n100,f.x),lerp(n010,n110,f.x),f.y),
                            lerp(lerp(n001,n101,f.x),lerp(n011,n111,f.x),f.y), f.z);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float u = IN.uv.x;
                float v = IN.uv.y;                       // 0 = 원점 끝, 1 = 지면 끝
                float xc = u - 0.5;

                // ★방사 수렴 좌표 — v 가 작을수록(원점 근처) 가로가 좁아져 스트릭이 한 점으로 모인다.
                float conv = lerp(1.0, max(v, 0.02), _Converge);
                float xEff = xc / conv;                  // 수렴 후 가로(원점에서 발산되는 부채 좌표)
                // 수렴으로 시트 범위(±0.5)를 벗어난 각도는 소거 — 원점 근처 가장자리 삐침 방지.
                float fan = 1.0 - smoothstep(0.42, 0.5, abs(xEff));

                // ── 스트릭: 부채 좌표 컬럼 해시 + 빔 방향 대시 스크롤 (TaoAuraFlare 펜선 이식) ──
                float nCol = round(_StreakCount);
                float colf = saturate(xEff + 0.5) * nCol;
                float ci = floor(colf);
                float fx = frac(colf);
                float h1 = hash1(ci);
                float h2 = hash1(ci + 57.3);
                float w = _StreakWidth * lerp(0.55, 1.45, h2) * 0.5;
                float cxc = 0.5 + (h1 - 0.5) * (1.0 - w * 2.0);
                float lineMask = 1.0 - smoothstep(w, w + max(_StreakSharp, 1e-4), abs(fx - cxc));
                float dash = smoothstep(1.0 - _DashDuty, 1.0, frac(v * _DashFreq + h2 * 7.0 - _Time.y * _ScrollSpeed));
                float lines = lineMask * dash * lerp(0.6, 1.35, h1);

                // ── 베일: 노이즈 일렁임 바탕(부채 좌표계라 원점으로 같이 수렴) ──
                float n = noise3(float3(xEff * _NoiseScale, v * _NoiseScale * 1.6 - _Time.y * _NoiseSpeed, h1 * 0.0 + 3.7));
                float n2 = noise3(float3(xEff * _NoiseScale * 2.3 + 11.0, v * _NoiseScale * 3.1 - _Time.y * _NoiseSpeed * 1.7, 9.1));
                float veil = saturate(n * 0.65 + n2 * 0.45) * _VeilStrength;

                // ── 마스크: 좌우 보더(하드컷 방지) + 지면 끝 페이드 + 원점 부스트 ──
                float edgeU = smoothstep(0.0, _EdgeFade, u) * smoothstep(0.0, _EdgeFade, 1.0 - u);
                float tail = 1.0 - smoothstep(1.0 - _TailFade, 1.0, v);
                float head = 1.0 + _HeadBoost * pow(saturate(1.0 - v), _HeadBoostPow);
                float mask = edgeU * fan * tail;

                float3 col = (_Color.rgb * _Intensity * veil
                            + _LineColor.rgb * _LineIntensity * lines)
                           * mask * head * _Opacity * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    CustomEditor "JC.VFX.TaoBarrageSheetGUI"
    Fallback Off
}
