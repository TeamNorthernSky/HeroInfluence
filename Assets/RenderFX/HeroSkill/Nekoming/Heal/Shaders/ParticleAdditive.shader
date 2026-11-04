// JC VFX 공용 가산 파티클 셰이더 (URP). 파티클 per-vertex color(COLOR) 지원.
// 소프트 가산: 출력 rgb를 알파로 프리멀티플 후 Blend One One → 부드러운 발광.
Shader "JC/VFX/ParticleAdditive"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        [HDR]_BaseColor ("Tint (HDR)", Color) = (1,1,1,1)
        // 카메라 쪽으로 당겨 그리는 거리(m). 0이면 그대로. 손 사이처럼 가려지는 곳의 빛을 가린 물체 두께만큼만 앞에 보일 때 쓴다.
        _CameraOffset ("Camera Offset (m)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _CameraOffset;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                positionWS += UNITY_MATRIX_V[2].xyz * _CameraOffset;   // 뷰 행렬 3행 = 카메라 쪽 방향(원근·직교 공통)
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 c = tex * IN.color * _BaseColor;
                return half4(c.rgb * c.a, c.a);   // 소프트 가산(프리멀티플)
            }
            ENDHLSL
        }
    }
    Fallback Off
}
