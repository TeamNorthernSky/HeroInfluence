Shader "JC/Battle/SoftCharacter"
{
 Properties
 {
  _BaseMap("Base Map",2D)="white"{}
  _BaseColor("Base Color",Color)=(1,1,1,1)
  _EmissionMap("Emission Map",2D)="white"{}
  _EmissionColor("Emission",Color)=(0,0,0,0)
  _Cutoff("Cutoff",Range(0,1))=.5
  _Cull("Cull",Float)=2
  _ShadeFloor("Shade Floor",Range(0,1))=.7
  _Wrap("Light Wrap",Range(0,1))=.4
  _ShadowStrength("Received Shadow",Range(0,1))=.2
  _AlbedoLift("Albedo Lift",Range(0,1))=.08
  _Saturation("Saturation",Range(0,2))=1.15
  _RimStrength("Rim Strength",Range(0,.3))=.025
  _MoldingStrength("Molding Strength",Range(0,1))=0
  _MoldingSharpness("Molding Sharpness",Range(4,96))=32
  _KeyDirection("Key Direction",Vector)=(.4,.8,-.3,0)
  _KeyTint("Key Tint",Color)=(1,.98,.94,1)
  _FillTint("Fill Tint",Color)=(.87,.92,1,1)
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit"
   Tags{"LightMode"="UniversalForward"}
   Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #pragma shader_feature_local_fragment _ALPHATEST_ON
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_EmissionMap_ST,_KeyDirection;
   half4 _BaseColor,_EmissionColor,_KeyTint,_FillTint;
   float _Cutoff,_ShadeFloor,_Wrap,_ShadowStrength,_AlbedoLift,_RimStrength,_Saturation,_MoldingStrength,_MoldingSharpness;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
   V vert(A i){V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
   half4 frag(V i):SV_Target
   {
    half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
    #if defined(_ALPHATEST_ON)
     clip(albedo.a-_Cutoff);
    #endif
    float3 n=normalize(i.normalWS);
    float lightAmount=smoothstep(-max(.01,_Wrap),1,dot(n,normalize(_KeyDirection.xyz)));
    half3 lighting=lerp(_FillTint.rgb*_ShadeFloor,_KeyTint.rgb,lightAmount);
    Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    lighting*=lerp(1,main.shadowAttenuation,_ShadowStrength);
    half3 color=lerp(albedo.rgb,sqrt(max(albedo.rgb,0)),_AlbedoLift)*lighting;
    half luminance=dot(color,half3(.2126,.7152,.0722));
    color=max(0,lerp(luminance.xxx,color,_Saturation));
    float rim=pow(1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.positionWS))),3);
    color+=albedo.rgb*rim*_RimStrength;
    // 실제 면의 노멀 변화와 좁은 반사띠로 몰딩을 강조합니다. 새 경계선을 만들지는 않습니다.
    float crease=saturate(length(fwidth(n))*5);
    float3 halfVector=normalize(normalize(_KeyDirection.xyz)+GetWorldSpaceNormalizeViewDir(i.positionWS));
    float highlight=pow(saturate(dot(n,halfVector)),max(4,_MoldingSharpness));
    color=color*(1-crease*_MoldingStrength*.4)+_KeyTint.rgb*highlight*_MoldingStrength*.22;
    color+=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
    return half4(MixFog(color,i.fog),albedo.a);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
