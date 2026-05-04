Shader "Custom/GridLine"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.15, 0.15, 0.15, 1)
        _LineColor ("Line Color", Color) = (0.2, 1.0, 0.3, 0.15)
        _GridSize ("Grid Size (x=width, y=height)", Vector) = (64, 64, 0, 0)
        _LineWidth ("Line Width", Range(0.001, 0.05)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "GridLine"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _LineColor;
                float4 _GridSize;
                float _LineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 gridUV = input.uv * _GridSize.xy;
                float2 wrapped = frac(gridUV);

                // 그리드 라인: 셀 경계에 가까울수록 1
                float gridLineX = step(wrapped.x, _LineWidth) + step(1.0 - _LineWidth, wrapped.x);
                float gridLineY = step(wrapped.y, _LineWidth) + step(1.0 - _LineWidth, wrapped.y);
                float gridLine = saturate(gridLineX + gridLineY);

                // 선이 아닌 영역은 완전 투명
                half4 col = half4(_LineColor.rgb, _LineColor.a * gridLine);
                return col;
            }
            ENDHLSL
        }
    }
}
