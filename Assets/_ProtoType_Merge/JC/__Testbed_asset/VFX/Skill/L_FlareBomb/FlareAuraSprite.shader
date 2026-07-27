// 루미나 「플레어 봄」 차징 오브 F2 — 2D 스프라이트 오라 혀.
// 시선축 정렬 빌보드 쿼드 위에서, 하단 발산점 E에서 경계 타원을 향해 그은
// 직선(레인)을 따라 획을 그린다.
// - 파란 경계(_BaseRadius × _EllipseRatio): 획 생성 위치 = 레인 직선과 경계의 원근 교점.
//   기본은 정원, _EllipseRatio로 타원화(정준 공간 y/k 변환 — 교차 수식 무변경 재사용).
// - E(_EmitterDepth): 중심 아래 -m·R. 1=하단 접점, 2=접점에서 반지름만큼 아래.
// - 임계높이(_CutHeight): 교점이 이보다 낮아지는 레인은 도메인 리매핑으로 원천 배제
//   (레인 전량이 유효 부채꼴 [-φmax, +φmax]만 N등분 — 낮은 생성이 확률적으로도 불가).
// - 녹색 원(_MaxReach): O 중심 강제 종료 경계(폭이 강제로 줄며 소멸, 생명주기와 독립).
// - 생명주기·지터·동공 프로파일·수명 페이드는 F1(FlareAuraLane)에서 이식.
// - 후면 레이어 없음. 큐 3002(코어 위, 최상단)로 코어를 가린다.
Shader "Testbed/FlareBomb/FlareAuraSprite"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorTongue    ("Tongue Color",    Color) = (1.05, 0.62, 0.16, 1)
        [HDR] _ColorHighlight ("Highlight Color", Color) = (1.45, 1.25, 0.80, 1)
        _Emission ("Emission", Range(0,6)) = 1.2

        [Header(Boundary UV)]
        _BaseRadius ("Boundary Radius (blue, horizontal)", Range(0.05,0.9)) = 0.42
        _EllipseRatio ("Boundary Vertical Ratio (1=circle)", Range(0.5,1.8)) = 1.0
        _MaxReach ("Max Reach Radius (green, force kill)", Range(0.1,1)) = 0.75
        _ReachRatio ("Reach Vertical Ratio (1=circle)", Range(0.5,1.8)) = 1.0
        _ReachSoft ("Reach Kill Softness", Range(0.01,0.3)) = 0.08
        _YOffset ("Pattern Y Offset (uv)", Range(-0.5,0.5)) = 0

        [Header(Emitter Fan)]
        _EmitterDepth ("Emitter Depth (1=bottom tangent, 2=+1R)", Range(1,2)) = 1.2
        _CutHeight ("Spawn Cut Height (-1=bottom, 1=top)", Range(-1,0.85)) = -0.6

        [Header(Lanes)]
        _LaneCount ("Lane Count (strokes in fan)", Range(3,32)) = 12
        _LaneWidth ("Stroke Half Width (0..0.5 cell)", Range(0.02,0.5)) = 0.16
        _LaneWidthJitter ("Per-stroke Width Jitter", Range(0,1)) = 0.35
        _LanePosJitter ("Per-stroke Position Jitter (cell)", Range(0,0.8)) = 0.35
        _LaneTiltJitter ("Per-stroke Tilt Jitter", Range(0,2)) = 0.5
        _WidthNoiseScale ("Width Waviness Scale", Range(0.5,12)) = 3.5
        _WidthNoiseAmount ("Width Waviness Amount", Range(0,1)) = 0.4

        [Header(Birth Band Boundary Relative)]
        _BirthMin ("Birth Offset Min (minus is inside)", Range(-0.5,0.5)) = -0.06
        _BirthMax ("Birth Offset Max", Range(-0.5,0.5)) = 0.05
        _BirthBias ("Inner Birth Weight (1=uniform)", Range(1,4)) = 1.8
        _VLengthJitter ("Per-cycle Length Jitter", Range(0,1)) = 0.35
        _MaxLen ("Max Stroke Length (uv)", Range(0.05,0.8)) = 0.22
        _OuterShrink ("Outer Birth Shrink (past boundary)", Range(0,1)) = 0.6
        _CutFarShrink ("Far From Cut Shrink (high spawn smaller)", Range(0,1)) = 0

        [Header(Lifecycle)]
        _TravelDist ("Stroke Travel Distance (uv)", Range(0.02,0.5)) = 0.15
        _TravelSpeed ("Stroke Rise Speed (vs Flow)", Range(0,1)) = 0.3
        _LifeFadePeak ("Life Fade Peak (0=start, 0.5=mid, 1=none)", Range(0,1)) = 0.4

        [Header(Stroke Shape)]
        _TaperSharp ("Pupil Profile Sharpness", Range(0.5,6)) = 1.4
        _HighlightRatio ("Inner Highlight Width Ratio", Range(0,1)) = 0.45
        _EdgeSoftRatio ("Edge Soft Ratio (0=hard cut)", Range(0,1)) = 0.25

        [Header(Motion)]
        _FlowSpeed ("Flow Speed", Range(0,6)) = 1.2
        _SCurveAmount ("S-Curve Amount (rad, cell-clamped)", Range(0,1.2)) = 0.45
        _SCurveFreq ("S-Curve Bends (len cap = 1 period)", Range(0.2,6)) = 2.0

        [Header(Debug)]
        _DebugCircles ("Debug Guides (boundary, reach, cut, E)", Range(0,1)) = 0

        [Header(Global)]
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareAuraSprite"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TWO_PI 6.2831853

            float shash(float n){ return frac(sin(n * 127.1 + 311.7) * 43758.5453); }
            float shash2(float a, float b){ return frac(sin(a * 127.1 + b * 269.5 + 74.7) * 43758.5453); }
            float hash21(float2 p){ p = frac(p*float2(123.34,345.45)); p += dot(p, p+34.345); return frac(p.x*p.y); }
            float vnoise2(float2 x){
                float2 i=floor(x), f=frac(x); f=f*f*(3.0-2.0*f);
                float a=hash21(i), b=hash21(i+float2(1,0)), c=hash21(i+float2(0,1)), d=hash21(i+float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorTongue,_ColorHighlight;
                half _Emission;
                half _BaseRadius,_EllipseRatio,_MaxReach,_ReachRatio,_ReachSoft,_YOffset;
                half _EmitterDepth,_CutHeight;
                half _LaneCount,_LaneWidth,_LaneWidthJitter,_LanePosJitter,_LaneTiltJitter,_WidthNoiseScale,_WidthNoiseAmount;
                half _BirthMin,_BirthMax,_BirthBias,_VLengthJitter,_MaxLen,_OuterShrink,_CutFarShrink;
                half _TravelDist,_TravelSpeed,_LifeFadePeak;
                half _TaperSharp,_HighlightRatio,_EdgeSoftRatio;
                half _FlowSpeed;
                half _SCurveAmount,_SCurveFreq;
                half _DebugCircles;
                half _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                // 시선축 정렬 빌보드(CoreRim과 동일 방식)
                float3 centerWS = TransformObjectToWorld(float3(0,0,0));
                float3 fwd = normalize(centerWS - GetCameraPositionWS());
                float3 upRef = abs(fwd.y) > 0.99 ? float3(0,0,1) : float3(0,1,0);
                float3 right = normalize(cross(upRef, fwd));
                float3 up = cross(fwd, right);
                float sx = length(unity_ObjectToWorld._m00_m10_m20);
                float sy = length(unity_ObjectToWorld._m01_m11_m21);
                float3 posWS = centerWS
                             + right * IN.positionOS.x * sx
                             + up    * IN.positionOS.y * sy;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 한 후보 레인(자기/이웃)의 획을 평가.
            // angLoc = 레인 로컬 가로 좌표(셀 단위, 중심 0), r = E 기준 거리,
            // tBase = 레인 직선-경계 원근 교점 거리, cellAng = 셀 각폭(rad).
            void EvalStroke2D(float lid, float angLoc, float r, float t, float tBase, float cellAng,
                              float reachKill, float hFar, out float stroke, out float highlight)
            {
                stroke = 0.0; highlight = 0.0;

                // 생명주기 (F1 이식): 사이클마다 전 랜덤 재추첨
                float vSpeed = _FlowSpeed * _TravelSpeed;
                float cycleT = t * vSpeed / max(_TravelDist, 0.02) + shash(lid);
                float cid = fmod(floor(cycleT), 64.0);
                float phase = frac(cycleT);

                float rBirth  = shash2(lid, cid);
                float rLen    = shash2(lid + 37.7, cid);
                float rWidth  = shash2(lid + 73.1, cid);
                float rTilt   = shash2(lid + 11.3, cid);
                float rPos    = shash2(lid + 29.7, cid);
                float rSPhase = shash2(lid + 47.3, cid);
                float rMirror = shash2(lid + 81.9, cid);
                float ph      = shash2(lid + 91.3, cid) * 8.0;

                float widthJ = 1.0 + (rWidth - 0.5) * 2.0 * _LaneWidthJitter;

                // 생성 = 경계 교점 + 상대 오프셋 밴드. 내측 확장(_TravelDist)으로 유량 정상화.
                float birthHi = max(_BirthMin, _BirthMax);
                float birthLo = min(_BirthMin, _BirthMax) - _TravelDist;
                float off0 = birthLo + pow(rBirth, _BirthBias) * max(birthHi - birthLo, 0.0);
                float rs0 = tBase + off0;
                float rs = rs0 + phase * _TravelDist;

                // 경계 밖 오프셋으로 태어난 획일수록 길이·두께 축소
                float outerT = saturate((rs0 - tBase) / max(birthHi, 0.02));
                float shrink = 1.0 - outerT * _OuterShrink;
                shrink *= 1.0 - hFar * _CutFarShrink;   // 임계선에서 높이 먼 생성일수록 축소
                float lenJ = 1.0 - rLen * _VLengthJitter;
                float minLen = min(0.05, _MaxLen);
                float len = max(_MaxLen * lenJ * shrink, minLen);
                len = min(len, 1.0 / max(_SCurveFreq, 0.2));   // S 1주기를 다 그리면 소멸
                float re = rs + len;
                float mid = 0.5 * (rs + re);

                // 고양이 동공(렌즈) 프로파일 — 전 길이 연속 폭 함수(꺾임 없음)
                float t01 = saturate((r - rs) / max(re - rs, 0.0001));
                float pupil = 4.0 * t01 * (1.0 - t01);
                float band = pow(pupil, _TaperSharp);

                // 셀 내 위치·기울기 지터 (진행 방향 자체는 레인 직선 = E 발산 방향)
                float centerOff = (rPos - 0.5) * _LanePosJitter;
                float tilt = (rTilt - 0.5) * _LaneTiltJitter;

                // 획별 S커브: 위상·방향 랜덤, 진폭은 이웃 1셀(0.5셀) 클램프
                float aCell = min(_SCurveAmount / max(cellAng, 0.001), 0.5);
                float sMirror = (rMirror < 0.5) ? -1.0 : 1.0;
                float sOff = sMirror * aCell * sin(r * _SCurveFreq * TWO_PI + rSPhase * TWO_PI);

                float x = angLoc - centerOff - tilt * (r - mid) - sOff;

                // 폭 변조 + 녹색원 강제 종료(생명주기와 독립: 폭이 강제로 줄며 소멸)
                float wn = vnoise2(float2(lid * 3.3, r * _WidthNoiseScale - t * _FlowSpeed + ph));
                float widthMod = lerp(1.0, wn, _WidthNoiseAmount);
                float halfW = _LaneWidth * widthJ * widthMod * band * shrink * reachKill;

                float soft = lerp(0.004, 0.10, _EdgeSoftRatio);
                float ax = abs(x);
                stroke = smoothstep(halfW, halfW - soft, ax);
                highlight = smoothstep(halfW * _HighlightRatio, halfW * _HighlightRatio - soft, ax);

                // 생명주기 페이드(획 전체 균일 알파): 피크 위치 슬라이더
                float pk = _LifeFadePeak;
                float rise = (pk <= 0.001) ? 1.0 : smoothstep(0.0, pk, phase);
                float fall = 1.0 - smoothstep(pk, 1.0, phase);
                float env = rise * fall;
                float noFade = saturate((pk - 0.5) * 2.0);
                stroke *= lerp(env, 1.0, noFade);
            }

            half4 frag(Varyings IN):SV_Target{
                float t = _Time.y;
                float2 p = IN.uv * 2.0 - 1.0;   // 실공간
                p.y -= _YOffset;                // 패턴 전체 Y 오프셋(경계·E·임계선·도달한계 동반 이동)
                // 도달한계 타원 반경(정규화): _MaxReach 비교용
                float kr = max(_ReachRatio, 0.1);
                float rReach = length(float2(p.x, p.y / kr));

                // 정준 공간: y/k 스케일로 경계 타원 → 반경 R 정원 (교차 수식 그대로 재사용)
                float k = max(_EllipseRatio, 0.1);
                float R = _BaseRadius;
                float2 q = float2(p.x, p.y / k);
                float m = _EmitterDepth;               // E = (0, -m·R), m∈[1,2]
                float2 Ec = float2(0.0, -m * R);

                // 임계높이 c·R → 유효 부채꼴 반각 φmax.
                // cosφ = (m+c)/√((m+c)²+1-c²) = E→(임계 교점) 방향. 접선 한계와 병합.
                float c = clamp(_CutHeight, -0.999, 0.85);
                float mc = max(m + c, 0.001);
                float uCut = mc / sqrt(mc * mc + max(1.0 - c * c, 0.0001));
                float uTan = (m > 1.001) ? sqrt(1.0 - 1.0 / (m * m)) : 0.0;
                float uMin = clamp(max(uCut, uTan + 0.001), 0.0, 0.9999);
                float phiMax = acos(uMin);

                float2 rel = q - Ec;
                float rE = length(rel);
                float phi = atan2(rel.x, rel.y);   // 0 = 상향(E 기준)

                // 도메인 리매핑: 유효 부채꼴만 N등분 → 임계 아래 생성이 구조적으로 불가
                float cellAng = 2.0 * phiMax / _LaneCount;
                float lanes = (phi + phiMax) / cellAng;
                float laneId = floor(lanes);

                float reachKill = smoothstep(_MaxReach, _MaxReach - _ReachSoft, rReach);

                // 이웃 3셀 평가(경계 잘림 방지·겹침 허용). 부채꼴 밖 레인은 없음(랩 없음).
                float bestStroke = 0.0, bestHi = 0.0;
                [unroll]
                for (int o = -1; o <= 1; o++)
                {
                    float lid = laneId + o;
                    if (lid < 0.0 || lid > _LaneCount - 1.0) continue;
                    float angLoc = lanes - lid - 0.5;
                    float phiL = -phiMax + (lid + 0.5) * cellAng;
                    float uL = cos(phiL);
                    float disc = max(m * m * uL * uL - m * m + 1.0, 0.0);
                    float tBase = R * (m * uL + sqrt(disc));   // 원근(+) 교점 거리
                    // 생성 교점의 임계선 대비 정규화 높이(0=임계선, 1=상단 극점)
                    float yB = -m * R + tBase * uL;
                    float hFar = saturate((yB - c * R) / max(R - c * R, 0.001));
                    float s, h;
                    EvalStroke2D(lid, angLoc, rE, t, tBase, cellAng, reachKill, hFar, s, h);
                    if (s > bestStroke) { bestStroke = s; bestHi = h; }
                }

                half3 col = lerp(_ColorTongue.rgb, _ColorHighlight.rgb, bestHi);
                half a = bestStroke * _Opacity;

                // 디버그 가이드: 경계 타원(청백)+도달한계(녹색)+임계선(주황)+E(적)
                if (_DebugCircles > 0.001)
                {
                    float bLine = smoothstep(0.012, 0.004, abs(length(q) - R)) * _DebugCircles;
                    float reachLine = smoothstep(0.012, 0.004, abs(rReach - _MaxReach)) * _DebugCircles;
                    float cutLine = smoothstep(0.010, 0.003, abs(p.y - c * R * k)) * _DebugCircles;
                    float eDot = smoothstep(0.035, 0.015, length(p - float2(0.0, -m * R * k))) * _DebugCircles;
                    if (bLine > a) { col = half3(0.8, 0.9, 1.0); a = bLine; }
                    if (reachLine > a) { col = half3(0.4, 1.0, 0.45); a = reachLine; }
                    if (cutLine > a) { col = half3(1.0, 0.55, 0.15); a = cutLine; }
                    if (eDot > a) { col = half3(1.0, 0.2, 0.2); a = eDot; }
                }

                if (a < 0.01) discard;
                return half4(col * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
