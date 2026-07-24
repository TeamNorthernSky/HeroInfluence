Shader "Testbed/FlareBomb/FireShell_Ball"
{
    Properties
    {
        [HDR] _ColorLow  ("Low Color",  Color) = (1.0, 0.12, 0.02, 1)
        [HDR] _ColorMid  ("Mid Color",  Color) = (1.0, 0.40, 0.05, 1)
        [HDR] _ColorHigh ("High Color", Color) = (1.0, 0.85, 0.40, 1)
        _Emission ("Emission", Range(0,6)) = 2.2
        _RimPower ("Rim Power", Range(0.5,8)) = 2.5
        _RimStrength ("Rim Strength", Range(0,3)) = 1.2
        _NoiseScale ("Noise Scale", Range(0.5,8)) = 3.0
        _RiseSpeed ("Rise Speed", Range(0,6)) = 2.0
        _SwirlSpeed ("Swirl Speed", Range(0,4)) = 0.5
        _FlameStrength ("Flame Lick Strength", Range(0,3)) = 1.6
        _Spread ("Lick Spread (low=wrap all, high=top only)", Range(0,1.5)) = 0.7
        _RiseHeight ("Rise Height (upward licks)", Range(0,2)) = 0.8
        _TipNoiseScale ("Tip Noise Scale (top fine detail)", Range(1,16)) = 6.0
        _TipSharp ("Tip Sharpness (jagged spikes)", Range(0.5,6)) = 1.6
        _FlameContrast ("Flame Contrast (bright vs dark)", Range(0.5,4)) = 1.5
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "FireShell"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

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

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionOS:TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorLow,_ColorMid,_ColorHigh;
                half _Emission,_RimPower,_RimStrength,_NoiseScale,_RiseSpeed,_SwirlSpeed,_FlameStrength,_Spread,_RiseHeight;
                half _TipNoiseScale,_TipSharp,_FlameContrast,_Opacity;
            CBUFFER_END

            Varyings vert(Attributes IN){
                Varyings OUT;
                float3 pos=IN.positionOS.xyz;
                float h=saturate(pos.y/0.5*0.5+0.5);                    // 0 bottom .. 1 top
                float3 sp=pos*_NoiseScale + float3(0,_Time.y*_RiseSpeed,0);
                float dn=fbm(sp);
                // high-freq ridged noise -> sharp peaks, concentrated at the very top
                float3 tp = pos*_TipNoiseScale + float3(0,_Time.y*_RiseSpeed*1.3,0);
                float ridge = pow(saturate(1.0 - abs(2.0*fbm(tp) - 1.0)), _TipSharp);
                // base smooth upward licks + spiky tips (mesh-density limited)
                pos.y += pow(h,1.5) * (0.35 + dn*0.9) * _RiseHeight
                       + pow(h,3.0) * ridge * _RiseHeight * 0.7;
                // slight inward pinch high up for tongue shape
                float pinch = lerp(1.0, 0.75, saturate((h-0.6)/0.4));
                pos.xz *= pinch;

                float3 posWS=TransformObjectToWorld(pos);
                OUT.positionHCS=TransformWorldToHClip(posWS);
                OUT.positionWS=posWS;
                OUT.normalWS=TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS=pos;
                return OUT;
            }

            half3 ramp(float v){ v=saturate(v);
                return (v<0.5)?lerp(_ColorLow.rgb,_ColorMid.rgb,v*2.0):lerp(_ColorMid.rgb,_ColorHigh.rgb,(v-0.5)*2.0); }

            half4 frag(Varyings IN):SV_Target{
                float3 N=normalize(IN.normalWS);
                float3 V=normalize(GetCameraPositionWS()-IN.positionWS);
                half fres=pow(saturate(1.0-saturate(dot(N,V))),_RimPower);

                float t=_Time.y;
                float v=saturate(IN.positionOS.y/0.5*0.5+0.5);          // 0 bottom .. 1 top
                float3 p=IN.positionOS*_NoiseScale;
                p=mul(rotY(t*_SwirlSpeed),p);
                p.y*=0.55;                                              // vertical stretch
                float3 flow=float3(0, t*_RiseSpeed, 0);                 // scroll up
                float n=fbm(p+flow);

                // extra high-frequency noise blended in toward the top -> finer, spikier tongues
                float3 ptip=IN.positionOS*_TipNoiseScale;
                ptip=mul(rotY(t*_SwirlSpeed*1.3),ptip);
                ptip.y*=0.55;
                float ntip=fbm(ptip+flow*1.3);
                float nmix=lerp(n, ntip, pow(v,2.0));

                // upward licks: noise survives more toward top; sharpen the edge into jagged tips
                float lickRaw=nmix - (1.0 - v)*_Spread;
                float soft=max(0.03, 0.45 / max(_TipSharp,0.5));        // _TipSharp narrows edge = crisper
                float lick=smoothstep(0.15 - soft, 0.15 + soft, lickRaw);
                // fiery silhouette ring (envelops sphere from any angle) + upward licks, scaled by opacity
                half a=saturate(fres*_RimStrength + lick*_FlameStrength) * _Opacity;

                // internal flicker contrast: push noise away from mid -> brighter brights, darker darks
                float nc=saturate((nmix - 0.5)*_FlameContrast + 0.5);
                float rampv=saturate(nc*0.5 + v*0.35 + fres*0.3);
                half3 col=ramp(rampv);
                half3 emission=col*_Emission*a;
                return half4(emission, a);
            }
            ENDHLSL
        }
    }
    CustomEditor "FlameShaderGUI"
    Fallback Off
}
