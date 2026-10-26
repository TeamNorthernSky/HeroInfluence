Shader "UliaVfx/VFX/UliaShieldHold"
{
    Properties { _Color ("Color", Color) = (0.6,0.12,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float rim=pow(1-saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),3);
                float pulse=.91+.09*sin(_Time.y*1.8);
                return half4(_Color.rgb*(.6+rim*1.5),(.025+rim*.7)*pulse*_Color.a);
            }
            ENDHLSL
        }
    }
}
