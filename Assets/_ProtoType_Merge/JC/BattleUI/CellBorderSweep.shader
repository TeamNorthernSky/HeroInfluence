Shader "JC/Battle/CellBorderSweep"
{
    Properties
    {
        _BaseMap ("Tile Texture", 2D) = "white" {}
        _BaseColor ("Tile Color", Color) = (1,1,1,1)
        _SweepEnabled ("Sweep Enabled", Float) = 0
        [HDR] _SweepColor ("Sweep Color", Color) = (.35,.75,1,1)
        _SweepIntensity ("Sweep Intensity", Range(0,8)) = 2
        _SweepWidth ("Sweep Half Width", Range(.01,.5)) = .12
        _SweepSoftness ("Sweep Softness", Range(0,1)) = .5
        _SweepTilt ("Sweep Tilt", Range(-80,80)) = 18
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
            Name "CellBorderSweep"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _SweepColor;
                float _SweepEnabled, _SweepIntensity, _SweepWidth, _SweepSoftness, _SweepTilt;
                float _SweepDuration, _SweepInterval, _SweepReverse, _SweepTime;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.uv=TRANSFORM_TEX(input.uv,_BaseMap); o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);
                half4 result=tex*_BaseColor;
                // 원본 파란 테두리는 B > G > R입니다. 보라 중앙/빨강/노랑/검정/금색은 제외합니다.
                half blueMask=smoothstep(.01,.05,tex.g-tex.r)*smoothstep(.025,.1,tex.b-tex.g);
                float duration=max(.05,_SweepDuration);
                float phase=fmod(max(0,_SweepTime),duration+max(0,_SweepInterval));
                if (_SweepEnabled > .5 && phase < duration)
                {
                    float angle=radians(_SweepTilt);
                    float2 axis=float2(cos(angle),sin(angle));
                    float width=max(.01,_SweepWidth);
                    float outer=width*(1+3*saturate(_SweepSoftness));
                    float extent=.5*(abs(axis.x)+abs(axis.y))+outer;
                    float t=phase/duration; if(_SweepReverse>.5)t=1-t;
                    float position=lerp(-extent,extent,t);
                    float distance=abs(dot(input.uv-.5,axis)-position);
                    float core=1-smoothstep(width*.2,width,distance);
                    float halo=1-smoothstep(width,max(width+.001,outer),distance);
                    float glow=(core+halo*.3*saturate(_SweepSoftness))*blueMask*max(0,_SweepIntensity)*_SweepColor.a;
                    result.rgb+=_SweepColor.rgb*glow;
                }
                result.rgb=MixFog(result.rgb,input.fog);
                return result;
            }
            ENDHLSL
        }
    }
}
