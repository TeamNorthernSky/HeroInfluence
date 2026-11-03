Shader "JC/Environment/Boss Window Glass"
{
    Properties {
        _Tint ("Glass tint", Color) = (0.63,0.79,0.85,1)
        _FaceAlpha ("Front opacity", Range(0,1)) = 0.055
        _EdgeAlpha ("Grazing opacity", Range(0,1)) = 0.18
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _FaceAlpha, _EdgeAlpha;
            CBUFFER_END
            struct Attributes {float4 vertex:POSITION; float3 normal:NORMAL;};
            struct Varyings {float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1;};
            Varyings Vert(Attributes v) {
                Varyings o;
                o.world=TransformObjectToWorld(v.vertex.xyz);
                o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);
                return o;
            }
            half4 Frag(Varyings i):SV_Target {
                half facing=saturate(abs(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world))));
                half rim=(1-facing)*(1-facing);
                return half4(_Tint.rgb,lerp(_FaceAlpha,_EdgeAlpha,rim));
            }
            ENDHLSL
        }
    }
}
