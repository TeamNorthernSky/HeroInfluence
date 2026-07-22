// JC 힐 스킬 VFX — 바닥 광채(E-3): 발밑에서 위로 솟는 광채 기둥(실린더 셸 표면).
// 가산·양면(Cull Off). 절차적, 메시 비의존:
//   - 세로 그라데이션 = 오브젝트 Y(하부 밝고 위로 페이드)
//   - 좌우 실루엣 소프트 = |NdotV| (grazing에서 0 → 딱딱한 세로 경계 없음)
//   - 세로 광선 = 3D 노이즈(원주 seamless). 규칙적 sin 밴딩 대신 불규칙 빛줄기.
//   - 상단 경계 불규칙(뾰족) = 원주별 높이 노이즈. 일렁임 = 저주파 밝기 노이즈.
// 실린더/구체(늘리면 타원 돔) 어느 메시든 동작. 바깥 번짐은 씬 URP Bloom.
Shader "JC/VFX/HealAuraGlow"
{
    Properties
    {
        [HDR]_Color ("Glow Color", Color) = (1.0, 0.85, 0.28, 1)
        _Intensity ("Intensity", Range(0,8)) = 1.8
        _Opacity ("Opacity", Range(0,1)) = 1.0
        [Header(Shape Vertical)]
        _YExtent ("Mesh Y Half-Extent", Float) = 1.0
        _BottomFade ("Bottom Fade", Range(0,0.5)) = 0.06
        _VerticalBias ("Vertical Bias (top falloff)", Range(0.1,5)) = 1.4
        [Header(Top Edge)]
        _TopMin ("Top Min Height", Range(0,1)) = 0.70
        _TopMax ("Top Max Height", Range(0,1)) = 0.92
        _TopSoft ("Top Softness", Range(0,0.5)) = 0.12
        _TopNoiseScale ("Top Noise Scale", Range(0.5,10)) = 3.0
        _TopNoiseSpeed ("Top Noise Speed", Range(0,4)) = 0.6
        [Header(Silhouette Soft)]
        _FacePower ("Face Power (edge soft)", Range(0.3,8)) = 1.6
        [Header(Vertical Rays)]
        _StreakTiling ("Ray Density", Range(0,80)) = 18
        _StreakStrength ("Ray Strength", Range(0,1)) = 0.12
        _StreakScroll ("Ray Scroll Speed", Range(-4,4)) = 0.7
        [Header(Wobble)]
        _WobbleAmount ("Wobble Amount (일렁임)", Range(0,1)) = 0.15
        _WobbleSpeed ("Wobble Speed", Range(0,6)) = 1.2
        [Header(Fade)]
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 posOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity; float _Opacity;
                float _YExtent; float _BottomFade; float _VerticalBias;
                float _TopMin; float _TopMax; float _TopSoft; float _TopNoiseScale; float _TopNoiseSpeed;
                float _FacePower;
                float _StreakTiling; float _StreakStrength; float _StreakScroll;
                float _WobbleAmount; float _WobbleSpeed;
                float _FadeMul;
            CBUFFER_END

            float hash13(float3 p){ p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float noise3(float3 x){
                float3 i = floor(x); float3 f = frac(x); f = f*f*(3.0-2.0*f);
                float n000=hash13(i+float3(0,0,0)), n100=hash13(i+float3(1,0,0));
                float n010=hash13(i+float3(0,1,0)), n110=hash13(i+float3(1,1,0));
                float n001=hash13(i+float3(0,0,1)), n101=hash13(i+float3(1,0,1));
                float n011=hash13(i+float3(0,1,1)), n111=hash13(i+float3(1,1,1));
                return lerp(lerp(lerp(n000,n100,f.x),lerp(n010,n110,f.x),f.y),
                            lerp(lerp(n001,n101,f.x),lerp(n011,n111,f.x),f.y), f.z);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.posOS = IN.positionOS.xyz;
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 세로 정규화: 오브젝트 Y [-YExtent,+YExtent] → h [0(bottom),1(top)]
                float h = saturate(IN.posOS.y / max(_YExtent, 1e-4) * 0.5 + 0.5);
                float ang = atan2(IN.posOS.z, IN.posOS.x);
                float2 cs = float2(cos(ang), sin(ang));   // 원주 seamless 좌표

                // 상단 경계: 원주별 상단 높이 = [TopMin,TopMax] 사이 노이즈. 둘 다 h<1이라 지오메트리 클립 없음.
                //   TopMin==TopMax면 경계가 한 높이로 고정. 몸통 그라데이션과 컷오프를 분리:
                //   body(하부밝음, verticalBias 곡률) × edge(상단 컷오프, TopSoft=0이면 날카로운 평면).
                float topN = noise3(float3(cs * _TopNoiseScale, _Time.y * _TopNoiseSpeed));
                float Tcol = lerp(_TopMin, _TopMax, topN);
                float body = pow(saturate(1.0 - h), _VerticalBias);              // 몸통: 하부 밝고 위로 은은히
                float edge = 1.0 - smoothstep(Tcol - max(_TopSoft, 1e-4), Tcol, h); // 상단 컷오프: soft 작을수록 평면 경계 또렷
                float bottom = smoothstep(0.0, max(_BottomFade, 1e-4), h);
                float vertical = body * edge * bottom;

                // 좌우 실루엣 소프트
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float face = pow(saturate(abs(dot(N, V))), _FacePower);

                // 세로 광선: 불규칙 노이즈(원주 seamless), 위로 스크롤
                float rays = noise3(float3(cs * _StreakTiling * 0.15, h * 4.0 - _Time.y * _StreakScroll));
                float streaks = lerp(1.0 - _StreakStrength, 1.0 + _StreakStrength, rays);

                // 일렁임: 저주파 밝기 노이즈
                float wob = noise3(float3(cs * 1.5, _Time.y * _WobbleSpeed));
                float wobble = lerp(1.0 - _WobbleAmount, 1.0 + _WobbleAmount, wob);

                float3 col = _Color.rgb * _Intensity * _Opacity * vertical * face * streaks * wobble * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
