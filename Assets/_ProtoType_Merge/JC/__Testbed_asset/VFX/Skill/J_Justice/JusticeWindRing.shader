// 바람무늬 방사광 — 안개 토러스(바닥 도넛 + 원통 벽)의 공용 셰이더.
// 갈리오 궁 장벽 테두리 참조: 날 선 벽면이 아니라 「금빛 안개 고리」 —
// 얼룩덜룩한 fbm 구름 패치가 천천히 회전·표류하고, 어느 방향으로도 경계가 뭉게하게 풀린다.
// 한 재질을 바닥/벽이 공유하고, 지오메트리 의존 파라미터([MPB])는 렌더러별 MPB로 덮는다.
// 노이즈 도메인은 각도를 단위원 좌표로 사상해 랩 이음새가 없다(주기성 자동).
Shader "Testbed/Justice/WindRing"
{
    Properties
    {
        _ColorA ("색 A (기본·흰)", Color) = (1, 1, 1, 1)
        _ColorB ("색 B (청)", Color) = (0.55, 0.8, 1, 1)
        _Emission ("발광 배수", Float) = 1.5
        _ColorNoiseTile ("색 노이즈 배율 (A↔B 얼룩 크기)", Float) = 1.2

        _NoiseScale ("알파 노이즈 밀도 (각도 방향)", Float) = 4
        _CrossScale ("알파 노이즈 밀도 (교차 방향)", Float) = 2
        _NoiseAmp ("알파 얼룩 강도 (0=균일 안개)", Range(0, 1)) = 0.7

        _SpinSpeed ("회전 속도 (회전/초)", Float) = 0.12
        _DriftSpeed ("표류 속도 (노이즈 자체 흐름)", Float) = 0.25

        _AlphaMax ("최대 불투명도", Range(0, 1)) = 0.35

        // ── 렌더러별 MPB로 덮이는 항목 (재질 값은 기본치) ──
        _Alpha ("[MPB] 수명 알파 엔벨로프", Range(0, 1)) = 1
        _Mode ("[MPB] 0=바닥 도넛 / 1=벽", Float) = 0
        _SpinMul ("[MPB] 회전 배속 (내벽 시차용)", Float) = 1
        _BandInner ("[MPB] 밴드 안쪽 (정규 0~1)", Range(0, 1)) = 0.55
        _BandOuter ("[MPB] 밴드 바깥 (정규 0~1)", Range(0, 1)) = 0.9
        _BandSoft ("[MPB] 밴드 경계 부드러움", Range(0.01, 0.6)) = 0.18
        _TearAmount ("[MPB] 소멸 가장자리 뜯김", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorA, _ColorB;
            float _Emission, _ColorNoiseTile;
            float _NoiseScale, _CrossScale, _NoiseAmp;
            float _SpinSpeed, _DriftSpeed;
            float _AlphaMax, _Alpha, _Mode, _SpinMul;
            float _BandInner, _BandOuter, _BandSoft, _TearAmount;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float fbm(float2 p)
            {
                return vnoise(p) * 0.55 + vnoise(p * 2.13 + 17.7) * 0.30 + vnoise(p * 4.31 + 41.3) * 0.15;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 도메인 좌표: ang = 각도(회전 단위), cross = 교차축, band = 밴드 판정축
                float ang, cross, band;
                if (_Mode < 0.5)
                {
                    // 바닥 도넛 — uv(0~1) 중심 극좌표. band = 반경(정규).
                    float2 c = i.uv * 2.0 - 1.0;
                    float r = length(c);
                    ang = atan2(c.y, c.x) / (2.0 * UNITY_PI);
                    cross = r;
                    band = r;
                }
                else
                {
                    // 벽 — u = 둘레(0~1), v = 높이(0~1). band = 높이.
                    ang = i.uv.x;
                    cross = i.uv.y;
                    band = i.uv.y;
                }

                // 회전: 각도를 시간에 따라 밀고, 단위원 사상으로 주기성 확보(이음새 없음).
                ang += _Time.y * _SpinSpeed * _SpinMul;
                float a2pi = ang * 2.0 * UNITY_PI;
                float2 circle = float2(cos(a2pi), sin(a2pi)) * _NoiseScale;
                float2 p = circle
                         + float2(cross * _CrossScale, cross * _CrossScale * 0.63)
                         + float2(_Time.y * _DriftSpeed, _Time.y * _DriftSpeed * -0.7);

                float n = fbm(p);                                   // 알파 얼룩·뜯김
                float n2 = fbm(p * _ColorNoiseTile * 0.37 + 5.2);   // 색 A↔B 얼룩(저주파)

                // 밴드: 안쪽은 부드럽게 차오르고, 바깥(소멸) 가장자리는 노이즈로 뜯겨 물러난다.
                float outerEdge = _BandOuter - _TearAmount * 0.35 * n;
                float alpha = smoothstep(_BandInner - _BandSoft, _BandInner, band)
                            * (1.0 - smoothstep(outerEdge, outerEdge + _BandSoft, band));

                alpha *= lerp(1.0 - _NoiseAmp, 1.0, n);   // 구름 얼룩
                alpha *= _Alpha * _AlphaMax;

                fixed3 col = lerp(_ColorA.rgb, _ColorB.rgb, n2) * _Emission;
                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
}
