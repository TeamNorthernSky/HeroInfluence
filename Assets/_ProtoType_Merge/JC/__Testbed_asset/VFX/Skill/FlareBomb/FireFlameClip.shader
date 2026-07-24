Shader "Testbed/FlareBomb/FireFlameClip"
{
    Properties
    {
        [HDR] _ColorLow  ("Low Color",  Color) = (1.0, 0.10, 0.02, 1)
        [HDR] _ColorMid  ("Mid Color",  Color) = (1.0, 0.40, 0.05, 1)
        [HDR] _ColorHigh ("High Color", Color) = (1.0, 0.85, 0.40, 1)
        [HDR] _EdgeColor ("Edge Hot Color", Color) = (1.0, 0.95, 0.6, 1)
        _Emission ("Emission", Range(0,6)) = 2.2
        _NoiseScale ("Noise Scale", Range(0.5,8)) = 3.0
        _RiseSpeed ("Rise Speed", Range(0,6)) = 2.2
        _SwirlSpeed ("Swirl Speed", Range(0,4)) = 0.4
        _Cutoff ("Cutoff", Range(0,1)) = 0.42
        _TopBias ("Top Eat (tongues)", Range(0,2)) = 1.1
        _EdgeWidth ("Edge Softness", Range(0.002,0.3)) = 0.06
        _EdgeHot ("Edge Hot Width", Range(0,0.4)) = 0.12
        _HeightMin ("Height Min (OS y bottom)", Float) = -0.5
        _HeightMax ("Height Max (OS y top)", Float) = 0.5
        _VerticalStretch ("Vertical Stretch (lower=longer streaks)", Range(0.1,1)) = 0.32
        _BaseFill ("Base Fill (bottom always solid)", Range(0,1)) = 0.0
        _BaseHeight ("Base Fill Height", Range(0.05,1)) = 0.4
        _TopCut ("Top Cut (no draw above)", Range(0,1.2)) = 1.2
        _TopCutBand ("Top Cut Softness", Range(0.01,0.4)) = 0.08
        _BaseJagged ("Base Edge Jaggedness", Range(0,1)) = 0.0
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FireFlameClip"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float hash31(float3 p){ p=frac(p*0.3183099+0.1); p*=17.0; return frac(p.x*p.y*p.z*(p.x+p.y+p.z)); }
            float vnoise(float3 x){
                float3 i=floor(x),f=frac(x); f=f*f*(3.0-2.0*f);
                float n000=hash31(i),n100=hash31(i+float3(1,0,0)),n010=hash31(i+float3(0,1,0)),n110=hash31(i+float3(1,1,0));
                float n001=hash31(i+float3(0,0,1)),n101=hash31(i+float3(1,0,1)),n011=hash31(i+float3(0,1,1)),n111=hash31(i+float3(1,1,1));
                float nx00=lerp(n000,n100,f.x),nx10=lerp(n010,n110,f.x),nx01=lerp(n001,n101,f.x),nx11=lerp(n011,n111,f.x);
                return lerp(lerp(nx00,nx10,f.y),lerp(nx01,nx11,f.y),f.z);
            }
            float fbm(float3 p){ float s=0,a=0.5; [unroll] for(int i=0;i<4;i++){ s+=a*vnoise(p); p*=2.03; a*=0.5;} return s; }
            float3x3 rotY(float a){ float c=cos(a),s=sin(a); return float3x3(c,0,s,0,1,0,-s,0,c); }

            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 positionOS:TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorLow,_ColorMid,_ColorHigh,_EdgeColor;
                half _Emission,_NoiseScale,_RiseSpeed,_SwirlSpeed,_Cutoff,_TopBias,_EdgeWidth,_EdgeHot;
                float _HeightMin,_HeightMax;
                half _VerticalStretch,_BaseFill,_BaseHeight,_TopCut,_TopCutBand,_BaseJagged,_Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                OUT.positionHCS=TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS=IN.positionOS.xyz;
                return OUT;
            }
            half3 ramp(float v){ v=saturate(v);
                return (v<0.5)?lerp(_ColorLow.rgb,_ColorMid.rgb,v*2.0):lerp(_ColorMid.rgb,_ColorHigh.rgb,(v-0.5)*2.0); }

            half4 frag(Varyings IN):SV_Target{
                float t=_Time.y;
                float v=saturate((IN.positionOS.y - _HeightMin)/max(0.0001,(_HeightMax - _HeightMin)));   // 0 bottom .. 1 top
                float3 p=IN.positionOS*_NoiseScale;
                p=mul(rotY(t*_SwirlSpeed),p);
                p.y*=_VerticalStretch;                           // lower => longer streaks
                float3 flow=float3(0,-t*_RiseSpeed,0);   // -y => noise scrolls UPWARD (flames rise)
                float n=fbm(p+flow);

                // flame field: noise minus height-eat => crisp licking tongues toward top
                float flame = n - v*_TopBias;
                // base fill: force lower region always solid; noise-jagged top boundary
                float bh = _BaseHeight * (1.0 - _BaseJagged * (n*2.0 - 1.0));
                flame += _BaseFill * saturate(1.0 - v/max(0.0001, bh));
                // top cut: nothing drawn above _TopCut (kills tail tip / dripping)
                flame *= 1.0 - smoothstep(_TopCut - _TopCutBand, _TopCut, v);
                // soft-clip for crisp-but-clean edge
                float a = smoothstep(_Cutoff - _EdgeWidth, _Cutoff + _EdgeWidth, flame);
                if (a <= 0.003) discard;

                // hot bright edge near the cutoff boundary
                float edge = 1.0 - smoothstep(_Cutoff, _Cutoff + _EdgeHot, flame);
                edge *= step(_Cutoff, flame);

                float rampv = saturate((flame - _Cutoff)*1.5 + (1.0 - v)*0.4);
                half3 col = ramp(rampv);
                col = lerp(col, _EdgeColor.rgb, edge*0.8);       // bright licking edges

                half3 emission = col * _Emission * a * _Opacity;
                return half4(emission, a * _Opacity);
            }
            ENDHLSL
        }
    }
    CustomEditor "FlameShaderGUI"
    Fallback Off
}
