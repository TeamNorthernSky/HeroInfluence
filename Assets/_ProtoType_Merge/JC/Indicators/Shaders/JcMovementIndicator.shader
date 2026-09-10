Shader "JC/Indicators/Movement"
{
    Properties
    {
        [HideInInspector] _Color("Color", Color) = (0,1,0,1)
        [HideInInspector] _Mode("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Indicator"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 worldXZ : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _UnreachableColor, _HighlightColor, _MarkerClip;
                float4 _Shape; // border, corner, ring radius, ring width
                float4 _Wave; // width, speed, period, strength
                float4 _Dash; // length, gap, width, speed
                float _DotRadius, _Opacity, _Mode, _Shadow, _Softness, _Clock;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                o.worldXZ = TransformObjectToWorld(v.positionOS.xyz).xz;
                return o;
            }
            float Box(float2 p, float halfSize, float radius)
            {
                radius = min(radius, halfSize);
                float2 q = abs(p) - (halfSize - radius);
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float sd;
                float wave = 0;
                if (_Mode < .5)
                {
                    float outer = Box(i.uv, .5, _Shape.y);
                    float inner = Box(i.uv, .5 - _Shape.x, _Shape.y * .85);
                    float border = max(outer, -inner);
                    float r = length(i.uv);
                    float ring = max(r - _Shape.z, (_Shape.z - min(_Shape.w, _Shape.z)) - r);
                    float dot = _DotRadius > 0 ? r - _DotRadius : 10;
                    sd = min(border, min(ring, dot));
                    float radius = .74 - fmod(_Clock, max(.05, _Wave.z)) * _Wave.y;
                    wave = (1 - smoothstep(0, max(.005, _Wave.x), abs(r - radius)))
                         * step(0, radius) * smoothstep(0, .06, radius);
                }
                else
                {
                    float period = max(.015, _Dash.x + _Dash.y);
                    float x = frac((i.uv.x - _Clock * _Dash.w) / period + .5) * period - period * .5;
                    // Rounded dash: total length stays meaningful even when shorter than the width.
                    float radius = min(_Dash.z, _Dash.x) * .5;
                    float2 q = abs(float2(x, i.uv.y)) - float2(_Dash.x * .5 - radius, _Dash.z * .5 - radius);
                    sd = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                }
                float aa = max(fwidth(sd), .0005);
                float alpha = 1 - smoothstep(-aa - _Softness, aa + _Softness, sd);
                if (_Mode > .5 && _MarkerClip.w > .5)
                {
                    float edge = Box((i.worldXZ - _MarkerClip.xy) / max(.001, _MarkerClip.z), .5, _Shape.y);
                    alpha *= smoothstep(-fwidth(edge), max(.0005, fwidth(edge)), edge);
                }
                float4 tint = lerp(_UnreachableColor, _Color, i.color.r);
                if (_Shadow > .5) tint = _Color;
                float3 rgb = tint.rgb + wave * _Wave.w * _HighlightColor.rgb * _HighlightColor.a * (1 - _Shadow);
                return half4(rgb, alpha * tint.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
