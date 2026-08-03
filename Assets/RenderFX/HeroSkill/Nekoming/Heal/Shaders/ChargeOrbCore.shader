// JC 힐 스킬 VFX — 차징 구체 코어 (방사형 + 표면 스파이크 요동)
// 프래그: NdotV로 [밝은 코어]+[간격]+[빛나는 외곽선]. 버텍스: 노이즈 변위로 표면이 바깥으로 뾰족하게 요동.
// 바깥 백색 번짐은 씬 URP Bloom. 외부 에셋 의존 없음(절차적).
Shader "JC/VFX/ChargeOrbCore"
{
    Properties
    {
        [HDR]_CoreColor ("Core Color", Color) = (1.0, 0.86, 0.42, 1)
        [HDR]_RimColor  ("Rim (Outline) Color", Color) = (1.0, 0.70, 0.18, 1)
        _CoreIntensity ("Core Intensity", Range(0,8)) = 3.0
        _RimIntensity  ("Rim Intensity", Range(0,8)) = 4.0
        _CorePower ("Core Tightness", Range(0.5,10)) = 2.5
        _RimPower  ("Rim Thinness", Range(0.5,16)) = 5.0
        _GapStrength ("Gap Strength", Range(0,1)) = 0.5
        [Header(Surface Spikes)]
        _CoreSpikeAmount ("Core Spike Amount (정면)", Range(0,0.6)) = 0.10
        _RimSpikeAmount  ("Rim Spike Amount (실루엣)", Range(0,0.6)) = 0.22
        _SpikeFreq   ("Spike Frequency", Range(0.5,12)) = 5.0
        _SpikeSpeed  ("Spike Wobble Speed", Range(0,8)) = 2.5
        _SpikeSharp  ("Spike Sharpness", Range(1,8)) = 3.0
        [Header(Fade)]
        _FadeMul ("Fade Multiplier (가산 페이드)", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewDirWS : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor; float4 _RimColor;
                float _CoreIntensity; float _RimIntensity;
                float _CorePower; float _RimPower; float _GapStrength;
                float _CoreSpikeAmount; float _RimSpikeAmount; float _SpikeFreq; float _SpikeSpeed; float _SpikeSharp;
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
                float3 pos = IN.positionOS.xyz;
                float3 nrm = normalize(IN.normalOS);
                // 코어/림 분리: 변위 전 원본 지오메트리의 NdotV로 스파이크 양을 lerp
                //  ndv=1(정면=코어) → CoreSpike, ndv=0(실루엣=림) → RimSpike
                float3 posWS0 = TransformObjectToWorld(pos);
                float3 nWS0 = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 vDir0 = normalize(GetWorldSpaceViewDir(posWS0));
                float ndv0 = saturate(dot(nWS0, vDir0));
                float spikeAmt = lerp(_RimSpikeAmount, _CoreSpikeAmount, ndv0);
                // 표면 스파이크: 노이즈를 sharpen해 바깥 방향으로 뾰족하게, 시간에 따라 요동
                float t = _Time.y * _SpikeSpeed;
                float n = noise3(nrm * _SpikeFreq + t);
                n = pow(saturate(n), _SpikeSharp);
                pos += nrm * (n * spikeAmt);

                VertexPositionInputs p = GetVertexPositionInputs(pos);
                VertexNormalInputs   nn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = p.positionCS;
                OUT.normalWS = nn.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float ndv = saturate(dot(N, V));
                float core = pow(ndv, _CorePower);
                float rim  = pow(saturate(1.0 - ndv), _RimPower);
                float gap = 1.0 - _GapStrength * (1.0 - core) * (1.0 - rim);
                float3 col = _CoreColor.rgb * core * _CoreIntensity
                           + _RimColor.rgb  * rim  * _RimIntensity;
                col *= gap * _FadeMul;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
