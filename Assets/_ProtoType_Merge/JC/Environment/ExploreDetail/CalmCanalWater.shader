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
  _FoamColor("포말 색",Color)=(.91,.97,1,1)
  _Foam("간격 · 밀도 · 두께 · 길이",Vector)=(.45,.52,.055,1.8)
  _FoamCurvature("포말 곡선 변형",Range(0,2))=.85
  _FoamOpacity("포말 불투명도",Range(0,1))=1
  _FoamFade("포말 사라짐 부드러움",Range(.02,.5))=.18
  _WaveShade("음영 강도 · 크기 · 부드러움",Vector)=(.3,1.2,.65,0)
  _Morph("형태 속도 · 변화량 · 물가색 폭 · 그림자",Vector)=(.18,.25,.85,.25)
  _ShoreFoam("물가 사용 · 폭 · 끊김 · 속도",Vector)=(1,.09,.35,.16)
  [HideInInspector] _UseWaterTime("독립 시간 사용",Float)=0
  [HideInInspector] _WaterTime("독립 시간",Float)=0
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
    half4 _BaseColor,_EdgeColor,_FoamColor;float4 _Foam,_Morph,_ShoreFoam,_WaveShade;float _FoamCurvature,_FoamOpacity,_FoamFade;float _UseWaterTime,_WaterTime;float4 _Flow,_Ripple;half _WaveStrength,_Shine,_EdgeWidth,_WaveVisibility,_RippleVisibility;float _TimeOffset,_FollowCanal;float4 _FlowMapBounds;
   CBUFFER_END
   TEXTURE2D(_FlowMap);SAMPLER(sampler_FlowMap);
   struct A {float4 positionOS:POSITION;float2 shore:TEXCOORD1;};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float shore:TEXCOORD1;half fog:TEXCOORD2;float4 shadow:TEXCOORD3;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.shore=i.shore.x;o.fog=ComputeFogFactor(p.positionCS.z);o.shadow=GetShadowCoord(p);return o;}
   float HashWater(float2 p) {
    float3 q=frac(float3(p.x,p.y,p.x)*float3(.1031,.1030,.0973));
    q+=dot(q,q.yxz+33.33);return frac((q.x+q.y)*q.z);
   }
   float SimplexCorner(float2 cell,float2 offset) {
    float angle=HashWater(cell)*6.2831853;
    float2 gradient=float2(cos(angle),sin(angle));
    float weight=max(.5-dot(offset,offset),0);
    weight*=weight;
    return weight*weight*dot(gradient,offset);
   }
   // 삼각 격자의 방사형 gradient kernel. 사각 셀 보간의 축 정렬 윤곽을 없앱니다.
   float WaterNoise(float2 p) {
    float2 cell=floor(p+(p.x+p.y)*.366025404);
    float2 a=p-cell+(cell.x+cell.y)*.211324865;
    float2 corner=a.x>a.y?float2(1,0):float2(0,1);
    float2 b=a-corner+.211324865;
    float2 c=a-1+.422649730;
    return .5+35*(SimplexCorner(cell,a)+SimplexCorner(cell+corner,b)+SimplexCorner(cell+1,c));
   }
   // 모든 타일이 월드 좌표를 공유합니다. 방향장은 좌표 회전 대신 이동과 이웃 샘플에 사용합니다.
   float3 SurfaceField(float2 world,float2 flow,float time) {
    float size=max(.15,_Foam.x);
    float2 p=world/size;
    float t=time*_Morph.x;
    // 낮은 주파수와 제한된 변위로 좌표가 접히며 생기는 뾰족한 윤곽을 완화합니다.
    float2 warp=float2(WaterNoise(p*.28+float2(19,43)),WaterNoise(p*.28+float2(71,11)))-.5;
    float2 motion=float2(sin(t*.71+p.y*.37),cos(t*.59+p.x*.31))*_Morph.y;
    float2 q=p+warp*(_FoamCurvature*.8)+motion;
    // 흐름 방향의 이웃 값을 평균하여 굽은 수로에서도 원점 기준 회전에 의한 뒤틀림을 피합니다.
    float2 stretch=flow*max(0,_Foam.w-1)*.3;
    float shape=WaterNoise(q+float2(3,17))*.6+
        (WaterNoise(q+stretch+float2(3,17))+WaterNoise(q-stretch+float2(3,17)))*.2;
    float coverage=WaterNoise(q*.47+float2(53,29));
    float shade=WaterNoise((world+(q-p)*size)/max(.2,_WaveShade.y)+float2(7,31));
    return float3(shape,coverage,shade);
   }
   float FoamAlpha(float2 field) {
    float contour=abs(field.x-.5);
    float aa=max(fwidth(contour),.002);
    float shape=1-smoothstep(_Foam.z*.5-aa,_Foam.z*.5+aa,contour);
    float density=saturate(_Foam.y);
    float fade=max(.02,_FoamFade);
    // 분포는 모양을 잘라내는 두 번째 경계가 아니라 투명도입니다.
    // 양 끝에서 밀도 0=완전히 끔, 1=모든 곡선 표시를 보장합니다.
    float cutoff=lerp(1+fade,-fade,density);
    float coverage=smoothstep(cutoff-fade,cutoff+fade,field.y);
    return shape*coverage;
   }
   half4 frag(V i):SV_Target {
    float time=(_UseWaterTime>.5?_WaterTime:_Time.y)+_TimeOffset;
    float2 flow=_Flow.xy;
    if(_FollowCanal>.5)flow=SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,(i.world.xz-_FlowMapBounds.xy)*_FlowMapBounds.zw).rg*2-1;
    float span=max(.5,_Foam.x*1.5), phase=frac(time*_Flow.z/span);
    float phaseB=frac(phase+.5), blend=smoothstep(0,1,abs(phase*2-1));
    // 로컬 방향으로 월드 좌표 전체를 회전하면 굽은 수로에서 이음새가 생기므로 이동에만 방향장을 사용합니다.
    float3 fieldA=SurfaceField(i.world.xz-flow*phase*span,flow,time);
    float3 fieldB=SurfaceField(i.world.xz-flow*phaseB*span+float2(37.17,83.41),flow,time);
    // 형상장을 먼저 하나로 연결한 뒤 포말을 한 번만 판정합니다.
    // 두 반투명 포말의 교차 합성으로 생기던 이중 윤곽/겹침을 만들지 않습니다.
    float3 field=lerp(fieldA,fieldB,blend);
    // 중간 위상에서 값의 분산이 줄어 포말 띠가 지나치게 굵어지는 것을 보정합니다.
    field.x=.5+(field.x-.5)*rsqrt((1-blend)*(1-blend)+blend*blend);
    float foam=FoamAlpha(field.xy);
    float shoreNoise=WaterNoise(i.world.xz*2.7+float2(time*_ShoreFoam.w*.23,17));
    float shoreWidth=max(.001,_ShoreFoam.y)*lerp(.45,1,shoreNoise);
    float shoreAA=max(fwidth(i.shore),.001);
    float shore=(1-smoothstep(shoreWidth-shoreAA,shoreWidth+shoreAA,i.shore));
    float shoreCutoff=lerp(-_FoamFade,1+_FoamFade,saturate(_ShoreFoam.z));
    shore*=smoothstep(shoreCutoff-_FoamFade,shoreCutoff+_FoamFade,shoreNoise)*_ShoreFoam.x;
    // 물가 포말도 겹친 부분의 불투명도를 누적하지 않습니다.
    foam=max(foam,shore)*saturate(_FoamOpacity);
    // 사라지는 초미세 선 대신 거리와 픽셀 크기에 맞춰 수면 포말 대비를 완만하게 줄입니다.
    float footprint=max(length(ddx(i.world.xz)),length(ddy(i.world.xz)))/max(.15,_Foam.x);
    foam*=1-smoothstep(.18,.65,footprint);
    half3 color=lerp(_EdgeColor.rgb,_BaseColor.rgb,smoothstep(0,max(.01,_Morph.z),i.shore));
    float softness=max(.025,_WaveShade.z*.5);
    float shade=smoothstep(.5-softness,.5+softness,field.z)*2-1;
    // 색상 음영만 적용합니다. 포말은 이후 합성하므로 흰 포말 내부에는 얼룩이 생기지 않습니다.
    half3 crest=saturate(lerp(color,_EdgeColor.rgb,.5)+half3(.08,.12,.14));
    color=lerp(color,color*.55,saturate(-shade)*_WaveShade.x);
    color=lerp(color,crest,saturate(shade)*_WaveShade.x);
    if(_Shine>.001){
     half3 view=GetWorldSpaceNormalizeViewDir(i.world);
     half3 reflection=GlossyEnvironmentReflection(reflect(-view,half3(0,1,0)),i.world,.65,1);
     color=lerp(color,reflection,_Shine*.25);
    }
    color=lerp(color,_FoamColor.rgb,saturate(foam));
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    color*=1-_Morph.w*(1-light.shadowAttenuation);
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
 }
}
