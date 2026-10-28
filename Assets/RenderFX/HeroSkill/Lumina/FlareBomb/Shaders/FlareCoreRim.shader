// 루미나 「플레어 봄」 차징 오브 — 코어 림 2D 빌보드 스프라이트.
// 코어 셰이더의 프레넬 림(3D 구면)은 실루엣에서 코어를 덮어버리므로,
// 림을 별도 빌보드 쿼드의 절차 링 글로우로 분리한다.
// 렌더 큐 2998 = 코어(3000)·후면 셸(2999)보다 먼저 그려져 항상 뒤(가림 없음).
Shader "Testbed/FlareBomb/FlareCoreRim"
{
    Properties
    {
        [HDR] _Color ("Rim Color", Color) = (1.8, 1.55, 1.0, 1)
        _Intensity ("Intensity", Range(0,6)) = 1.2
        _RingRadius ("Ring Radius (uv, core silhouette=~0.57)", Range(0.1,1)) = 0.6
        _RingWidth ("Ring Width", Range(0.01,0.6)) = 0.15
        _HaloStrength ("Inner Halo Strength", Range(0,1)) = 0.35
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FlareCoreRim"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One   // 가산 글로우(림/헤일로)
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
                half _Intensity,_RingRadius,_RingWidth,_HaloStrength,_Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                // 시선축 정렬 빌보드: 쿼드를 카메라→오브젝트 방향에 수직으로 세운다.
                // (뷰 평면 정렬은 오프센터에서 구 실루엣과 어긋나는 원근 시프트가 커진다)
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
                float r = length(IN.uv * 2.0 - 1.0);
                if (r > 1.15) discard;
                // 링(가우시안 밴드) + 안쪽 헤일로
                float ring = exp(-pow((r - _RingRadius) / max(_RingWidth, 0.001), 2.0));
                float halo = pow(saturate(1.0 - r), 2.0) * _HaloStrength;
                half a = saturate(ring + halo) * _Opacity;
                if (a < 0.01) discard;
                return half4(_Color.rgb * _Intensity, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
