Shader "JC/Indicators/Movement"
{
    Properties
    {
        [HideInInspector] _Color("Color", Color) = (0,1,0,1)
        [HideInInspector] _Cull("Cull", Float) = 0
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
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 worldXZ : TEXCOORD1; float3 normalWS : TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _UnreachableColor, _HighlightColor, _MarkerClip;
                float4 _Shape; // border, corner, ring radius, ring width
                float4 _Wave; // width, speed, period, strength
                float4 _Dash; // length, gap, width, speed
                float4 _DashTrail; // length, opacity, alpha falloff, width falloff
                float4 _DashGlow; // width, strength
                float4 _FlickerPulse; // leading edge distance from destination, unit length, unused, active
                float4 _DashFlickerRates; // rise and fall curve speed multipliers
                float4 _DashFlicker; // white blend strength, speed, direction
                float _SideBrightness, _MarkerColorCycle;
                float _DotRadius, _Opacity, _Mode, _Shadow, _Softness, _Clock;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldDir(v.normalOS);
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
            float ColorCycle(float period)
            {
                return 1 - abs(frac(_Clock / max(.00001, period)) * 2 - 1);
            }
            float FlickerPulse(float phase)
            {
                if (phase <= 0 || phase >= 2 || _FlickerPulse.w < .5) return 0;
                bool rising = phase < 1;
                float progress = rising ? phase : phase - 1;
                float rate = rising ? _DashFlickerRates.x : _DashFlickerRates.y;
                rate = clamp(rate > 0 ? rate : 1, .1, 10);
                progress = rate * progress / (1 + (rate - 1) * progress);
                progress = progress * progress * (3 - 2 * progress);
                return rising ? progress : 1 - progress;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float sd;
                float wave = 0;
                bool solidMarker = _Mode > 1.5;
                bool dashEffects = _Mode > .5 && _Mode < 1.5 && _Shadow < .5;
                float trail = 0, sideDistance = 0;
                if (_Mode < .5 || solidMarker)
                {
                    float outer = Box(i.uv, .5, _Shape.y);
                    float inner = Box(i.uv, .5 - _Shape.x, _Shape.y * .85);
                    float border = max(outer, -inner);
                    float r = length(i.uv);
                    float ring = max(r - _Shape.z, (_Shape.z - min(_Shape.w, _Shape.z)) - r);
                    float dot = _DotRadius > 0 ? r - _DotRadius : 10;
                    sd = solidMarker ? -1 : min(border, min(ring, dot));
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
                    // Overlap the rounded rear cap to avoid a rectangular seam at the tail's head.
                    float rearDistance = (1 - frac((i.uv.x - _Clock * _Dash.w + _Dash.x * .5 - radius) / period)) * period;
                    float tailLength = min(_DashTrail.x, _Dash.y * .95);
                    float remaining = saturate(1 - rearDistance / max(.00001, tailLength + radius));
                    trail = pow(remaining, max(.2, _DashTrail.z))
                          * _DashTrail.y * step(.00001, tailLength);
                    float widthScale = _DashTrail.w > 0 ? pow(remaining, _DashTrail.w) : 1;
                    sideDistance = abs(i.uv.y) - _Dash.z * .5 * widthScale;
                }
                float aa = max(fwidth(sd), .0005);
                float alpha = 1 - smoothstep(-aa - _Softness, aa + _Softness, sd);
                if (dashEffects)
                {
                    float tailAA = max(fwidth(sideDistance), .0005);
                    float tailAlpha = trail * (1 - smoothstep(-tailAA, tailAA, sideDistance));
                    alpha = max(alpha, tailAlpha);
                    float glowWidth = max(.00001, _DashGlow.x);
                    float glow = max(1 - smoothstep(0, glowWidth, max(0, sd)),
                        trail * (1 - smoothstep(0, glowWidth, max(0, sideDistance))));
                    glow *= _DashGlow.y * step(.00001, _DashGlow.x);
                    alpha += (1 - alpha) * glow;
                }
                if (_Mode > .5 && _Mode < 1.5 && _MarkerClip.w > .5)
                {
                    float edge = Box((i.worldXZ - _MarkerClip.xy) / max(.001, _MarkerClip.z), .5, _Shape.y);
                    alpha *= smoothstep(-fwidth(edge), max(.0005, fwidth(edge)), edge);
                }
                float4 tint = lerp(_UnreachableColor, _Color, i.color.r);
                if (_Shadow > .5) tint = _Color;
                float face = solidMarker ? saturate(i.normalWS.y) : 1;
                float3 rgb = tint.rgb * lerp(_SideBrightness, 1, face);
                if (_Mode < 1.5) rgb = tint.rgb;
                if (_MarkerColorCycle > 0)
                {
                    // Replace only the wave region; the base color must remain visible at the start of the cycle.
                    float3 waveColor = lerp(tint.rgb, _HighlightColor.rgb * _Wave.w, ColorCycle(_MarkerColorCycle));
                    rgb = lerp(rgb, waveColor, wave * saturate(_Wave.w) * _HighlightColor.a * (1 - _Shadow) * face);
                }
                else
                    rgb += wave * _Wave.w * _HighlightColor.rgb * _HighlightColor.a * (1 - _Shadow) * face;
                if (dashEffects)
                {
                    // Destinationward phase is independent of dash motion. Never darken any RGB channel.
                    float direction = _DashFlicker.z < 0 ? -1 : 1;
                    float phase = (-i.uv.x - _FlickerPulse.x) * direction / max(.00001, _FlickerPulse.y);
                    float sparkle = saturate(_DashFlicker.x) * FlickerPulse(phase);
                    float white = max(1, max(rgb.r, max(rgb.g, rgb.b)));
                    rgb = lerp(rgb, float3(white, white, white), sparkle);
                }
                return half4(rgb, alpha * tint.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
