Shader "JC/Boss/Baked Scenery"
{
 Properties { _BaseMap("표면 색상",2D)="white"{} _BaseColor("색상",Color)=(1,1,1,1) _BakedLight("베이크 조명",2D)="white"{} _AtlasST("아틀라스 영역",Vector)=(1,1,0,0) _EmissionMap("발광",2D)="black"{} _EmissionColor("발광 색상",Color)=(0,0,0,0) }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" } Pass { HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
 TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap); TEXTURE2D(_BakedLight); SAMPLER(sampler_BakedLight); TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseMap_ST,_BaseColor,_AtlasST,_EmissionColor;
 CBUFFER_END
 struct A {float4 position:POSITION;float2 uv:TEXCOORD0;float2 uv2:TEXCOORD1;};
 struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float2 lightuv:TEXCOORD1;};
 V Vert(A i){V o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.lightuv=i.uv2*_AtlasST.xy+_AtlasST.zw;return o;}
 half4 Frag(V i):SV_Target {half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;half3 light=DecodeLightmap(SAMPLE_TEXTURE2D(_BakedLight,sampler_BakedLight,i.lightuv),half4(LIGHTMAP_HDR_MULTIPLIER,LIGHTMAP_HDR_EXPONENT,0,0));return half4(color*light+SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb,1);}
 ENDHLSL } }
}
