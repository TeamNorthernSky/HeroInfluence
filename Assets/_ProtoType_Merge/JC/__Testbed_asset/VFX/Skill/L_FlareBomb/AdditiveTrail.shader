Shader "Testbed/FlareBomb/AdditiveTrail"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (1.0, 0.6, 0.15, 1)
        _SoftEdge ("Soft Edge", Range(0.0, 0.5)) = 0.25
        _HeadFade ("Head/Tail Fade Power", Range(0.1, 4)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "AdditiveTrail"
            Tags { "LightMode"="UniversalForward" }
            Blend One One           // additive
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _SoftEdge;
                half _HeadFade;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // v across width (0..1): soft taper at both edges
                half edge = smoothstep(0.0, _SoftEdge, IN.uv.y) * smoothstep(1.0, 1.0 - _SoftEdge, IN.uv.y);
                // u along length (0=head .. 1=tail): fade toward tail
                half lengthFade = pow(saturate(1.0 - IN.uv.x), _HeadFade);
                half a = edge * lengthFade * IN.color.a;
                half3 rgb = _Color.rgb * IN.color.rgb * a;
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    CustomEditor "FlameShaderGUI"
    Fallback Off
}
