// JC 힐 스킬 VFX — 바닥 장판(E-5): 방사형 샤인 스파크. 바닥에 눕는 디스크(쿼드).
// 가산·양면(Cull Off). 절차적: 중심 광채(라디얼 falloff) + 방사형 스파크 광선(회전) + 외곽 소프트 수렴.
// 텍스처 의존 없음. 바깥 번짐은 씬 URP Bloom.
Shader "JC/VFX/HealGroundShine"
{
    Properties
    {
        [HDR]_Color ("Shine Color", Color) = (1.0, 0.88, 0.35, 1)
        _Intensity ("Intensity", Range(0,8)) = 1.8
        _Opacity ("Opacity", Range(0,1)) = 1.0
        [Header(Star Shape)]
        _DiscRadius ("Disc Radius (중심장판, 정규화)", Range(0.01,1)) = 0.72
        _RayRadius ("Ray Radius (레이 외곽, 정규화)", Range(0.01,1)) = 1.0
        _EdgeSoft ("Edge Softness (외곽 수렴 폭)", Range(0.01,1)) = 0.35
        _SideSoft ("Side Softness (레이 옆면 폭)", Range(0,0.3)) = 0.06
        _CenterFalloff ("Center Falloff (중심 몰림)", Range(0.2,6)) = 1.6
        [Header(Rays)]
        _RayDensity ("Ray Density (스포크 수)", Range(0,48)) = 14
        _RaySharp ("Ray Sharpness", Range(0.5,8)) = 2.5
        _RayRotSpeed ("Ray Rotate Speed (도/초)", Range(-180,180)) = 18
        _ValleyWidth ("Valley Plateau (골 평탄 비율)", Range(0,0.95)) = 0
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
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity; float _Opacity;
                float _DiscRadius; float _RayRadius; float _EdgeSoft; float _SideSoft; float _CenterFalloff;
                float _RayDensity; float _RaySharp; float _RayRotSpeed; float _ValleyWidth;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                float r = saturate(length(p) * 2.0);          // 0=중심, 1=쿼드 가장자리(=max(디스크,레이) 반경)

                // ★단일 별 실루엣: 경계 반지름 = 삼각파(선형, 골·꼭짓점 V자) 기반.
                //   sin파(골에서 평평→원호+부착 스파이크로 보임) 대신 삼각파 → 골→꼭짓점을 쉼없이 오르내리는 하나의 별.
                //   rayDensity=별 포인트 수(5=5망성…). 레이외곽 ≤ 디스크 → 순수 원판. 살짝 크면 뚱뚱한 샤인.
                //   raySharp: <1 뚱뚱·둥근 별 / 1 직선 엣지 별 / >1 오목 샤인(✦).
                float ang = atan2(p.y, p.x) + radians(_RayRotSpeed) * _Time.y;
                float N = round(_RayDensity);
                float Ro = max(_RayRadius, _DiscRadius);        // 레이 ≤ 디스크 → 원판으로 수렴
                float w = 0.0;
                float RbPrime = 0.0;                            // |dRb/dθ| (경계의 각도 방향 기울기, SDF 보정용)
                if (N >= 1.0)
                {
                    float ph = frac(ang * N / 6.2831853);      // 포인트당 1주기
                    float tri = 1.0 - abs(ph * 2.0 - 1.0);     // 삼각파: 0(골) ↔ 1(꼭짓점), 선형
                    // 골 플래토: 주기의 _ValleyWidth 비율만큼 골을 평평하게(경계=디스크 원호 유지).
                    //   0=연속 별 → 클수록 원호 구간↑ = "디스크+돌출 레이" 쪽으로. 꼭짓점(=1)은 유지.
                    float triR = saturate((tri - _ValleyWidth) / max(1.0 - _ValleyWidth, 1e-4));
                    w = pow(triR, _RaySharp);
                    // 해석적 기울기: |dtri/dθ|=N/π, 플래토 리맵 1/(1-vw) 배(플래토 구간은 0), 체인룰로 pow 미분.
                    float slopeAbs = tri > _ValleyWidth ? (N * 0.31830989) / max(1.0 - _ValleyWidth, 1e-4) : 0.0;
                    float wPrime = _RaySharp * pow(max(triR, 1e-3), _RaySharp - 1.0) * slopeAbs;
                    RbPrime = (Ro - _DiscRadius) * wPrime;
                }
                float Rb = max(lerp(_DiscRadius, Ro, w), 1e-4);

                // 밝기: 전역 반지름 기준(방향 무관 동일 그라데이션). 같은 월드 반지름=같은 밝기
                //   → 밝은 코어 디스크가 형체로 보이고, 꼭짓점은 끝으로 갈수록 자연히 옅어짐(샤인 느낌).
                //   실루엣(별 모양)은 아래 edge 마스크(로컬 Rb)가 담당.
                float radial = pow(saturate(1.0 - r), _CenterFalloff);
                // 경계 수렴 — 외곽/옆면 분리(커플링 해소):
                //   edgeOuter = 반지름 방향 밴드(원호·팁 담당, 기존 감각). 레이 두께와 무관.
                //   edgeSide  = 1차 SDF 근사 거리 + 별도의 작은 밴드(옆면 담당). 레이가 sideSoft보다
                //               두꺼우면 중심선은 온전히 밝음 → 플래토로 좁혀도 레이가 소멸하지 않음.
                float edgeOuter = smoothstep(0.0, max(Rb * _EdgeSoft, 1e-4), Rb - r);
                float grad = sqrt(1.0 + pow(RbPrime / max(r, 1e-3), 2.0));
                float dist = (Rb - r) / grad;                   // dist=(Rb-r)/√(1+(Rb'/r)²)
                dist = max(dist, _DiscRadius - r);              // 중심부 하한 보정(근사 오염 방지)
                float edgeSide = smoothstep(0.0, max(_SideSoft, 1e-4), dist);
                float edge = min(edgeOuter, edgeSide);

                float3 col = _Color.rgb * _Intensity * _Opacity * radial * edge * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
