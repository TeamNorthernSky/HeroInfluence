// 루미나 「플레어 봄」 차징 오브 Alt — 레인 기반(획 우선) 오라 혀.
// 둘레를 _LaneCount개의 레인으로 나누고, 각 레인이 S자 경로를 따라 곡면에
// "그어진 획"이 된다. 획은 생명주기(랜덤 높이 탄생→상승 이동→소멸)를 돌며,
// 사이클마다 높이·길이·폭·기울기·위치가 재추첨된다. 획의 집합이 무늬를 이룬다.
// _EdgeSoftRatio 0=하드컷. 팁에서는 폭이 핀치되어 바늘끝으로 소멸(페이드 아님).
Shader "Testbed/FlareBomb/FlareAuraLane"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorTongue    ("Tongue Color",    Color) = (1.05, 0.62, 0.16, 1)
        [HDR] _ColorHighlight ("Highlight Color", Color) = (1.45, 1.25, 0.80, 1)
        _Emission ("Emission", Range(0,6)) = 1.2

        [Header(Lanes)]
        _LaneCount ("Lane Count (strokes around)", Range(3,24)) = 9
        _LaneWidth ("Stroke Half Width (0..0.5 lane)", Range(0.02,0.5)) = 0.16
        _LaneWidthJitter ("Per-lane Width Jitter", Range(0,1)) = 0.35
        _LanePosJitter ("Per-lane Position Jitter (cell)", Range(0,0.8)) = 0.35
        _LaneTiltJitter ("Per-lane Tilt Jitter (up kept, side slant)", Range(0,2)) = 0.5
        _WidthNoiseScale ("Width Waviness Scale", Range(0.5,12)) = 3.5
        _WidthNoiseAmount ("Width Waviness Amount", Range(0,1)) = 0.4

        [Header(Existence Band)]
        _VStart ("Stroke Start Height (v)", Range(0,1)) = 0.08
        _VEnd ("Stroke End Height (v)", Range(0,1)) = 0.82
        _VJitter ("Birth Height Spread (0=fixed, 0.5=full range)", Range(0,0.5)) = 0.25
        _BirthBias ("Bottom Birth Weight (1=uniform)", Range(1,4)) = 1.8
        _VLengthJitter ("Per-cycle Length Jitter", Range(0,1)) = 0.35
        _MaxLen ("Max Stroke Length (v, 0.25=1/8 circumference)", Range(0.08,1)) = 0.25
        _UpperShrink ("Upper Birth Shrink (equator boundary)", Range(0,1)) = 0.6

        [Header(Lifecycle)]
        _TravelDist ("Stroke Travel Distance (v)", Range(0.02,0.6)) = 0.22
        _TravelSpeed ("Stroke Rise Speed (vs Flow)", Range(0,1)) = 0.3

        [Header(Stroke Shape)]
        _TaperSharp ("Pupil Profile Sharpness (slim, pointy)", Range(0.5,6)) = 1.4
        _LifeFadePeak ("Life Fade Peak (0=start, 0.5=mid, 1=none)", Range(0,1)) = 0.4
        _HighlightRatio ("Inner Highlight Width Ratio", Range(0,1)) = 0.45
        _EdgeSoftRatio ("Edge Soft Ratio (0=hard cut)", Range(0,1)) = 0.25

        [Header(Motion)]
        _FlowSpeed ("Rise Speed", Range(0,6)) = 1.2
        _Shear ("Spiral Shear (0=pure rise)", Range(-2,2)) = 0
        _SCurveAmount ("S-Curve Amount (rad, cell-clamped)", Range(0,1.2)) = 0.45
        _SCurveFreq ("S-Curve Bends (len cap = 1 period)", Range(0.2,4)) = 1.2
        _SCurveUpperRatio ("S-Curve Upper Attenuation", Range(0,1)) = 0.4

        [Header(Tip Break)]
        _TipErodeStart ("Tip Pinch Start (v)", Range(0,1)) = 0.68
        _TipErodeStrength ("Tip Pinch Strength", Range(0,1.5)) = 0.9

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
        _Opacity ("Opacity", Range(0,1)) = 1.0
        _DebugOutline ("Debug Outline (teardrop silhouette)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareAuraLane"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TWO_PI 6.2831853

            // 사인 기반 정수 해시: 등간격 입력에서도 이웃 상관이 없다
            // (곱셈-frac 해시는 레인 인덱스처럼 선형 증가하는 입력에서 램프 아티팩트 발생)
            float shash(float n){ return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            float shash2(float a, float b){ return frac(sin(a * 127.1 + b * 269.5 + 74.7) * 43758.5453); }
            float hash21(float2 p){ p = frac(p*float2(123.34,345.45)); p += dot(p, p+34.345); return frac(p.x*p.y); }
            float vnoise2(float2 x){
                float2 i=floor(x), f=frac(x); f=f*f*(3.0-2.0*f);
                float a=hash21(i), b=hash21(i+float2(1,0)), c=hash21(i+float2(0,1)), d=hash21(i+float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 positionOS:TEXCOORD0; float2 uv:TEXCOORD1; float3 normalWS:TEXCOORD2; float3 positionWS:TEXCOORD3; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorTongue,_ColorHighlight;
                half _Emission;
                half _LaneCount,_LaneWidth,_LaneWidthJitter,_LanePosJitter,_LaneTiltJitter,_WidthNoiseScale,_WidthNoiseAmount;
                half _TaperSharp,_LifeFadePeak,_HighlightRatio,_EdgeSoftRatio;
                half _VStart,_VEnd,_VJitter,_BirthBias,_VLengthJitter,_MaxLen,_UpperShrink;
                half _TravelDist,_TravelSpeed;
                half _FlowSpeed,_Shear;
                half _SCurveAmount,_SCurveFreq,_SCurveUpperRatio;
                half _TipErodeStart,_TipErodeStrength;
                half _Cull,_Opacity,_DebugOutline;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;   // v = 셸 정규화 높이(0 하단..1 팁)
                return OUT;
            }

            // 한 후보 레인(자기 또는 이웃 셀)의 획 알파·하이라이트를 평가.
            // xLoc = 해당 셀 기준 로컬 가로 좌표(셀 중심 0).
            void EvalStroke(float lid, float xLoc, float v, float t,
                            out float stroke, out float highlight)
            {
                stroke = 0.0; highlight = 0.0;
                // 둘레 래핑에 안전한 레인 인덱스
                float laneW = lid - _LaneCount * floor(lid / _LaneCount);

                // ---- 생명주기: 획은 태어나 위로 이동하며 두드러졌다 소멸한다 ----
                float vSpeed = _FlowSpeed * _TravelSpeed;
                float cycleT = t * vSpeed / max(_TravelDist, 0.02) + shash(laneW);   // 레인 디싱크
                float cid = fmod(floor(cycleT), 64.0);                               // 해시 안정용 랩
                float phase = frac(cycleT);

                float rBirth  = shash2(laneW, cid);
                float rLen    = shash2(laneW + 37.7, cid);
                float rWidth  = shash2(laneW + 73.1, cid);
                float rTilt   = shash2(laneW + 11.3, cid);
                float rPos    = shash2(laneW + 29.7, cid);
                float rSPhase = shash2(laneW + 47.3, cid);
                float rMirror = shash2(laneW + 81.9, cid);
                float ph      = shash2(laneW + 91.3, cid) * 8.0;

                float widthJ = 1.0 + (rWidth - 0.5) * 2.0 * _LaneWidthJitter;

                // 생성 높이: 하단 확장(_TravelDist) + 하단 가중(_BirthBias) + 산포(_VJitter)
                float birthSpread = saturate(_VJitter * 2.0);
                float rBirthW = pow(rBirth, _BirthBias);
                float birthLo = _VStart - _TravelDist;
                float vs0 = birthLo + rBirthW * max(_VEnd - birthLo, 0.0) * birthSpread;
                float vs = vs0 + phase * _TravelDist;

                // 길이: 최대 한계(_MaxLen) 아래로 분포. 적도면(v=0.5) 위 생성은 길이·두께 축소.
                float lenJ = 1.0 - rLen * _VLengthJitter;
                float upperT = saturate((vs0 - 0.5) * 2.0);
                float shrink = 1.0 - upperT * _UpperShrink;
                float minLen = min(0.08, _MaxLen);
                float len = max(_MaxLen * lenJ * shrink, minLen);
                len = min(len, 1.0 / max(_SCurveFreq, 0.2));   // S 1주기를 다 그리면 소멸
                len = min(len, max(0.95 - vs, 0.06));          // 팁 직전 하드 상한
                float ve = vs + len;
                float mid = 0.5 * (vs + ve);

                // 고양이 동공(렌즈) 프로파일: 플래토+캡 대신 전 길이에 걸쳐
                // 매끄럽게 가늘어지는 단일 폭 함수. 중심선이 곡선을 유지해도
                // 폭이 연속이라 어디에도 꺾임(검끝)이 생기지 않는다.
                float t01 = saturate((v - vs) / max(ve - vs, 0.0001));
                float pupil = 4.0 * t01 * (1.0 - t01);           // 양끝 0, 중앙 1
                float band = pow(pupil, _TaperSharp);            // 지수↑ = 날씬·예리

                // 사이클별 위치·기울기 지터(이웃 셀 평가 덕에 셀 경계를 넘어도 잘리지 않음)
                float centerOff = (rPos - 0.5) * _LanePosJitter;
                float tilt = (rTilt - 0.5) * _LaneTiltJitter;

                // 획별 S커브: 위상·방향이 사이클마다 랜덤 → 획마다 S 궤도의 다른 구간을 그린다.
                // 진폭은 이웃 1셀 범위(0.5셀)로 클램프. 중심선은 전 구간 곡선 유지.
                float heightAtten = lerp(1.0, _SCurveUpperRatio, smoothstep(0.4, 0.6, v));
                float aCell = min(_SCurveAmount / TWO_PI * _LaneCount, 0.5);
                float sMirror = (rMirror < 0.5) ? -1.0 : 1.0;
                float sOff = sMirror * aCell * heightAtten
                           * sin(v * _SCurveFreq * TWO_PI + rSPhase * TWO_PI);

                float x = xLoc - centerOff - tilt * (v - mid) - sOff;

                // 폭 변조(물결) + 팁 핀치(폭 자체가 줄어 바늘끝 소멸, 페이드 아님)
                float wn = vnoise2(float2(laneW * 3.3, v * _WidthNoiseScale - t * _FlowSpeed + ph));
                float widthMod = lerp(1.0, wn, _WidthNoiseAmount);
                float tipBand = smoothstep(_TipErodeStart, 1.0, v);
                float halfW = _LaneWidth * widthJ * widthMod * band
                              * shrink   // 적도면 위 생성 획은 두께도 축소
                              * saturate(1.0 - tipBand * _TipErodeStrength);

                // 에지: 소프트 비율(팁에서는 하드로 수렴)
                float soft = lerp(0.004, 0.10, _EdgeSoftRatio);
                soft = lerp(soft, 0.004, tipBand);
                float ax = abs(x);
                stroke = smoothstep(halfW, halfW - soft, ax);
                highlight = smoothstep(halfW * _HighlightRatio, halfW * _HighlightRatio - soft, ax);

                // 생명주기 페이드(획 전체 균일 알파, 형태 불변):
                // _LifeFadePeak = 최대 알파 도달 시점.
                //   0   = 시작부터 최대 → 수명 끝에 0
                //   0.5 = 중간 피크(시작·끝 0)
                //   1   = 페이드 없음(상시 최대). 0.5~1은 무페이드로 연속 블렌드.
                float pk = _LifeFadePeak;
                float rise = (pk <= 0.001) ? 1.0 : smoothstep(0.0, pk, phase);
                float fall = 1.0 - smoothstep(pk, 1.0, phase);
                float env = rise * fall;
                float noFade = saturate((pk - 0.5) * 2.0);
                stroke *= lerp(env, 1.0, noFade);
            }

            half4 frag(Varyings IN):SV_Target{
                float t = _Time.y;
                float v = saturate(IN.uv.y);
                float3 dir = normalize(IN.positionOS);

                // 둘레 좌표(0..1) — 극점 근처는 방위각 정의가 흐려지나 폭이 0이라 무해
                float u = atan2(dir.x, dir.z) / TWO_PI + 0.5;

                // 전역 오프셋은 나선 시어만. S커브는 획별(per-stroke)로 셀 내부에서 적용.
                float uOff = (v * _Shear) / TWO_PI;

                // 레인 분해 + 이웃 셀 평가:
                // 자기 셀과 양옆 셀의 획을 함께 평가해 max 결합 → 지터·기울기·S로
                // 셀 경계를 넘는 획도 잘리지 않고 이웃 위로 자연스럽게 겹친다(1패스).
                float lanes = (u + uOff) * _LaneCount;
                float laneId = floor(lanes);

                float bestStroke = 0.0, bestHi = 0.0;
                [unroll]
                for (int o = -1; o <= 1; o++)
                {
                    float lid = laneId + o;
                    float xLoc = lanes - lid - 0.5;   // 해당 셀 기준 로컬 가로 좌표
                    float s, h;
                    EvalStroke(lid, xLoc, v, t, s, h);
                    if (s > bestStroke) { bestStroke = s; bestHi = h; }
                }

                half3 col = lerp(_ColorTongue.rgb, _ColorHighlight.rgb, bestHi);
                half a = bestStroke * _Opacity;

                // 디버그 아웃라인: 프레넬 실루엣 라인으로 티어드롭 형태를 표시(기본 0)
                if (_DebugOutline > 0.001)
                {
                    float3 N = normalize(IN.normalWS);
                    float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                    float fres = pow(saturate(1.0 - abs(dot(N, V))), 3.0);
                    float outline = smoothstep(0.55, 0.9, fres) * _DebugOutline;
                    if (outline > a) { col = half3(0.8, 0.9, 1.0); a = outline; }
                }

                if (a < 0.01) discard;
                return half4(col * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
