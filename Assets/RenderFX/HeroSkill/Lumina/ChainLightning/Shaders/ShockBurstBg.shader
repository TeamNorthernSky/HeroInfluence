// 루미나 「체인 라이팅」 감전 배경 버스트 — 시선축 빌보드 쿼드.
// 감전 아크 뒤에 깔리는 스파이키 방사형 버스트. ★알파 블렌딩(가산 아님)으로
// 어두운 보라 실루엣이 배경을 덮어, 그 위의 가산 아크가 광원처럼 도드라진다.
// _Seed 플리커(아크와 공유)로 형상이 함께 명멸. 큐 3004(아크 3005 바로 아래).
Shader "Testbed/ChainLightning/ShockBurstBg"
{
    Properties
    {
        [Header(Colors)]
        _ColorBody ("Body Color (dark violet)", Color) = (0.22, 0.08, 0.38, 1)
        [HDR] _ColorCenter ("Center Color (brighter)", Color) = (0.55, 0.28, 0.95, 1)
        _CenterSize ("Center Glow Size (uv)", Range(0.05,0.8)) = 0.3

        [Header(Driven by CSharp)]
        _Seed ("Flicker Seed", Range(0,512)) = 0
        _Opacity ("Opacity (runtime fade)", Range(0,1)) = 1
        _MaxAlpha ("Max Alpha (material cap)", Range(0,1)) = 0.85

        [Header(Spikes)]
        _SpikeCount ("Spike Count", Range(4,32)) = 14
        _SpikeLen ("Spike Max Length (uv)", Range(0.2,1)) = 0.85
        _LenJitter ("Length Jitter", Range(0,1)) = 0.5
        _SpikeWidth ("Spike Base Half Width (uv)", Range(0.02,0.4)) = 0.14
        _TaperSharp ("Tip Taper Sharpness", Range(0.5,6)) = 2.0
        _PosJitter ("Angular Position Jitter (cell)", Range(0,1)) = 0.6
        _EdgeSoft ("Edge Softness", Range(0.05,1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "ShockBurstBg"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TAU 6.2831853

            float bghash(float a, float b){ return frac(sin(a * 127.1 + b * 269.5 + 74.7) * 43758.5453); }

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBody,_ColorCenter;
                half _CenterSize;
                half _Seed,_Opacity,_MaxAlpha;
                half _SpikeCount,_SpikeLen,_LenJitter,_SpikeWidth,_TaperSharp,_PosJitter,_EdgeSoft;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                // 시선축 정렬 빌보드(ShockAura와 동일)
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

            half4 frag(Varyings IN):SV_Target{
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float ang01 = atan2(p.x, -p.y) / TAU + 0.5;

                float a = ang01 * _SpikeCount;
                float id = floor(a);
                float cellAng = TAU / _SpikeCount;
                float best = 0.0;
                [unroll]
                for (int o = -1; o <= 1; o++)
                {
                    float lid = id + o;
                    float lidW = lid - _SpikeCount * floor(lid / _SpikeCount);   // 각도 랩
                    float h1 = bghash(lidW, _Seed);
                    float h2 = bghash(lidW + 17.3, _Seed);
                    float L = _SpikeLen * (1.0 - _LenJitter * h1);
                    float tt = r / max(L, 0.01);
                    if (tt >= 1.0) continue;
                    float center = 0.5 + (h2 - 0.5) * _PosJitter;
                    float lat = (a - lid - center) * cellAng * max(r, 0.02);
                    float hw = _SpikeWidth * pow(saturate(1.0 - tt), _TaperSharp);
                    float soft = max(hw * _EdgeSoft, 0.004);
                    float s = smoothstep(hw, hw - soft, abs(lat));
                    best = max(best, s);
                }

                float centerGlow = exp(-(r * r) / max(_CenterSize * _CenterSize, 1e-4));
                half3 col = lerp(_ColorBody.rgb, _ColorCenter.rgb, centerGlow);
                float aOut = saturate(best + centerGlow * 0.8) * _MaxAlpha * _Opacity;
                if (aOut < 0.004) discard;
                return half4(col, aOut);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
