// 루미나 「솔라 프리즘」 스타버스트 플레어 — 시선축 빌보드 쿼드.
// 프리즘 벨트 꼭지점이 카메라와 정렬되는 순간 C#(SolarPrismVfx)이 _Opacity로 점멸시킨다.
// 십자 4줄기 + 대각 보조 줄기 + 중심 가우시안. 가산 블렌드, 큐 3007(최상단).
Shader "Testbed/SolarPrism/StarFlare"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.6, 1.65, 1.8, 1)
        _Emission ("Emission", Range(0,6)) = 1.8
        _Opacity ("Opacity (C# driven)", Range(0,1)) = 1

        [Header(Star Shape)]
        _SpikeNarrow ("Spike Narrowness", Range(4,80)) = 26
        _SpikeFall ("Spike Length Falloff", Range(1,10)) = 3.5
        _DiagRatio ("Diagonal Spike Ratio", Range(0,1)) = 0.35
        _CoreSize ("Core Glow Size (uv)", Range(0.02,0.6)) = 0.16
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "StarFlare"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Emission,_Opacity;
                half _SpikeNarrow,_SpikeFall,_DiagRatio,_CoreSize;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                // 시선축 정렬 빌보드
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

            float Ray(float alongAxis, float acrossAxis)
            {
                return pow(saturate(1.0 - abs(alongAxis)), _SpikeFall)
                     * exp(-pow(acrossAxis * _SpikeNarrow, 2.0));
            }

            half4 frag(Varyings IN):SV_Target{
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);

                float star = Ray(p.x, p.y) + Ray(p.y, p.x);
                float2 pd = float2(p.x + p.y, p.x - p.y) * 0.70710678;
                star += (Ray(pd.x, pd.y) + Ray(pd.y, pd.x)) * _DiagRatio;
                float core = exp(-(r * r) / max(_CoreSize * _CoreSize, 1e-4));

                float a = saturate(star + core) * _Opacity;
                if (a < 0.004) discard;
                return half4(_Color.rgb * _Emission, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
