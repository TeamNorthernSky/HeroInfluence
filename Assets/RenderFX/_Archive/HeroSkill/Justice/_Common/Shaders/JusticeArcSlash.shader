// 저스티스 대쉬 — 호 참격 (Arc Slash)
//
// 카메라를 향한 쿼드 1장에 초승달 호를 그린다. 7시 → 1시로 촤악 지나가는 참격.
// 캐릭터 모션(치비 체형의 짧은 다리)에 의존하지 않도록, 신체 궤적이 아니라
// **가상의 궤도를 셰이더가 직접** 그린다.
//
// 질감: 매끈한 밴드 하나가 아니라, 반경·굵기·각도 범위가 제각각인 획 여러 겹.
// 거친 펜선으로 휘갈긴 듯한 인상은 획마다 해시로 흔든 편차가 만든다.
//
// 진행: 파티클 Custom Vertex Stream AgePercent(uv.z)를 받아
//   1) 스윕 — 호가 시작각에서 끝각으로 자라난다
//   2) 앞머리 하이라이트 — 진행 선두가 밝게 빛난다
//   3) 잔상 감쇠 — 지나간 자리는 서서히 옅어진다 (전체 알파는 파티클 색이 곱해짐)
Shader "Testbed/Justice/ArcSlash"
{
    Properties
    {
        _HeadColor ("Head Color (시작쪽)", Color) = (1, 1, 1, 1)
        _MidColor ("Mid Color", Color) = (0.7, 0.85, 1, 1)
        _TailColor ("Tail Color (끝쪽)", Color) = (0.32, 0.48, 0.72, 1)
        _CoreColor ("Core Color (획 중심 하이라이트)", Color) = (1, 1, 1, 1)

        _AngStart ("Start Angle (deg, 12시=0 시계방향)", Float) = 210
        _AngEnd ("End Angle (deg)", Float) = 30
        _RadiusInner ("Radius Inner (0~1)", Float) = 0.45
        _RadiusOuter ("Radius Outer (0~1)", Float) = 0.92
        _StrokeCount ("Stroke Count (1~8)", Float) = 6
        _StrokeWidth ("Stroke Width (0~0.5)", Float) = 0.10
        _StrokeWidthJitter ("Stroke Width Jitter (0~1)", Float) = 0.6
        _StrokeAngJitter ("Stroke Angle Jitter (0~0.5)", Float) = 0.12
        _Taper ("Taper (끝 뾰족함)", Float) = 0.7
        _CoreRatio ("Core Ratio (0~1)", Float) = 0.35
        _CoreSharp ("Core Sharpness", Float) = 2.0
        _EdgeSoft ("Edge Softness", Float) = 0.5

        _SweepEnd ("Sweep End (수명 중 스윕이 끝나는 비율)", Float) = 0.45
        _HeadGlow ("Head Glow (앞머리 발광)", Float) = 2.5
        _HeadLen ("Head Length (앞머리 구간)", Float) = 0.15
        _TrailFade ("Trail Fade (잔상이 옅어지는 정도)", Float) = 0.55

        _Emission ("Emission", Float) = 2.0
        _Seed ("Seed (획 배치 리롤)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One          // 가산
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
                float3 uv : TEXCOORD0;      // z = AgePercent (Custom Vertex Stream)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float3 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _HeadColor, _MidColor, _TailColor, _CoreColor;
            float _AngStart, _AngEnd, _RadiusInner, _RadiusOuter;
            float _StrokeCount, _StrokeWidth, _StrokeWidthJitter, _StrokeAngJitter;
            float _Taper, _CoreRatio, _CoreSharp, _EdgeSoft;
            float _SweepEnd, _HeadGlow, _HeadLen, _TrailFade;
            float _Emission, _Seed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            float Hash(float n) { return frac(sin(n * 127.1 + _Seed * 311.7) * 43758.5453); }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv.xy * 2.0 - 1.0;
                float r = length(p);
                float age = saturate(IN.uv.z);

                // 시계 각도: 12시 = 0, 시계방향 증가 (기획 언어와 맞춤: 7시=210, 1시=30)
                float clockAng = degrees(atan2(p.x, p.y));       // -180..180
                if (clockAng < 0.0) clockAng += 360.0;

                // 시작→끝 각을 ★반시계방향 호로 정규화 (7시→6시→4시→1시, 아래를 지나 올려 벤다)
                float span = _AngStart - _AngEnd;
                if (span <= 0.0) span += 360.0;
                float rel = _AngStart - clockAng;
                if (rel < 0.0) rel += 360.0;
                if (rel > span) return half4(0, 0, 0, 0);        // 호 바깥
                float an = rel / span;                            // 0(시작) .. 1(끝)

                // 스윕: 수명 앞부분(_SweepEnd)에 호가 자라난다
                float sweep = saturate(age / max(_SweepEnd, 0.001));
                sweep = 1.0 - (1.0 - sweep) * (1.0 - sweep);      // ease-out — 초반이 빠르다("촤악")
                if (an > sweep) return half4(0, 0, 0, 0);         // 아직 안 지나간 구간

                // 획 겹치기 — 반경·굵기·각도 범위를 해시로 흔든다
                int count = (int)clamp(_StrokeCount, 1.0, 8.0);
                float body = 0.0;
                float core = 0.0;
                [unroll(8)]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= count) break;
                    float fi = (float)i;
                    float h1 = Hash(fi * 7.13);
                    float h2 = Hash(fi * 3.71);
                    float h3 = Hash(fi * 9.29);
                    float h4 = Hash(fi * 5.57);

                    // 이 획의 반경 위치와 굵기
                    float rc = lerp(_RadiusInner, _RadiusOuter, h1);
                    float w = _StrokeWidth * lerp(1.0 - _StrokeWidthJitter, 1.0, h2);

                    // 획마다 각도 범위를 조금씩 잘라 끝이 어긋난 붓자국을 만든다
                    float aStart = h3 * _StrokeAngJitter;
                    float aEnd = 1.0 - h4 * _StrokeAngJitter;
                    if (an < aStart || an > aEnd) continue;
                    float anL = (an - aStart) / max(aEnd - aStart, 0.001);

                    // 폭 프로파일: 중앙이 굵고 양끝이 뾰족한 초승달
                    float prof = pow(abs(sin(anL * 3.14159265)), max(_Taper, 0.05));
                    float halfW = max(w * prof, 1e-4);

                    float d = abs(r - rc) / halfW;               // 0=획 중심
                    float s = 1.0 - smoothstep(1.0 - _EdgeSoft, 1.0, d);
                    body = max(body, s);

                    float cd = d / max(_CoreRatio, 0.01);
                    core = max(core, pow(saturate(1.0 - cd), _CoreSharp) * s);
                }
                if (body <= 0.001) return half4(0, 0, 0, 0);

                // 색: 호 진행 방향으로 머리→중간→꼬리
                half3 grad = an < 0.5
                    ? lerp(_HeadColor.rgb, _MidColor.rgb, an * 2.0)
                    : lerp(_MidColor.rgb, _TailColor.rgb, an * 2.0 - 1.0);

                // 앞머리 하이라이트: 스윕 선두 근처가 밝게 빛난다
                float headDist = (sweep - an) / max(_HeadLen, 0.001);
                float head = saturate(1.0 - headDist);
                // 잔상 감쇠: 선두에서 멀수록 옅어진다
                float fade = lerp(1.0, saturate(1.0 - _TrailFade * headDist * _HeadLen * 4.0), step(0.0, headDist));

                half3 rgb = grad * _Emission + _CoreColor.rgb * core + grad * head * _HeadGlow;
                half a = body * fade * IN.color.a;                // 전체 페이드는 파티클 색이 준다
                return half4(rgb, a);                             // Blend SrcAlpha One — 알파는 블렌드가 곱한다
            }
            ENDHLSL
        }
    }
}
