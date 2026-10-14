Shader "JC/Environment/Calm Canal Water"
{
 Properties {
  _BaseColor("중앙 물색",Color)=(.135,.33,.39,1)
  _EdgeColor("물가 색",Color)=(.265,.465,.50,1)
  _Flow("방향 XY · 속도 Z · 크기 W",Vector)=(1,0,.035,1.8)
  _Ripple("잔물결 크기 · 강도 · 속도 · 흐름선",Vector)=(.32,.035,.25,.025)
  _WaveStrength("물결 강도",Float)=.07
  _WaveVisibility("큰 물결 음영",Float)=1
  _RippleVisibility("잔물결 음영",Float)=1
  _Shine("반사 강도",Float)=.12
  _EdgeWidth("물가 띠 폭",Float)=.05
  [HideInInspector] _FlowMap("수로 방향 지도",2D)="gray" {}
  [HideInInspector] _FollowCanal("수로 따라 흐름",Float)=0
  [HideInInspector] _FlowMapBounds("방향 지도 범위",Vector)=(0,0,1,1)
  [HideInInspector] _TimeOffset("미리보기 시간",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   ZWrite On Cull Back
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor,_EdgeColor;float4 _Flow,_Ripple;half _WaveStrength,_Shine,_EdgeWidth,_WaveVisibility,_RippleVisibility;float _TimeOffset,_FollowCanal;float4 _FlowMapBounds;
   CBUFFER_END
   TEXTURE2D(_FlowMap);SAMPLER(sampler_FlowMap);
   struct A {float4 positionOS:POSITION;float2 shore:TEXCOORD1;};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float shore:TEXCOORD1;half fog:TEXCOORD2;float4 shadow:TEXCOORD3;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.shore=i.shore.x;o.fog=ComputeFogFactor(p.positionCS.z);o.shadow=GetShadowCoord(p);return o;}
   float HashWater(float2 p) {
    float3 q=frac(float3(p.x,p.y,p.x)*float3(.1031,.1030,.0973));
    q+=dot(q,q.yxz+33.33);return frac((q.x+q.y)*q.z);
   }
   // 월드 좌표의 불규칙한 값에서 높이와 기울기를 얻습니다. 타일 UV는 사용하지 않습니다.
   float3 WaterNoise(float2 p) {
    float2 cell=floor(p),f=frac(p);
    float2 u=f*f*f*(f*(f*6-15)+10),du=30*f*f*(f-1)*(f-1);
    float a=HashWater(cell),b=HashWater(cell+float2(1,0)),c=HashWater(cell+float2(0,1)),d=HashWater(cell+1);
    float k=a-b-c+d;
    return float3(a+(b-a)*u.x+(c-a)*u.y+k*u.x*u.y,(b-a+k*u.y)*du.x,(c-a+k*u.x)*du.y);
   }
   float4 WaterSurface(float2 moving,float2 across,float time) {
    float2 p=moving/max(.1,_Flow.w);
    float3 broad=WaterNoise(p*.67+float2(19.37,71.83));
    float2 warp=float2(broad.x,WaterNoise(p*.51+float2(-43.2,17.9)).x)-.5;
    float2 q=p+warp*.55;
    float3 first=WaterNoise(q*1.73+float2(13.1,-27.4));
    float2 rotated=float2(q.x*.8-q.y*.6,q.x*.6+q.y*.8)*2.39+float2(73.7,9.3)+across*sin(time*_Ripple.z*.7)*.035;
    float3 second=WaterNoise(rotated);
    float2 secondGradient=float2(second.y*.8+second.z*.6,-second.y*.6+second.z*.8);
    float2 broadGradient=(first.yz+secondGradient*.55)*_WaveStrength;
    // 월드 좌표의 독립적인 두 잔물결이 이동하며 일렁입니다. 타일별 위상은 없습니다.
    float2 fine=moving/max(.1,_Ripple.x);
    float3 ripA=WaterNoise(fine+warp*.8+float2(117.1,-35.4)+float2(.23,.17)*time*_Ripple.z);
    float2 fineRot=float2(fine.x*.93+fine.y*.367,-fine.x*.367+fine.y*.93)*1.417;
    float3 ripB=WaterNoise(fineRot+float2(-83.7,119.5)+float2(-.19,.13)*time*_Ripple.z);
    float2 ripGradient=float2(ripB.y*.93-ripB.z*.367,ripB.y*.367+ripB.z*.93);
    float fineFade=1-smoothstep(.35,1.2,max(length(ddx(fine)),length(ddy(fine))));
    float2 fineGradient=(ripA.yz+ripGradient*.6)*_Ripple.y*2*fineFade;
    // 반사광이 없는 구도에서도 읽히는 유색 음영입니다. 흰 선/임계값 패턴은 쓰지 않습니다.
    float shade=dot(broadGradient,float2(.8,.6))*_WaveVisibility*1.6
               +dot(fineGradient,float2(-.6,.8))*_RippleVisibility*1.5;
    return float4(broadGradient+fineGradient,broad.x,shade);
   }
   half4 frag(V i):SV_Target {
    float time=_Time.y+_TimeOffset;
    float2 flow=_Flow.xy;
    if(_FollowCanal>.5)flow=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,(i.world.xz-_FlowMapBounds.xy)*_FlowMapBounds.zw).rg*2-1;
    float2 across=float2(-flow.y,flow.x);
    // 두 위상의 이동을 섞어 방향이 꺾이는 곳의 무한한 늘어짐을 막습니다.
    float travel=time*_Flow.z;
    float span=max(.25,_Flow.w*.75);
    float phase=frac(travel/span),phaseB=frac(travel/span+.5);
    float blend=abs(phase*2-1);
    float4 surface=lerp(WaterSurface(i.world.xz-flow*phase*span,across,time),WaterSurface(i.world.xz-flow*phaseB*span+float2(37.17,83.41),across,time),blend);
    float2 gradient=surface.xy;
    half3 n=normalize(half3(-gradient.x,1,-gradient.y));
    half3 color=lerp(_EdgeColor.rgb,_BaseColor.rgb,smoothstep(0,.85,i.shore));
    // 큰 흐름은 낮은 대비의 불규칙한 색 변화로만 표현합니다.
    color*=1+(surface.z-.5)*_Ripple.w*2;
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    half3 view=GetWorldSpaceNormalizeViewDir(i.world);
    half ndv=saturate(dot(n,view));
    half fresnel=.035+.35*pow(1-ndv,4);
    half3 reflection=GlossyEnvironmentReflection(reflect(-view,n),i.world,.38,1);
    half spec=pow(saturate(dot(n,normalize(light.direction+view))),48)*_Shine*.24;
    color=lerp(color,reflection,saturate(fresnel*_Shine*2));
    color*=.78+.22*light.shadowAttenuation;
    color+=light.color*spec*light.shadowAttenuation;
    color*=1+clamp(surface.w,-.4,.5);
    half edge=(1-smoothstep(0,max(.005,_EdgeWidth),i.shore))*.035;
    color+=_EdgeColor.rgb*edge;
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
 }
}
