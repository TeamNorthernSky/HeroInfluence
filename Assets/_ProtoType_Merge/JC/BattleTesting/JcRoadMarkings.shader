Shader "JC/Environment/Road Markings"
{
 Properties
 {
  _BaseMap("원본 도로",2D)="white"{} _BaseColor("색상",Color)=(1,1,1,1)
  _BumpMap("도로 노멀",2D)="bump"{} _BumpScale("노멀 강도",Float)=.25
  _Smoothness("매끄러움",Range(0,1))=.08 _Metallic("금속",Float)=0
  _SpecColor("반사",Color)=(.2,.2,.2,1) _OcclusionStrength("음영",Float)=1
  _EmissionColor("발광",Color)=(0,0,0,0) _EmissionMap("발광 맵",2D)="white"{}
  _DetailAlbedoMap("입자",2D)="linearGrey"{} _DetailAlbedoMapScale("입자 강도",Float)=.2
  _DetailNormalMap("입자 노멀",2D)="bump"{} _DetailNormalMapScale("입자 노멀 강도",Float)=0
  _DetailMask("입자 마스크",2D)="white"{}
  _PlainMap("무표시 아스팔트",2D)="gray"{}
  _MarkMode("표시: 횡단보도 1 중앙선 2 잘라내기 3 도색메시 4",Float)=1
  _LineNormal("UV 수직축과 중심",Vector)=(0,1,0,0)
  _LineWorld("월드 길이축",Vector)=(1,0,0,0)
  _TileMeters("UV 단위 실제 폭",Float)=1
  _LineWidth("점선 폭",Float)=.15 _DashLength("마디 길이",Float)=2.5 _DashGap("마디 간격",Float)=3.5
  _Emboss("횡단보도 요철",Float)=.045
  [HideInInspector] _Surface("Surface",Float)=0
  [HideInInspector] _Cutoff("Cutoff",Float)=.5
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex LitPassVertex
   #pragma fragment RoadFragment
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #pragma shader_feature_local _DETAIL_SCALED
   #define _NORMALMAP 1
   #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
   TEXTURE2D(_PlainMap); SAMPLER(sampler_PlainMap);
   float _MarkMode,_TileMeters,_LineWidth,_DashLength,_DashGap,_Emboss;
   float4 _LineNormal,_LineWorld;
   float Yellow(float2 uv)
   {
    float3 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
    return smoothstep(.012,.045,min(c.r,c.g)-c.b);
   }
   half4 RoadFragment(Varyings input):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData s; InitializeStandardLitSurfaceData(input.uv,s);
    float mask=Yellow(input.uv);
    if(_MarkMode<1.5)
    {
     s.albedo=lerp(s.albedo,half3(.82,.82,.80)*_BaseColor.rgb,mask);
     float2 e=float2(1.0/512,0);
     float2 slope=float2(Yellow(input.uv+e)-Yellow(input.uv-e),Yellow(input.uv+e.yx)-Yellow(input.uv-e.yx));
     s.normalTS=normalize(s.normalTS+float3(-slope*_Emboss,0));
    }
    else if(_MarkMode>3.5)
    {
     // 실제 점선 메시: 깊이와 노멀 패스도 같은 외곽을 사용합니다.
     s.albedo=half3(.85,.59,.045);
    }
    else
    {
     // 타일에 구워진 굵은 표시를 지우고 월드 거리로 마디 주기를 계산합니다.
     half3 asphalt=SAMPLE_TEXTURE2D(_PlainMap,sampler_PlainMap,input.uv).rgb*_BaseColor.rgb;
     half3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb*_BaseColor.rgb;
     s.albedo=asphalt*s.albedo/max(original,half3(.01,.01,.01));
     float distance=abs(dot(input.uv,_LineNormal.xy)-_LineNormal.z)*_TileMeters;
     float aa=max(fwidth(distance),.002);
     float stripe=1-smoothstep(_LineWidth*.5-aa,_LineWidth*.5+aa,distance);
     stripe*=step(.8,input.normalWS.y);
     float period=max(.2,_DashLength+_DashGap);
     float phase=frac(dot(input.positionWS,_LineWorld.xyz)/period)*period;
     float da=max(fwidth(phase),.01);
     float dash=smoothstep(0,da,phase)*(1-smoothstep(_DashLength-da,_DashLength+da,phase));
     if(_MarkMode>2.5)clip(stripe*dash-.5);
     s.albedo=lerp(s.albedo,half3(.85,.59,.045),stripe*dash);
    }
    InputData data;InitializeInputData(input,s.normalTS,data);
    half4 color=UniversalFragmentPBR(data,s);color.rgb=MixFog(color.rgb,data.fogCoord);color.a=1;return color;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
