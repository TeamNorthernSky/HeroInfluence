Shader "JC/Indicators/Bridge Overlay"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        _Intensity ("Emission Intensity", Float) = 1
        _VolumeShade ("Arrow Face Contrast", Float) = 0
        _NightIntensityMultiplier ("Night Intensity Multiplier", Float) = 0.2
        _BaseMap ("Fill Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Size ("Cell Size", Vector) = (3.3,3.3,0,0)
        _Inset ("Inset", Float) = 0
        _Thickness ("Thickness", Float) = .045
        _CornerCut ("Corner Cut", Float) = .25
        _IsFill ("Interior", Float) = 0
        _FillOcclusionHeight ("Fill Occlusion World Height", Float) = 0.01
        _ZWrite ("Depth Write", Float) = 1
        _Cull ("Cull Mode", Float) = 2
        _SweepEnabled ("Sweep Enabled", Float) = 1
        _SweepColor ("Sweep Tint", Color) = (1,1,1,1)
        _SweepIntensity ("Sweep Intensity", Float) = 2
        _SweepWidth ("Sweep Width", Float) = .12
        _SweepSoftness ("Sweep Softness", Float) = .5
        _SweepTilt ("Sweep Tilt", Float) = 18
        _SweepDuration ("Sweep Duration", Float) = .8
        _SweepInterval ("Sweep Interval", Float) = 1.5
        _SweepReverse ("Sweep Reverse", Float) = 0
        _SweepTime ("Sweep Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _Intensity, _NightIntensityMultiplier, _VolumeShade;
                float4 _BaseMap_ST, _Size;
                half4 _BaseColor, _SweepColor;
                float _Inset, _Thickness, _IsFill, _CornerCut, _FillOcclusionHeight;
                float _SweepEnabled, _SweepIntensity, _SweepWidth, _SweepSoftness, _SweepTilt;
                float _SweepDuration, _SweepInterval, _SweepReverse, _SweepTime;
            CBUFFER_END
            float4 _JcDayNight;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 cellUV:TEXCOORD1; half fog:TEXCOORD2; half shade:TEXCOORD3; float3 positionWS:TEXCOORD4; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.cellUV=v.positionOS.xz/max(_Size.xy,float2(.001,.001))+.5;
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                float3 normal=TransformObjectToWorldNormal(v.normalOS);
                o.shade=_IsFill>.5?1:saturate(.6+.4*saturate(normal.y)+.12*dot(normal,float3(-.6,0,-.8)));
                o.shade=lerp(o.shade,normal.y>.99?1:lerp(.12,.35,saturate(normal.y)),_VolumeShade);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings v):SV_Target
            {
                half4 result=_BaseColor;
                result.rgb*=v.shade;
                if (_IsFill>.5) result*=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv);
                clip(result.a-.001);
                float duration=max(.05,_SweepDuration);
                float phase=fmod(max(0,_SweepTime),duration+max(0,_SweepInterval));
                if (_IsFill<.5 && _SweepEnabled>.5 && phase<duration)
                {
                    float angle=radians(_SweepTilt);
                    float2 axis=float2(cos(angle),sin(angle));
                    float width=max(.01,_SweepWidth);
                    float outer=width*(1+3*saturate(_SweepSoftness));
                    float extent=.5*(abs(axis.x)+abs(axis.y))+outer;
                    float t=phase/duration;
                    if (_SweepReverse>.5) t=1-t;
                    float distance=abs(dot(v.cellUV-.5,axis)-lerp(-extent,extent,t));
                    float core=1-smoothstep(width*.2,width,distance);
                    float halo=1-smoothstep(width,max(width+.001,outer),distance);
                    float glow=(core+halo*.3*saturate(_SweepSoftness))*max(0,_SweepIntensity)*_SweepColor.a;
                    result.rgb+=lerp(_BaseColor.rgb,half3(1,1,1),.45)*_SweepColor.rgb*glow;
                }
                float daylight=lerp(1,saturate(_JcDayNight.y),saturate(_JcDayNight.x));
                result.rgb*=max(0,_Intensity)*lerp(_NightIntensityMultiplier,1,daylight);
                return result;
            }
            ENDHLSL
        }
    }
}
