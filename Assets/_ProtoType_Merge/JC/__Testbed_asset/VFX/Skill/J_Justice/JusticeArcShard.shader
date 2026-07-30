// 호 파편 — 이등변 삼각형을 절단선으로 깎아 만든 파편 조각.
//
// 형상 규칙(사용자 확정, 260730):
//   이등변 삼각형 T(꼭대기)·L(좌하)·R(우하)에서
//   좌변 위의 점 P와 우하 꼭지점 R을 잇는 절단선, 우변 위의 점 Q와 좌하 꼭지점 L을 잇는 절단선으로
//   깎아내고 **꼭지점 T가 포함된 조각**을 남긴다. 한쪽만 자르면 삼각형, 양쪽 자르면 사각형이 된다
//   (절단선의 한 끝이 늘 밑 꼭지점에 고정되어 밑변이 부분적으로 남지 않으므로 오각형은 나올 수 없다).
//
// 절단점 위치는 파티클마다 다른 난수로 정한다 — Custom Vertex Streams의 StableRandom을
//   TEXCOORD0.zw로 받아 무한 변주를 만든다(메시 에셋 불필요, 렌더러 메시 4개 제한도 없음).
//
// 채색: 볼록 다각형 거리장의 부호로 내부/테두리를 가른다.
//   내부 = 빛나는 흰색(_InnerColor), 테두리 = 이펙트 색(파티클 색 — 프리셋의 수명 그라데이션).
Shader "Testbed/Justice/ArcShard"
{
    Properties
    {
        _InnerColor ("내부 색 (빛나는 심)", Color) = (1, 1, 1, 1)
        _InnerEmission ("내부 발광 배수", Float) = 1.6
        _EdgeEmission ("테두리 발광 배수", Float) = 1.2

        _TriWidth ("삼각형 밑변 반폭 (uv)", Range(0.05, 1)) = 0.45
        _TriHeight ("삼각형 높이 반값 (uv)", Range(0.1, 1)) = 0.85

        _CutMin ("절단점 최소 (0=미절단, 0.5=최대 절단)", Range(0, 1)) = 0.25
        _CutMax ("절단점 최대", Range(0, 1)) = 0.75

        _EdgeWidth ("테두리 두께 (uv)", Range(0.001, 0.5)) = 0.08
        _EdgeSoft ("경계 부드러움", Range(0.001, 0.2)) = 0.012
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

            fixed4 _InnerColor;
            float _InnerEmission, _EdgeEmission;
            float _TriWidth, _TriHeight;
            float _CutMin, _CutMax;
            float _EdgeWidth, _EdgeSoft;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;   // xy = uv, zw = StableRandom(파티클별 고정 난수)
                fixed4 color : COLOR;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            /// 점 p가 직선 ab의 어느 쪽인가 — inside 쪽이 음수가 되도록 부호를 맞춘 거리.
            float HalfPlane(float2 p, float2 a, float2 b, float2 inside)
            {
                float2 e = b - a;
                float2 n = normalize(float2(e.y, -e.x));
                float d = dot(p - a, n);
                float s = dot(inside - a, n);
                return (s > 0) ? -d : d;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // uv(0~1) → 로컬 좌표(-1~1). +Y가 파편의 뾰족한 끝(진행 방향).
                float2 p = i.uv.xy * 2.0 - 1.0;

                float2 T = float2(0.0, _TriHeight);
                float2 L = float2(-_TriWidth, -_TriHeight);
                float2 R = float2(_TriWidth, -_TriHeight);

                // 삼각형 세 변
                float d = HalfPlane(p, T, L, R);
                d = max(d, HalfPlane(p, L, R, T));
                d = max(d, HalfPlane(p, R, T, L));

                // 절단선 2개 — 절단점은 파티클별 난수. t가 0이나 1에 가까우면 거의 깎이지 않는다.
                float tL = lerp(_CutMin, _CutMax, i.uv.z);
                float2 P = lerp(T, L, tL);
                d = max(d, HalfPlane(p, P, R, T));

                float tR = lerp(_CutMin, _CutMax, i.uv.w);
                float2 Q = lerp(T, R, tR);
                d = max(d, HalfPlane(p, L, Q, T));

                // 내부일수록 큰 값
                float inner = -d;
                float shape = smoothstep(0.0, _EdgeSoft, inner);
                if (shape <= 0.001) discard;

                // 테두리: 경계에서 _EdgeWidth 안쪽까지
                float edge = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth + _EdgeSoft, inner);

                fixed3 edgeCol = i.color.rgb * _EdgeEmission;
                fixed3 inCol = _InnerColor.rgb * _InnerEmission;
                fixed3 col = lerp(inCol, edgeCol, edge);

                return fixed4(col, shape * i.color.a);
            }
            ENDCG
        }
    }
}
