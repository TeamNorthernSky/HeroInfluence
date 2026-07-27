// 저스티스 타격 섬광 — **타점에서 전방으로 뻗는** 빔.
//
// 이전 판은 u를 중앙 기준으로 접어(|u-0.5|) 양 끝이 수렴하는 대칭형이었다.
// 그래서 구조적으로 "중앙에서 퍼지는" 모양밖에 나오지 않았다.
// 여기서는 u를 그대로 쓴다: u=0이 타점(시작), u=1이 끝.
//
// 폭은 시작→끝 프로파일 하나로 표현한다.
//   테이퍼 : _StartWidth 1.0 → _EndWidth 0.0   (타점이 두껍고 끝이 뾰족)
//   확대   : _StartWidth 0.2 → _EndWidth 1.0   (부채꼴처럼 벌어짐)
// _WidthCurve가 변화의 급함을 정한다(1=선형, >1=끝에서 급변, <1=초반에 급변).
//
// 스프라이트는 1장 고정. 겹쳐서 스타버스트를 만들지 않고 형상은 전부 이 셰이더가 그린다.
Shader "Testbed/Justice/ImpactFlash"
{
    Properties
    {
        [HDR] _BodyColor ("Body Color (몸통)", Color) = (1, 0.1, 0.11, 1)
        [HDR] _CoreColor ("Core Color (하이라이트)", Color) = (1, 1, 1, 1)

        _StartWidth ("Start Width (타점 쪽 폭)", Range(0, 1)) = 1.0
        _EndWidth   ("End Width (끝 쪽 폭)", Range(0, 1)) = 0.0
        _WidthCurve ("Width Curve (폭 변화 곡선)", Range(0.1, 6)) = 1.0

        _CoreWidth ("Core Width (하이라이트 영역 넓이)", Range(0, 1)) = 0.25
        _CoreSharp ("Core Edge Sharpness", Range(0.1, 6)) = 1.6
        _BodySoft  ("Body Edge Softness", Range(0.1, 6)) = 1.2

        _HeadFade ("Head Fade (타점 쪽 페이드)", Range(0, 1)) = 0.0
        _TailFade ("Tail Fade (끝 쪽 페이드)", Range(0, 1)) = 0.35

        _EdgeFade ("Edge Fade (경계부터 잠식하는 정도)", Range(0, 1)) = 1.0
        _MaxAlpha ("Max Alpha (정규화 기준)", Range(0.001, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "JusticeImpactFlash"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor;
                half4 _CoreColor;
                half  _StartWidth;
                half  _EndWidth;
                half  _WidthCurve;
                half  _CoreWidth;
                half  _CoreSharp;
                half  _BodySoft;
                half  _HeadFade;
                half  _TailFade;
                half  _EdgeFade;
                half  _MaxAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float u = saturate(IN.uv.x);              // 0 = 타점, 1 = 끝
                float av = abs(IN.uv.y - 0.5) * 2.0;      // 0 = 중심선, 1 = 폭 가장자리

                // 시작 → 끝 폭 프로파일. 테이퍼와 확대를 같은 식으로 표현한다.
                float t = pow(u, _WidthCurve);
                float w = max(lerp(_StartWidth, _EndWidth, t), 1e-4);

                // ── 경계부터 잠식하는 나타남/사라짐 ──
                // 파티클의 colorOverLifetime 알파를 0~1 진행도로 정규화해서 쓴다.
                // _EdgeFade=1이면 균일하게 옅어지는 대신 **유효 폭이 수축**해
                // 위/아래 경계가 안쪽으로 먹혀 들어가고 중심선만 남다가 사라진다.
                float fade = saturate(IN.color.a / max(_MaxAlpha, 1e-4));
                float widthFade = lerp(1.0, fade, _EdgeFade);
                float wEff = max(w * widthFade, 1e-4);

                float body = saturate(1.0 - av / wEff);
                body = pow(body, _BodySoft);

                float coreHalf = max(_CoreWidth * wEff, 1e-4);
                float core = saturate(1.0 - av / coreHalf);
                core = pow(core, _CoreSharp);

                // 길이 방향 페이드 — 끝이 뚝 잘리지 않게(시작·끝점 페이드는 유지).
                float headMask = _HeadFade > 0.001 ? saturate(u / _HeadFade) : 1.0;
                float tailMask = _TailFade > 0.001 ? saturate((1.0 - u) / _TailFade) : 1.0;

                half3 rgb = lerp(_BodyColor.rgb, _CoreColor.rgb, core);
                rgb *= IN.color.rgb;

                // 경계 잠식으로 표현한 만큼 균일 알파 감쇠는 덜 적용한다.
                half uniformAlpha = lerp(IN.color.a, _MaxAlpha, _EdgeFade);
                half a = body * headMask * tailMask * uniformAlpha;
                if (a < 0.003) discard;

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
