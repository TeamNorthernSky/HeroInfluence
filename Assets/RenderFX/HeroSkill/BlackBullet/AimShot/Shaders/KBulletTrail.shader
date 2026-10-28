// JC 블랙불릿 AimShot VFX — 탄도 궤적(획) 셰이더.
//
// 대상: TrailRenderer (Texture Mode = Stretch).
//   Stretch 모드 UV → uv.x = 길이 방향(궤적 전체에 한 번 매핑), uv.y = 폭 방향(0~1, 0.5가 중심선).
//
// ★uv.x 방향(실측 확정): TrailRenderer는 **머리(현재 트랜스폼 위치)를 uv.x=0**,
//   **꼬리(가장 오래된 점)를 uv.x=1** 로 매핑한다.
//   측정 방법 — 트랜스폼을 x=20에 두고 AddPosition으로 x=0, x=10을 넣고 BakeMesh:
//     x=20(머리) → uv.x 0.00 / x=10(중간) → 0.50 / x=0(꼬리) → 1.00
//   그래서 길이 좌표를 len = 1 - uv.x 로 뒤집어 **len: 0=꼬리, 1=머리**로 정규화한다.
//   (초기 구현은 len=uv.x로 두어 테이퍼와 머리 하이라이트가 꼬리에 걸리는 뒤집힘이 있었다.
//    토글로 두면 재발·혼동 소지가 있어 상수로 고정한다.)
//
// 하이라이트 축을 3개로 분리해 서로 간섭하지 않게 둔다(JusticeStrokeCore와 같은 정리):
//   ① 길이 방향  — 꼬리 테이퍼(_TailWidth/_TailFade/_TaperCurve) + 머리 하이라이트 + 양끝 페이드
//   ② 폭 방향    — 종(bell) 모양 몸통(_BodyFalloff) + 독립 중심선(_CoreWidth/_CoreSharp/_CoreColor)
//   ③ 시간 방향  — 오케스트레이터가 _FadeMul로 구동 (요소 생명주기)
//
// 몸통 단면을 평탄한 밴드로 두면 리본 덩어리처럼 보여 샤프함이 사라진다(저스티스에서 확인된 회귀).
// 그래서 중심에서 가장자리로 지수 감쇠하는 종 모양을 쓴다.
//
// 블렌드는 SrcAlpha One — 알파가 전체 세기를 다스리는 가산. 순수 One One보다 페이드 제어가 자연스럽다.
Shader "JC/VFX/KBulletTrail"
{
    Properties
    {
        // 아래 4색은 프리셋의 색 세트가 MPB로 구동한다(발광·탈색방지 적용 완료된 HDR 값).
        // 머티리얼 값은 프리셋이 없을 때의 폴백.
        [HDR]_HeadColor ("Head Color (탄두 쪽 테두리)", Color) = (0.85, 0.95, 1, 1)
        [HDR]_MidColor ("Mid Color (중간 테두리)", Color) = (0.45, 0.75, 1, 1)
        [HDR]_TailColor ("Tail Color (총구 쪽 테두리)", Color) = (0.20, 0.45, 0.9, 1)
        [HDR]_CoreColor ("Core Color (중심선 · 테두리와 독립)", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity (형상 게인 · 색 발광은 프리셋 담당)", Range(0,10)) = 1.0

        [Header(Width Profile)]
        _BodyFalloff ("Body Falloff (단면 감쇠 지수 — 클수록 샤프)", Range(0.1,8)) = 2.5
        _CoreWidth ("Core Width (폭 대비 비율)", Range(0.01,1)) = 0.35
        _CoreSharp ("Core Sharpness", Range(0.1,8)) = 2.0

        // 아래 3개는 프리셋이 MaterialPropertyBlock으로 구동한다(머티리얼 값은 폴백).
        [Header(Length Taper)]
        _TailWidth ("Tail Width Ratio", Range(0.01,1)) = 0.30
        _TailFade ("Tail Brightness Ratio", Range(0,1)) = 0.22
        _TaperCurve ("Taper Curve", Range(0.2,6)) = 1.15

        [Header(Head Highlight)]
        _HeadBoost ("Head Boost", Range(0,5)) = 1.2
        _HeadLength ("Head Highlight Length", Range(0.01,0.5)) = 0.08

        [Header(Ends)]
        _EndFade ("End Fade (양끝 페이드 구간 0~0.5)", Range(0,0.5)) = 0.06

        [Header(Flicker)]
        _NoiseScale ("Noise Scale", Range(0,40)) = 12
        _NoiseScroll ("Noise Scroll", Range(-20,20)) = -6
        _NoiseAmount ("Noise Amount", Range(0,1)) = 0.10

        _FadeMul ("Fade Multiplier", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _HeadColor; float4 _MidColor; float4 _TailColor; float4 _CoreColor;
                float _Intensity;
                float _BodyFalloff; float _CoreWidth; float _CoreSharp;
                float _TailWidth; float _TailFade; float _TaperCurve;
                float _HeadBoost; float _HeadLength;
                float _EndFade;
                float _NoiseScale; float _NoiseScroll; float _NoiseAmount;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash1(float n) { return frac(sin(n) * 43758.5453); }
            float noise1(float x)
            {
                float i = floor(x); float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(hash1(i), hash1(i + 1.0), f);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // len: 0 = 꼬리, 1 = 머리(탄두).
                // TrailRenderer는 머리를 uv.x=0으로 주므로 반드시 뒤집는다(위 주석의 실측 근거 참조).
                float len = saturate(1.0 - IN.uv.x);
                float d = abs(IN.uv.y - 0.5) * 2.0;   // 0 = 중심선, 1 = 가장자리

                // ── ① 길이 방향 테이퍼: 머리에서 1.0, 꼬리에서 _TailWidth/_TailFade로 수렴 ──
                float prof = pow(len, _TaperCurve);
                float widthProf = lerp(_TailWidth, 1.0, prof);
                float brightProf = lerp(_TailFade, 1.0, prof);

                // 미세 흔들림. 꼬리쪽일수록 약하게 실어 산만함을 줄인다.
                float n = noise1(len * _NoiseScale + _Time.y * _NoiseScroll);
                widthProf *= 1.0 + _NoiseAmount * (n - 0.5) * prof;

                float dw = d / max(widthProf, 1e-3);   // 테이퍼 반영 폭 좌표

                // ── ② 폭 방향: 종 모양 몸통 + 독립 중심선 ──
                float body = pow(saturate(1.0 - dw), max(_BodyFalloff, 0.1));
                float core = pow(saturate(1.0 - dw / max(_CoreWidth, 0.01)), max(_CoreSharp, 0.1));

                // 양끝(길이 방향) 페이드 — 머리·꼬리가 뚝 잘리지 않게
                float ef = max(_EndFade, 1e-4);
                float ends = smoothstep(0.0, ef, IN.uv.x) * smoothstep(1.0, 1.0 - ef, IN.uv.x);

                // 탄두 근처 하이라이트
                float head = exp(-(1.0 - len) / max(_HeadLength, 1e-3)) * _HeadBoost;

                // ── 공간축 3스톱 테두리 색: gt 0=탄두(머리) → 1=총구(꼬리) ──
                // 휘도 감쇠는 brightProf가 따로 담당하므로 여기서는 색만 다룬다(역할 분리).
                float gt = 1.0 - len;
                half3 bodyCol = gt < 0.5
                    ? lerp(_HeadColor.rgb, _MidColor.rgb, gt * 2.0)
                    : lerp(_MidColor.rgb, _TailColor.rgb, (gt - 0.5) * 2.0);

                // 정점색은 기본적으로 흰색(무해). TrailRenderer colorGradient를 쓰면 여기로 합성된다.
                half3 rgb = (bodyCol * (1.0 + head) + _CoreColor.rgb * core) * _Intensity * IN.color.rgb;
                half a = saturate(body + core) * ends * brightProf * _FadeMul * IN.color.a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
