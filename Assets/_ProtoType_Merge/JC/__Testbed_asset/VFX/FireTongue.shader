Shader "Testbed/FireTongue"
{
    Properties
    {
        [HDR] _ColorLow  ("Low (base) Color",  Color) = (1.0, 0.12, 0.02, 1)
        [HDR] _ColorMid  ("Mid Color",         Color) = (1.0, 0.40, 0.05, 1)
        [HDR] _ColorHigh ("High (hot) Color",  Color) = (1.0, 0.85, 0.40, 1)
        _Emission ("Emission", Range(0,6)) = 2.0

        _NoiseScale ("Noise Scale", Range(0.5, 8)) = 3.0
        _RiseSpeed ("Rise Speed", Range(0, 6)) = 2.5
        _Dissolve ("Dissolve Edge", Range(0, 1)) = 0.45
        _TipFade ("Tip Fade Power", Range(0.2, 4)) = 1.6
        _BaseFade ("Base Fade", Range(0, 0.5)) = 0.12
        _Flicker ("Per-instance Flicker Phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "FireTongue"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One           // additive-with-alpha (glowy)
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float hash21(float2 p){ p = frac(p*float2(123.34,345.45)); p += dot(p, p+34.345); return frac(p.x*p.y); }
            float vnoise2(float2 x){
                float2 i=floor(x), f=frac(x); f=f*f*(3.0-2.0*f);
                float a=hash21(i), b=hash21(i+float2(1,0)), c=hash21(i+float2(0,1)), d=hash21(i+float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }
            float fbm2(float2 p){ float s=0,a=0.5; [unroll] for(int i=0;i<4;i++){ s+=a*vnoise2(p); p*=2.03; a*=0.5;} return s; }

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorLow,_ColorMid,_ColorHigh;
                half _Emission,_NoiseScale,_RiseSpeed,_Dissolve,_TipFade,_BaseFade,_Flicker;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionHCS=TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv=IN.uv;
                OUT.color=IN.color;
                return OUT;
            }

            half4 frag(Varyings IN):SV_Target{
                float v = saturate(IN.uv.y);                       // 0 base .. 1 tip
                float t = _Time.y * _RiseSpeed + _Flicker;
                // noise scrolling downward in uv => flame rising upward
                float2 np = float2(IN.uv.x*_NoiseScale, IN.uv.y*_NoiseScale - t);
                float n = fbm2(np);

                // flame mask: strong at base, dissolve toward tip, noise-eaten edges
                float tipFade = pow(saturate(1.0 - v), _TipFade);
                float baseFade = smoothstep(0.0, _BaseFade, v);    // fade in at very bottom
                float mask = tipFade * baseFade;
                // noise dissolve: eat away where noise < threshold growing toward tip
                float threshold = _Dissolve * v;
                mask *= smoothstep(threshold, threshold + 0.25, n);

                // color ramp by height+noise (hot at core/low, cooler tips? fire is hot low)
                float ramp = saturate(n * 0.5 + (1.0 - v) * 0.6);
                half3 col = (ramp < 0.5)
                    ? lerp(_ColorLow.rgb, _ColorMid.rgb, ramp*2.0)
                    : lerp(_ColorMid.rgb, _ColorHigh.rgb, (ramp-0.5)*2.0);

                half a = saturate(mask) * IN.color.a;        // particle lifetime alpha
                half3 rgb = col * _Emission * a * IN.color.rgb; // particle lifetime tint
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    CustomEditor "FlameShaderGUI"
    Fallback Off
}
