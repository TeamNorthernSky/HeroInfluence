Shader "HeroInfluence/UI/TutorialIntroPulse"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.83, 0.23, 1)
        _CardSize ("Card Size", Vector) = (620,150,0,0)
        _RectSize ("Drawing Size", Vector) = (754,284,0,0)
        _Elapsed ("Elapsed Seconds", Float) = -1
        _WaveSpeed ("Wave Speed", Float) = 72.22222
        _WaveCount ("Wave Count", Int) = 3
        _WaveInterval ("Wave Interval", Float) = 0.35
        _Spread ("Maximum Spread", Float) = 65
        _BoundaryFade ("Fade Distance Near Boundary", Float) = 12
        _Thickness ("Line Thickness", Float) = 3
        _CornerRadius ("Corner Radius", Float) = 20
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;
            float4 _CardSize, _RectSize, _ClipRect;
            float _Elapsed, _WaveSpeed, _WaveInterval, _Spread, _BoundaryFade, _Thickness, _CornerRadius;
            int _WaveCount;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.local = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float RoundedBox(float2 samplePosition, float2 halfSize, float radius)
            {
                radius = min(radius, min(halfSize.x, halfSize.y));
                float2 q = abs(samplePosition) - halfSize + radius;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 samplePosition = (input.uv - 0.5) * _RectSize.xy;
                float2 halfCard = _CardSize.xy * 0.5;
                // The outer rounded rectangle is a hard spatial limit, independent of time.
                float boundary = RoundedBox(samplePosition, halfCard + _Spread, _CornerRadius);
                clip(-boundary);
                float fade = saturate(-boundary / max(_BoundaryFade, 0.001));
                float alpha = 0;
                [loop] for (int wave = 0; wave < min(_WaveCount, 32); wave++)
                {
                    float age = _Elapsed - wave * _WaveInterval;
                    float expansion = max(0, age) * _WaveSpeed;
                    float distance = RoundedBox(samplePosition, halfCard + expansion, _CornerRadius);
                    float signedLine = abs(distance + _Thickness * 0.5) - _Thickness * 0.5;
                    float aa = max(fwidth(samplePosition.x), fwidth(samplePosition.y));
                    aa = max(aa, 0.5);
                    float coverage = 1 - smoothstep(-aa, aa, signedLine);
                    float active = step(0, age) * (1 - step(_Spread, expansion));
                    alpha = max(alpha, coverage * active);
                }
                fixed4 result = input.color;
                result.a *= alpha * fade;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
