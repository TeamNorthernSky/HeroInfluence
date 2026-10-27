// 루미나 「솔라 프리즘」 크리스탈 본체 — PrismMesh(플랫 셰이딩) 전용 반투명 유리.
// 플랫 노멀 특성상 면마다 N·V가 점프해 회전 시 유리 파셋 계조가 자연 발생.
// 프레넬 림(스침각 에지 강조) + 백열 에미션. _Opacity는 C#(소환 페이드)이 MPB 구동.
// 알파 블렌드, ZWrite Off, 큐 3001.
Shader "Testbed/SolarPrism/PrismCrystal"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorBody ("Body Color", Color) = (0.82, 0.9, 1.05, 1)
        [HDR] _ColorRim ("Rim Color", Color) = (1.45, 1.55, 1.75, 1)
        _Emission ("Emission", Range(0,6)) = 1.3

        [Header(Glass)]
        _BodyAlpha ("Body Alpha", Range(0,1)) = 0.22
        _RimAlpha ("Rim Alpha", Range(0,1)) = 0.85
        _FresnelPow ("Fresnel Power", Range(0.5,8)) = 2.6
        _FacetAmount ("Facet Shading Amount", Range(0,1)) = 0.65

        [Header(Driven by CSharp)]
        _Opacity ("Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "PrismCrystal"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 posWS:TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBody,_ColorRim;
                half _Emission;
                half _BodyAlpha,_RimAlpha,_FresnelPow,_FacetAmount;
                half _Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target{
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetCameraPositionWS() - IN.posWS);
                float ndv = saturate(dot(N, V));
                float fres = pow(1.0 - ndv, _FresnelPow);
                float facet = lerp(1.0, ndv * ndv, _FacetAmount);   // 면 단위 유리 계조

                half3 col = (_ColorBody.rgb * facet + _ColorRim.rgb * fres) * _Emission;
                float a = saturate(_BodyAlpha * facet + _RimAlpha * fres) * _Opacity;
                if (a < 0.004) discard;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
