Shader "JC/Environment/Road Edge"
{
    Properties {
        _BaseMap("원본 무늬",2D)="white"{}
        _BaseColor("도로 색상",Color)=(1,1,1,1)
        _RoadSurface("도로 조명 사용",Float)=0
        _Smoothness("도로 매끄러움",Float)=0.08
        _BumpMap("도로 노멀",2D)="bump"{}
        _BumpScale("도로 요철",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _RoadSurface,_Smoothness,_BumpScale;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; float4 shadowCoord:TEXCOORD2; float3 world:TEXCOORD3; };
            V vert(A v) { V o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.shadowCoord=GetShadowCoord(p);o.uv=v.uv;o.color=v.color;o.fog=ComputeFogFactor(o.positionCS.z);return o; }
            half4 frag(V i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.color;
                if(_RoadSurface>.5)
                {
                    c*=_BaseColor;
                    half3 n=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
                    InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=normalize(half3(n.x,n.z,n.y));
                    d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=i.shadowCoord;
                    d.bakedGI=SampleSH(d.normalWS);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=1;
                    SurfaceData s=(SurfaceData)0;s.albedo=c.rgb;s.alpha=c.a;s.smoothness=_Smoothness;s.occlusion=1;s.normalTS=half3(0,0,1);
                    half4 lit=UniversalFragmentPBR(d,s);lit.rgb=MixFog(lit.rgb,i.fog);return lit;
                }
                Light l=GetMainLight(i.shadowCoord);c.rgb*=SampleSH(half3(0,1,0))+l.color*saturate(l.direction.y)*l.shadowAttenuation;
                c.rgb=MixFog(c.rgb,i.fog);return c;
            }
            ENDHLSL
        }
    }
}
