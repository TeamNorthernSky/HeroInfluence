// 루미나 「플레어 봄」 차징 오브 F2 — 2D 스프라이트 오라 혀.
// 시선축 정렬 빌보드 쿼드 위 2D 극좌표(각도 θ, 반경 r)에 획을 그린다.
// - 검은 원(_BaseRadius): 획 생성 기준 반경 / 녹색 원(_MaxReach): 강제 종료 경계
//   (닿으면 폭이 강제로 줄며 소멸. 생명주기 로직과는 독립).
// - 생명주기·지터·동공 프로파일·수명 페이드는 F1(FlareAuraLane)에서 이식.
// - _UpBias: 획 진행 방향을 방사형(0)↔화면 상향(1)으로 기울임. S자 곡선 유지.
// - 후면 레이어 없음. 큐 3002(코어 위, 최상단)로 코어를 가린다.
Shader "Testbed/FlareBomb/FlareAuraSprite"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorTongue    ("Tongue Color",    Color) = (1.05, 0.62, 0.16, 1)
        [HDR] _ColorHighlight ("Highlight Color", Color) = (1.45, 1.25, 0.80, 1)
        _Emission ("Emission", Range(0,6)) = 1.2

        [Header(Layout UV)]
        _BaseRadius ("Base Circle Radius (black)", Range(0.05,0.9)) = 0.42
        _MaxReach ("Max Reach Radius (green, force kill)", Range(0.1,1)) = 0.75
        _ReachSoft ("Reach Kill Softness", Range(0.01,0.3)) = 0.08

        [Header(Lanes)]
        _LaneCount ("Sector Count (strokes around)", Range(3,32)) = 12
        _LaneWidth ("Stroke Half Width (0..0.5 cell)", Range(0.02,0.5)) = 0.16
        _LaneWidthJitter ("Per-stroke Width Jitter", Range(0,1)) = 0.35
        _LanePosJitter ("Per-stroke Position Jitter (cell)", Range(0,0.8)) = 0.35
        _LaneTiltJitter ("Per-stroke Tilt Jitter", Range(0,2)) = 0.5
        _WidthNoiseScale ("Width Waviness Scale", Range(0.5,12)) = 3.5
        _WidthNoiseAmount ("Width Waviness Amount", Range(0,1)) = 0.4

        [Header(Birth Band Radial)]
        _RStart ("Birth Radius Start", Range(0,1)) = 0.34
        _REnd ("Birth Radius End", Range(0,1)) = 0.6
        _RJitter ("Birth Radius Spread (0=fixed, 0.5=full)", Range(0,0.5)) = 0.25
        _BirthBias ("Inner Birth Weight (1=uniform)", Range(1,4)) = 1.8
        _VLengthJitter ("Per-cycle Length Jitter", Range(0,1)) = 0.35
        _MaxLen ("Max Stroke Length (uv r)", Range(0.05,0.8)) = 0.22
        _OuterShrink ("Outer Birth Shrink (base circle boundary)", Range(0,1)) = 0.6

        [Header(Lifecycle)]
        _TravelDist ("Stroke Travel Distance (uv r)", Range(0.02,0.5)) = 0.15
        _TravelSpeed ("Stroke Rise Speed (vs Flow)", Range(0,1)) = 0.3
        _LifeFadePeak ("Life Fade Peak (0=start, 0.5=mid, 1=none)", Range(0,1)) = 0.4

        [Header(Stroke Shape)]
        _TaperSharp ("Pupil Profile Sharpness", Range(0.5,6)) = 1.4
        _HighlightRatio ("Inner Highlight Width Ratio", Range(0,1)) = 0.45
        _EdgeSoftRatio ("Edge Soft Ratio (0=hard cut)", Range(0,1)) = 0.25

        [Header(Motion)]
        _FlowSpeed ("Flow Speed", Range(0,6)) = 1.2
        _SpinSpeed ("Pattern Spin (deg per sec)", Range(-180,180)) = 25
        _UpBias ("Direction Bias (0=radial, 1=screen up)", Range(0,1)) = 0
        _SCurveAmount ("S-Curve Amount (rad, cell-clamped)", Range(0,1.2)) = 0.45
        _SCurveFreq ("S-Curve Bends (len cap = 1 period)", Range(0.2,6)) = 2.0

        [Header(Debug)]
        _DebugCircles ("Debug Circles (base and reach)", Range(0,1)) = 0

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
                half _BaseRadius,_MaxReach,_ReachSoft;
                half _LaneCount,_LaneWidth,_LaneWidthJitter,_LanePosJitter,_LaneTiltJitter,_WidthNoiseScale,_WidthNoiseAmount;
                half _RStart,_REnd,_RJitter,_BirthBias,_VLengthJitter,_MaxLen,_OuterShrink;
                half _TravelDist,_TravelSpeed,_LifeFadePeak;
                half _TaperSharp,_HighlightRatio,_EdgeSoftRatio;
                half _FlowSpeed,_SpinSpeed,_UpBias;
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

            // 한 후보 섹터(자기/이웃)의 획을 평가. angLoc = 해당 섹터 로컬 가로 좌표(중심 0).
            void EvalStroke2D(float lid, float angLoc, float r, float t, float sectorAngle,
                              out float stroke, out float highlight)
            {
                stroke = 0.0; highlight = 0.0;
                float laneW = lid - _LaneCount * floor(lid / _LaneCount);

                // 생명주기 (F1 이식): 사이클마다 전 랜덤 재추첨
                float vSpeed = _FlowSpeed * _TravelSpeed;
                float cycleT = t * vSpeed / max(_TravelDist, 0.02) + shash(laneW);
                float cid = fmod(floor(cycleT), 64.0);
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

                // 생성 반경: 내측 확장(_TravelDist) + 내측 가중(_BirthBias) + 산포(_RJitter)
                float birthSpread = saturate(_RJitter * 2.0);
                float rBirthW = pow(rBirth, _BirthBias);
                float birthLo = _RStart - _TravelDist;
                float rs0 = birthLo + rBirthW * max(_REnd - birthLo, 0.0) * birthSpread;
                float rs = rs0 + phase * _TravelDist;

                // 길이: _MaxLen 아래로 분포. 기준원 밖 생성은 길이·두께 축소.
                float lenJ = 1.0 - rLen * _VLengthJitter;
                float outerT = saturate((rs0 - _BaseRadius) / max(_MaxReach - _BaseRadius, 0.01));
                float shrink = 1.0 - outerT * _OuterShrink;
                float minLen = min(0.05, _MaxLen);
                float len = max(_MaxLen * lenJ * shrink, minLen);
                len = min(len, 1.0 / max(_SCurveFreq, 0.2));   // S 1주기를 다 그리면 소멸
                float re = rs + len;
                float mid = 0.5 * (rs + re);

                // 고양이 동공(렌즈) 프로파일 — 전 길이 연속 폭 함수(꺾임 없음)
                float t01 = saturate((r - rs) / max(re - rs, 0.0001));
                float pupil = 4.0 * t01 * (1.0 - t01);
                float band = pow(pupil, _TaperSharp);

                // 지터 + 상향 바이어스 틸트:
                // 섹터의 방사 방향이 화면 위(각도 0.5턴 기준)와 이루는 차이만큼 획을 기울여
                // 진행 방향을 방사↔상향으로 보간. 극단(측면·하단)은 셀 한계로 클램프.
                float centerOff = (rPos - 0.5) * _LanePosJitter;
                float upDelta = sectorAngle - 0.5;                       // -0.5..0.5 (턴)
                upDelta -= floor(upDelta + 0.5);                          // 최단 랩
                float upTilt = clamp(-upDelta * _LaneCount * _UpBias, -1.4, 1.4);
                float tilt = (rTilt - 0.5) * _LaneTiltJitter + upTilt;

                // 획별 S커브(유지): 위상·방향 랜덤, 진폭은 이웃 1셀(0.5셀) 클램프
                float aCell = min(_SCurveAmount / TWO_PI * _LaneCount, 0.5);
                float sMirror = (rMirror < 0.5) ? -1.0 : 1.0;
                float sOff = sMirror * aCell * sin(r * _SCurveFreq * TWO_PI + rSPhase * TWO_PI);

                float x = angLoc - centerOff - tilt * (r - mid) - sOff;

                // 폭 변조 + 녹색원 강제 종료(생명주기와 독립: 폭이 강제로 줄며 소멸)
                float wn = vnoise2(float2(laneW * 3.3, r * _WidthNoiseScale - t * _FlowSpeed + ph));
                float widthMod = lerp(1.0, wn, _WidthNoiseAmount);
                float reachKill = smoothstep(_MaxReach, _MaxReach - _ReachSoft, r);
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
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                // 각도(0..1, 화면 위 = 0.5) + 2D 패턴 스핀
                float ang = atan2(p.x, -p.y) / TWO_PI + 0.5;
                ang = frac(ang + t * _SpinSpeed / 360.0);

                // 섹터 분해 + 이웃 3셀 평가(경계 잘림 방지·겹침 허용)
                float lanes = ang * _LaneCount;
                float laneId = floor(lanes);

                float bestStroke = 0.0, bestHi = 0.0;
                [unroll]
                for (int o = -1; o <= 1; o++)
                {
                    float lid = laneId + o;
                    float angLoc = lanes - lid - 0.5;
                    float sectorAngle = frac((lid + 0.5) / _LaneCount + t * _SpinSpeed / 360.0);
                    float s, h;
                    EvalStroke2D(lid, angLoc, r, t, sectorAngle, s, h);
                    if (s > bestStroke) { bestStroke = s; bestHi = h; }
                }

                half3 col = lerp(_ColorTongue.rgb, _ColorHighlight.rgb, bestHi);
                half a = bestStroke * _Opacity;

                // 디버그 원: 기준원(청백) + 도달한계(녹색)
                if (_DebugCircles > 0.001)
                {
                    float baseLine = smoothstep(0.012, 0.004, abs(r - _BaseRadius)) * _DebugCircles;
                    float reachLine = smoothstep(0.012, 0.004, abs(r - _MaxReach)) * _DebugCircles;
                    if (baseLine > a) { col = half3(0.8, 0.9, 1.0); a = baseLine; }
                    if (reachLine > a) { col = half3(0.4, 1.0, 0.45); a = reachLine; }
                }

                if (a < 0.01) discard;
                return half4(col * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
