Shader "UliaVfx/VFX/UliaOrbDenseBody"
{
    Properties { _BaseColor("Body Color", Color) = (.20,.012,.38,.62) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            V vert(A input)
            {
                V o; o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS); return o;
            }
            half4 frag(V i):SV_Target
            {
                half rim=pow(1-saturate(dot(normalize(i.normalWS),normalize(GetCameraPositionWS()-i.positionWS))),2);
                return half4(_BaseColor.rgb*(.8+rim*.7),saturate(_BaseColor.a+rim*.12));
            }
            ENDHLSL
        }
    }
}
