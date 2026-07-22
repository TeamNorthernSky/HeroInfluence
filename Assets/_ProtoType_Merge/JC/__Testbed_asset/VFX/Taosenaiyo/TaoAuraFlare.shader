// JC Taosenaiyo VFX — 발산 오라(초사이언풍): HealAuraGlow(실린더 셸) 포크.
// 차이점: ①버텍스 플레어(_Flare — 위로 갈수록 xz 확장 = 좌우 발산각 콘)
//        ②펜선 스트릭 — 부드러운 노이즈 변조 대신 얇고 진한 세로 라인(원주 컬럼 해시+상승 대시 스크롤).
// 세로 그라데이션/상단 경계(TopMin/Max/Soft)/|NdotV| 실루엣 소프트는 힐 것 계승. 가산·양면.
Shader "JC/VFX/TaoAuraFlare"
{
    Properties
    {
        [HDR]_Color ("Base Glow Color", Color) = (1.0, 0.82, 0.3, 1)
        _Intensity ("Base Intensity", Range(0,8)) = 1.2
        [HDR]_LineColor ("Pen Line Color", Color) = (1.0, 0.7, 0.15, 1)
        _LineIntensity ("Pen Line Intensity", Range(0,10)) = 3.2
        _Opacity ("Opacity", Range(0,1)) = 1.0
        [Header(Flare)]
        _Flare ("Base Flare (bottom spread)", Range(0,2)) = 0.7
        _FlareCurve ("Flare Curve (base concentration)", Range(0.5,6)) = 2.2
        _FlareUp ("Flare Direction Up (0=bottom 1=top)", Range(0,1)) = 0
        _BaseBoost ("Base Brightness Boost", Range(0,3)) = 0.6
        [Header(Shape Vertical)]
        _YExtent ("Mesh Y Half-Extent", Float) = 1.0
        _BottomFade ("Bottom Fade", Range(0,0.5)) = 0.06
        _VerticalBias ("Vertical Bias", Range(0.1,5)) = 1.2
        [Header(Top Edge)]
        _TopMin ("Top Min Height", Range(0,1)) = 0.72
        _TopMax ("Top Max Height", Range(0,1)) = 0.95
        _TopSoft ("Top Softness", Range(0,0.5)) = 0.15
        _TopNoiseScale ("Top Noise Scale", Range(0.5,10)) = 3.0
        _TopNoiseSpeed ("Top Noise Speed", Range(0,4)) = 0.8
        [Header(Silhouette Soft)]
        _FacePower ("Face Power", Range(0.3,8)) = 1.4
        [Header(Pen Lines)]
        _LineCount ("Line Column Count", Range(4,80)) = 26
        _LineWidth ("Line Width in Cell", Range(0.02,0.6)) = 0.16
        _LineSharp ("Line Edge Sharpness", Range(0.005,0.3)) = 0.04
        _DashFreq ("Dash Frequency", Range(0.5,8)) = 2.2
        _DashDuty ("Dash Duty (line fill)", Range(0.1,1)) = 0.65
        _ScrollSpeed ("Dash Scroll Up Speed", Range(0,6)) = 1.6
        [Header(Base Spray)]
        [HDR]_SprayColor ("Spray Color", Color) = (1.0, 0.62, 0.12, 1)
        _SprayIntensity ("Spray Intensity", Range(0,10)) = 3.5
        _SprayHeight ("Spray Zone Height", Range(0.05,1)) = 0.35
        _SprayCount ("Spray Column Count", Range(4,120)) = 42
        _SprayWidth ("Spray Line Width", Range(0.02,0.6)) = 0.22
        _SpraySharp ("Spray Edge Sharpness", Range(0.005,0.3)) = 0.05
        _SprayDashFreq ("Spray Dash Frequency", Range(0.5,12)) = 3.2
        _SprayDashDuty ("Spray Dash Duty", Range(0.1,1)) = 0.55
        _SpraySpeed ("Spray Flow Speed", Range(-6,6)) = 2.2
        _SprayTaper ("Spray Tip Taper", Range(0,1)) = 0.85
        _SprayRandom ("Spray Randomness", Range(0,1)) = 0.7
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
                float4 _Color; float _Intensity;
                float4 _LineColor; float _LineIntensity;
                float _Opacity; float _Flare; float _FlareCurve; float _FlareUp; float _BaseBoost;
                float _YExtent; float _BottomFade; float _VerticalBias;
                float _TopMin; float _TopMax; float _TopSoft; float _TopNoiseScale; float _TopNoiseSpeed;
                float _FacePower;
                float _LineCount; float _LineWidth; float _LineSharp;
                float _DashFreq; float _DashDuty; float _ScrollSpeed;
                float4 _SprayColor; float _SprayIntensity; float _SprayHeight;
                float _SprayCount; float _SprayWidth; float _SpraySharp;
                float _SprayDashFreq; float _SprayDashDuty; float _SpraySpeed; float _SprayTaper; float _SprayRandom;
                float _FadeMul;
            CBUFFER_END

            float hash13(float3 p){ p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float hash1(float n) { return frac(sin(n) * 43758.5453); }
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
                // 버텍스 플레어: 기초(바닥)에서 방사로 벌어지고 위로 갈수록 수직 수렴(벨 프로파일).
                //   _FlareCurve↑ = 벌어짐이 바닥에 집중(위쪽은 빠르게 수직). posOS는 원본을 넘겨 h/각도 계산 일관 유지.
                float h01 = saturate(IN.positionOS.y / max(_YExtent, 1e-4) * 0.5 + 0.5);
                float3 flared = IN.positionOS.xyz;
                float prof = lerp(1.0 - h01, h01, _FlareUp);   // 0=바닥 벌어짐(벨) / 1=위로 벌어짐(스커트 콘)
                flared.xz *= 1.0 + _Flare * pow(prof, _FlareCurve);
                VertexPositionInputs p = GetVertexPositionInputs(flared);
                OUT.positionHCS = p.positionCS;
                OUT.posOS = IN.positionOS.xyz;
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float h = saturate(IN.posOS.y / max(_YExtent, 1e-4) * 0.5 + 0.5);
                float ang = atan2(IN.posOS.z, IN.posOS.x);
                float2 cs = float2(cos(ang), sin(ang));
                float angNorm = ang / 6.2831853 + 0.5;   // 0..1

                // 상단 경계 + 몸통 그라데이션 (힐 계승)
                float topN = noise3(float3(cs * _TopNoiseScale, _Time.y * _TopNoiseSpeed));
                float Tcol = lerp(_TopMin, _TopMax, topN);
                float body = pow(saturate(1.0 - h), _VerticalBias);
                float edge = 1.0 - smoothstep(Tcol - max(_TopSoft, 1e-4), Tcol, h);
                float bottom = smoothstep(0.0, max(_BottomFade, 1e-4), h);
                float vertical = body * edge * bottom;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float face = pow(saturate(abs(dot(N, V))), _FacePower);

                // 펜선: 원주 컬럼별 얇은 세로 라인(해시 폭/위상) × 상승 대시
                float nCol = round(_LineCount);          // 정수 컬럼 = 원주 seamless
                float colf = angNorm * nCol;
                float ci = floor(colf);
                float fx = frac(colf);
                float h1 = hash1(ci);
                float h2 = hash1(ci + 57.3);
                float w = _LineWidth * lerp(0.6, 1.4, h2) * 0.5;
                float xc = 0.5 + (h1 - 0.5) * (1.0 - w * 2.0);
                float lineMask = 1.0 - smoothstep(w, w + max(_LineSharp, 1e-4), abs(fx - xc));
                float dash = smoothstep(1.0 - _DashDuty, 1.0, frac(h * _DashFreq + h2 * 7.0 - _Time.y * _ScrollSpeed));
                float lines = lineMask * dash * lerp(0.6, 1.3, h1);

                // 기초 방사 강조: 바닥 근처가 더 이글거리게
                float baseBoost = 1.0 + _BaseBoost * pow(saturate(1.0 - h), 3.0);

                // 기초 스프레이: 바닥 존에 집중된 별도 스트릭(아래로 흐름). 벨 프로파일 표면 위라
                // 바깥-아래로 뿜는 방향으로 읽힘 — 초사이언 아우라의 바닥 갈퀴 실루엣.
                float sprayN = round(_SprayCount);
                float scolf = angNorm * sprayN;
                float sci = floor(scolf);
                float sfx = frac(scolf);
                float sh1 = hash1(sci + 811.7);
                float sh2 = hash1(sci + 173.9);
                // 컬럼별 무작위화: 속도/갈퀴 길이(duty)를 컬럼마다 흩뜨려 일제 발사(열 맞춤) 느낌 제거
                float sh3 = hash1(sci + 331.7);
                float sh4 = hash1(sci + 77.7);
                float spd = _SpraySpeed * lerp(1.0, lerp(0.55, 1.75, sh3), _SprayRandom);
                float duty = _SprayDashDuty * lerp(1.0, lerp(0.5, 1.15, sh4), _SprayRandom);

                // 대시 위상: 한 갈퀴 내 0(밑동)→1(끝). 끝으로 갈수록 폭이 좁아져 뾰족한 삼각형.
                //   부호: +speed = 위·바깥으로 흐름(직관 방향)
                float sphase = frac(h * _SprayDashFreq + sh2 * 9.0 - _Time.y * spd);
                float qn = saturate((sphase - (1.0 - duty)) / max(duty, 1e-4));
                float sprayDash = smoothstep(1.0 - duty, 1.0 - duty + 0.05, sphase);
                float sw = _SprayWidth * lerp(0.5, 1.5, sh2) * 0.5 * (1.0 - _SprayTaper * qn);
                float sxc = 0.5 + (sh1 - 0.5) * (1.0 - _SprayWidth);
                float sprayLine = 1.0 - smoothstep(sw, sw + max(_SpraySharp, 1e-4), abs(sfx - sxc));
                float sprayZone = (1.0 - smoothstep(0.0, _SprayHeight, h)) * bottom;
                float spray = sprayLine * sprayDash * sprayZone * lerp(0.6, 1.4, sh1);

                float3 col = ((_Color.rgb * _Intensity + _LineColor.rgb * _LineIntensity * lines)
                              * vertical * baseBoost
                            + _SprayColor.rgb * _SprayIntensity * spray)
                           * _Opacity * face * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
