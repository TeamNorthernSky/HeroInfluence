Shader "Unlit/GridShader"
{
     Properties
    {
        _GridSize ("Grid Size", Float) = 10
        _LineWidth ("Line Width", Float) = 0.02
        _LineColor ("Line Color", Color) = (0,0,0,1)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            float _GridSize;
            float _LineWidth;
            float4 _LineColor;
            float4 _BaseColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex);
                
                o.uv = v.uv * _GridSize;
                return o;
            }

            float gridLine(float2 uv, float width)
            {
                float2 grid = abs(frac(uv - 0.5) - 0.5) / fwidth(uv);
                float line = min(grid.x, grid.y);
                return 1.0 - smoothstep(0.0, width, line);
            }

            float4 frag (v2f i) : SV_Target
            {
                float line = gridLine(i.uv, _LineWidth);
                float4 color = lerp(_BaseColor, _LineColor, line);
                return color;
            }
            ENDHLSL
        }
    }
}
