// JC Taosenaiyo VFX — 마법진 장판: 절차적 기하 문양(동심원 링 + 육망성 + 룬 눈금 밴드) + 중심 광채.
// 내륜(육망성·눈금)과 외륜(대시 링)이 역방향 회전, 전체 밝기 펄스. 텍스처 무의존, HDR+블룸 전제.
// 배치/스케일/페이드는 스크립트(TaoMagicCircle)가 구동. 가산 블렌드.
Shader "JC/VFX/TaoMagicCircle"
{
    Properties
    {
        [HDR]_Color ("Color", Color) = (1.0, 0.85, 0.35, 1)
        _Intensity ("Intensity", Range(0,8)) = 2.2
        [Header(Rings)]
        _Ring1 ("Ring1 Radius", Range(0,1)) = 0.95
        _Ring2 ("Ring2 Radius", Range(0,1)) = 0.78
        _Ring3 ("Ring3 Radius", Range(0,1)) = 0.38
        _LineWidth ("Line Width", Range(0.002,0.08)) = 0.012
        _LineSoft ("Line Softness", Range(0.001,0.05)) = 0.008
        [Header(Hexagram)]
        _HexRadius ("Hexagram Radius", Range(0,1)) = 0.72
        _HexWidth ("Hexagram Line Width", Range(0.002,0.08)) = 0.011
        [Header(Square Star)]
        _SquareRadius ("Square Star Radius", Range(0,1)) = 0.6
        _SquareWidth ("Square Line Width", Range(0.002,0.08)) = 0.009
        [Header(Satellites)]
        _SatOrbit ("Satellite Orbit Radius", Range(0,1)) = 0.78
        _SatCount ("Satellite Count", Range(2,16)) = 6
        _SatRadius ("Satellite Circle Radius", Range(0.01,0.2)) = 0.055
        [Header(Spokes)]
        _SpokeCount ("Spoke Count", Range(0,32)) = 8
        _SpokeInner ("Spoke Inner Radius", Range(0,1)) = 0.4
        _SpokeOuter ("Spoke Outer Radius", Range(0,1)) = 0.74
        [Header(Rune Ticks)]
        _TickRadius ("Tick Band Radius", Range(0,1)) = 0.86
        _TickWidth ("Tick Band Half Width", Range(0.005,0.12)) = 0.045
        _TickCount ("Tick Count", Range(4,128)) = 48
        _TickDuty ("Tick Duty", Range(0.05,0.95)) = 0.45
        [Header(Rotation Pulse)]
        _RotInner ("Inner Rot Speed DegSec", Range(-180,180)) = 18
        _RotOuter ("Outer Rot Speed DegSec", Range(-180,180)) = -12
        _PulseAmp ("Pulse Amp", Range(0,1)) = 0.18
        _PulseFreq ("Pulse Freq", Range(0,12)) = 2.4
        [Header(Center Glow)]
        _CenterGlow ("Center Glow", Range(0,3)) = 0.55
        _CenterFalloff ("Center Falloff", Range(0.5,8)) = 2.6
        _EdgeFade ("Outer Edge Fade", Range(0.005,0.3)) = 0.06
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
                float4 _Color; float _Intensity;
                float _Ring1; float _Ring2; float _Ring3; float _LineWidth; float _LineSoft;
                float _HexRadius; float _HexWidth;
                float _SquareRadius; float _SquareWidth;
                float _SatOrbit; float _SatCount; float _SatRadius;
                float _SpokeCount; float _SpokeInner; float _SpokeOuter;
                float _TickRadius; float _TickWidth; float _TickCount; float _TickDuty;
                float _RotInner; float _RotOuter; float _PulseAmp; float _PulseFreq;
                float _CenterGlow; float _CenterFalloff; float _EdgeFade;
                float _FadeMul;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float sdSeg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float t = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * t);
            }

            float LineMask(float d, float w, float s)
            {
                return 1.0 - smoothstep(w, w + max(s, 1e-4), d);
            }

            // 정n각형 아웃라인(반지름 R, 회전 rot)의 최소 선분 거리
            float NGonDist(float2 p, float R, float rot, int n)
            {
                float step = 6.2831853 / n;
                float d = 1e5;
                float2 prev = R * float2(cos(rot), sin(rot));
                for (int i = 1; i <= n; i++)
                {
                    float a = rot + step * i;
                    float2 cur = R * float2(cos(a), sin(a));
                    d = min(d, sdSeg(p, prev, cur));
                    prev = cur;
                }
                return d;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float rotI = radians(_RotInner) * _Time.y;
                float rotO = radians(_RotOuter) * _Time.y;

                float v = 0.0;

                // 동심원 링 3개 (0=off)
                if (_Ring1 > 0.01) v += LineMask(abs(r - _Ring1), _LineWidth, _LineSoft);
                if (_Ring2 > 0.01) v += LineMask(abs(r - _Ring2), _LineWidth, _LineSoft);
                if (_Ring3 > 0.01) v += LineMask(abs(r - _Ring3), _LineWidth, _LineSoft);

                // 육망성: 정삼각형 2개(60도 오프셋), 내륜 회전
                if (_HexRadius > 0.01)
                {
                    float d1 = NGonDist(p, _HexRadius, rotI + 1.5707963, 3);
                    float d2 = NGonDist(p, _HexRadius, rotI + 1.5707963 + 1.0471976, 3);
                    v += LineMask(min(d1, d2), _HexWidth, _LineSoft);
                }

                // 겹정사각(8망성): 정사각형 2개(45도 오프셋), 내륜 역위상 회전
                if (_SquareRadius > 0.01)
                {
                    float rotSq = -rotI * 0.7;   // 육망성과 미묘하게 다른 속도·역방향
                    float d1 = NGonDist(p, _SquareRadius, rotSq, 4);
                    float d2 = NGonDist(p, _SquareRadius, rotSq + 0.7853982, 4);
                    v += LineMask(min(d1, d2), _SquareWidth, _LineSoft);
                }

                // 위성 소원: 궤도 위 N개 작은 원 아웃라인, 외륜 회전
                if (_SatOrbit > 0.01)
                {
                    float rep = 6.2831853 / max(round(_SatCount), 1.0);
                    float aRel = ang - rotO;
                    float satAng = (floor(aRel / rep + 0.5)) * rep + rotO;
                    float2 c = _SatOrbit * float2(cos(satAng), sin(satAng));
                    float dSat = abs(length(p - c) - _SatRadius);
                    v += LineMask(dSat, _LineWidth, _LineSoft);
                }

                // 방사 스포크: 내→외 반경 구간의 방사 선분 N개, 내륜 회전
                if (_SpokeCount > 0.5)
                {
                    float rep = 6.2831853 / max(round(_SpokeCount), 1.0);
                    float aRel = ang - rotI;
                    float delta = abs(aRel - rep * floor(aRel / rep + 0.5));
                    float dLine = r * sin(delta);   // 스포크 레이까지의 수직 거리
                    float band = smoothstep(_SpokeInner - 0.02, _SpokeInner, r)
                               * (1.0 - smoothstep(_SpokeOuter, _SpokeOuter + 0.02, r));
                    v += LineMask(dLine, _LineWidth, _LineSoft) * band;
                }

                // 룬 눈금 밴드: 각도 대시 × 반경 밴드, 외륜 회전
                if (_TickRadius > 0.01)
                {
                    float band = 1.0 - smoothstep(_TickWidth * 0.7, _TickWidth, abs(r - _TickRadius));
                    float dash = step(1.0 - _TickDuty, frac((ang + rotO) * round(_TickCount) / 6.2831853));
                    v += band * dash;
                }

                // 중심 광채
                v += _CenterGlow * pow(saturate(1.0 - r), _CenterFalloff);

                // 외곽 페이드(쿼드 경계 은폐) + 펄스
                float edge = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, r);
                float pulse = 1.0 + _PulseAmp * sin(_Time.y * _PulseFreq);

                float3 col = _Color.rgb * _Intensity * v * edge * pulse * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
