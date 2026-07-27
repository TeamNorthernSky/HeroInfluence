// 루미나 「플레어 봄」 차징 오브 — 티어드롭 셸 표면의 에바풍 오라 혀.
// FlareOrbShell이 생성한 통통 티어드롭 메시 표면에, 굵고 둥근 혀 획들이
// 위로 일렁이며 타오르는 무늬를 절차적으로 그린다.
// - 노이즈는 오브젝트 공간 방향 기반 → 둘레 이음매 없음
// - _EdgeSoftRatio 0 = 하드컷(카툰), 1 = 소프트 발광 에지
// - 혀가 없는 영역은 완전 투명(림 없음)
Shader "Testbed/FlareBomb/FlareAuraTongue"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorTongue    ("Tongue Color",    Color) = (1.05, 0.62, 0.16, 1)
        [HDR] _ColorHighlight ("Highlight Color", Color) = (1.45, 1.25, 0.80, 1)
        _Emission ("Emission", Range(0,6)) = 1.2

        [Header(Pattern)]
        _NoiseScale ("Pattern Scale (density)", Range(0.5,8)) = 3.2
        _VStretch ("Vertical Stretch (low=longer strokes)", Range(0.1,1)) = 0.45
        _Detail ("Detail Octave Weight (chunky<->busy)", Range(0,1)) = 0.45
        _RidgeMix ("Sharp Mix (0=blob, 1=sharp peak stroke)", Range(0,1)) = 0.8
        _TaperSharp ("Tip Taper Sharpness", Range(1,8)) = 2.6
        _BreakScale ("Segment Break Scale", Range(0.3,6)) = 1.6
        _BreakAmount ("Segment Break Amount (0=connected)", Range(0,1)) = 0.8
        _Threshold ("Coverage Threshold (high=sparse)", Range(0,1)) = 0.32
        _EdgeSoftRatio ("Edge Soft Ratio (0=hard cut)", Range(0,1)) = 0.25
        _HighlightShift ("Inner Highlight Shift", Range(0,0.4)) = 0.12

        [Header(Motion)]
        _FlowSpeed ("Rise Speed", Range(0,6)) = 1.2
        _Waver ("Licking Waver Amount", Range(0,0.6)) = 0.16
        _WaverSpeed ("Waver Speed", Range(0,6)) = 1.4
        _Shear ("Spiral Shear (0=pure rise)", Range(-2,2)) = 0
        _SCurveAmount ("S-Curve Amount (rad)", Range(0,1.2)) = 0.35
        _SCurveFreq ("S-Curve Bends (over height)", Range(0.2,4)) = 1.2
        _SCurveFollow ("S-Curve Flow Follow (1=ride the flow)", Range(0,2)) = 1.0
        _SCurveUpperRatio ("S-Curve Upper Attenuation", Range(0,1)) = 0.4

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2

        [Header(Tip Break)]
        _TipErodeStart ("Tip Erode Start (v)", Range(0,1)) = 0.68
        _TipErodeStrength ("Tip Erode Strength", Range(0,1.5)) = 0.55

        [Header(Specks)]
        _SpeckAmount ("Speck Amount (edge droplets)", Range(0,1)) = 0.5
        _SpeckScale ("Speck Scale", Range(2,24)) = 10.0

        [Header(Global)]
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareAuraTongue"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha   // 밝은 코어 위에 진한 혀가 얹히도록
            ZWrite Off
            Cull [_Cull]   // Back=전면 셸 / Front=후면 화염 레이어(뒷반구만)

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float hash31(float3 p){ p=frac(p*0.3183099+0.1); p*=17.0; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float vnoise(float3 x){
                float3 i=floor(x),f=frac(x); f=f*f*(3.0-2.0*f);
                float n000=hash31(i),n100=hash31(i+float3(1,0,0)),n010=hash31(i+float3(0,1,0)),n110=hash31(i+float3(1,1,0));
                float n001=hash31(i+float3(0,0,1)),n101=hash31(i+float3(1,0,1)),n011=hash31(i+float3(0,1,1)),n111=hash31(i+float3(1,1,1));
                float nx00=lerp(n000,n100,f.x),nx10=lerp(n010,n110,f.x),nx01=lerp(n001,n101,f.x),nx11=lerp(n011,n111,f.x);
                return lerp(lerp(nx00,nx10,f.y),lerp(nx01,nx11,f.y),f.z);
            }
            float3x3 rotY(float a){ float c=cos(a),s=sin(a); return float3x3(c,0,s,0,1,0,-s,0,c); }

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 positionOS:TEXCOORD0; float2 uv:TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorTongue,_ColorHighlight;
                half _Emission;
                half _NoiseScale,_VStretch,_Detail,_RidgeMix,_TaperSharp,_BreakScale,_BreakAmount,_Threshold,_EdgeSoftRatio,_HighlightShift;
                half _FlowSpeed,_Waver,_WaverSpeed,_Shear;
                half _SCurveAmount,_SCurveFreq,_SCurveFollow,_SCurveUpperRatio;
                half _TipErodeStart,_TipErodeStrength;
                half _SpeckAmount,_SpeckScale;
                half _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.uv = IN.uv;   // v = 셸 정규화 높이(0 하단..1 팁), FlareOrbShell이 굽는다
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target{
                float t = _Time.y;
                float v = saturate(IN.uv.y);
                float3 dir = normalize(IN.positionOS);

                // 나선 시어(기본 0 = 순수 상승) + S자 곡선 진행:
                // 위상 이동은 전체 흐름(FlowSpeed)에 결합(_SCurveFollow=1이면 흐름과 동행).
                // 진폭은 하단 100% → 중간(0.4~0.6) 지점부터 _SCurveUpperRatio로 감쇠.
                float heightAtten = lerp(1.0, _SCurveUpperRatio, smoothstep(0.4, 0.6, v));
                float sAng = sin(v * _SCurveFreq * 6.2831853 - t * _FlowSpeed * _SCurveFollow)
                             * _SCurveAmount * heightAtten;
                float3 d = mul(rotY(v * _Shear + sAng), dir);

                // 획 도메인: 방향 기반(이음매 없음) + 세로 스트레치 + 상승 스크롤
                float3 q = d * _NoiseScale;
                q.y *= _VStretch;
                q.y -= t * _FlowSpeed;

                // 청키한 2옥타브 노이즈 (저주파 형상 + 소량 디테일)
                float n = vnoise(q);
                n += (vnoise(q * 2.13 + 5.7) - 0.5) * _Detail;
                n = saturate(n);

                // 봉우리(peak) 강조: 노이즈 봉우리 능선을 따라 획이 형성된다.
                // 등고선(항상 닫힌 곡선)이 아닌 언덕 꼭대기 기반이라 획끼리
                // 고리를 이루거나 갈라짐·합쳐짐이 동시에 생기지 않는다.
                // _TaperSharp가 클수록 단면이 좁아져 끝이 얇고 샤프하게 마감.
                float peak = pow(saturate(n), _TaperSharp);
                float shaped = lerp(n, peak, _RidgeMix);

                // 세그먼트 브레이크: 제2 노이즈로 등고선망을 끊어 개별 획으로 분리.
                // 릿지 단면은 중앙선이 가장 높아, 마스크가 줄어드는 지점에서 획 끝이
                // 자연히 가늘어지며 뾰족하게 마감된다.
                float segMask = vnoise(d * _BreakScale + float3(3.1, -t * _FlowSpeed * 0.4, 7.7));
                shaped *= lerp(1.0, smoothstep(0.30, 0.62, segMask), _BreakAmount);

                // 일렁임: 필드 자체를 느리게 흔들어 licking
                shaped += (vnoise(d * 1.7 + float3(0, t * _WaverSpeed, 0)) - 0.5) * _Waver;

                // 팁 소멸: 위로 갈수록 임계값이 올라가 획 단면이 좁아진다(핀치).
                // 획 단면은 능선 중앙이 가장 높으므로, 폭이 점점 가늘어지다 바늘끝으로 소멸.
                float tipBand = smoothstep(_TipErodeStart, 1.0, v);
                float field = shaped - tipBand * _TipErodeStrength;

                // 실루엣 컷: 소프트 비율 파라미터 (0 = AA만 남는 하드컷).
                // 팁 밴드에서는 하드 에지로 수렴 → 알파 페이드 없이 폭만 줄어든다.
                float soft = lerp(0.006, 0.14, _EdgeSoftRatio);
                soft = lerp(soft, 0.006, tipBand);
                float tongue = smoothstep(_Threshold - soft, _Threshold + soft, field);

                // 획 내부 하이라이트(2톤)
                float highlight = smoothstep(_Threshold + _HighlightShift - soft,
                                             _Threshold + _HighlightShift + soft, field);

                // 방울 파편: 획 경계 바로 바깥 띠에서 고주파 점
                float band = smoothstep(_Threshold - 0.16, _Threshold - 0.03, field) * (1.0 - tongue);
                float dots = smoothstep(0.78, 0.86, vnoise(d * _SpeckScale + float3(0, -t * _FlowSpeed * 0.7, 0)));
                float speck = band * dots * _SpeckAmount;

                half3 col = lerp(_ColorTongue.rgb, _ColorHighlight.rgb, highlight);
                half a = saturate(max(tongue, speck)) * _Opacity;
                if (a < 0.01) discard;
                return half4(col * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
