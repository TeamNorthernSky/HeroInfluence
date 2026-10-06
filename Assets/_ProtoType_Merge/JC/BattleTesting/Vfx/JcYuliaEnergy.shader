Shader "JC/BattleTesting/YuliaEnergy"
{
    Properties
    {
        [HDR] _Color ("Energy", Color) = (1,0.2,2,1)
        _Mode ("Line 0 / Shell 1 / Particle 2", Float) = 0
        _SrcBlend ("Src", Float) = 5
        _DstBlend ("Dst", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Mode; float _SrcBlend; float _DstBlend;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o; o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; o.color=dot(v.color.rgb,v.color.rgb)<.0001?float4(1,1,1,1):v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float alpha;
                if (_Mode>.5 && _Mode<1.5)
                {
                    float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),2.2);
                    float wave=sin(i.world.y*13+sin(i.world.x*8+_Time.y*1.5)*2+_Time.y*3);
                    float veins=pow(saturate(1-abs(wave)),14);
                    alpha=saturate(rim*.85+veins*.2+.025);
                }
                else if (_Mode>1.5)
                {
                    float2 p=i.uv*2-1; float r=dot(p,p);
                    float cloud=.7+.3*sin(i.uv.x*14+sin(i.uv.y*17)+_Time.y);
                    alpha=pow(saturate(1-r),2)*cloud;
                }
                else alpha=pow(saturate(1-abs(i.uv.y*2-1)),1.4);
                return half4(_Color.rgb*i.color.rgb,alpha*_Color.a*i.color.a);
            }
            ENDHLSL
        }
    }
}
