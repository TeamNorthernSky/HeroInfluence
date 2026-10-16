Shader "JC/Environment/Calm Canal Water"
{
 Properties {
  _Transmission("수면 불투명도 · 수심 흡수",Vector)=(.52,.65,0,0)
  _BaseColor("중앙 물색",Color)=(.135,.33,.39,1)
  _EdgeColor("물가 색",Color)=(.265,.465,.50,1)
  _EdgeVariation("물가색 불규칙성 · 변화 크기",Vector)=(.65,3.5,0,0)
  _Flow("방향 XY · 속도 Z",Vector)=(1,0,.035,1.8)
  _Reflection("반사 강도 · 선명도 · 시선 · 왜곡",Vector)=(.65,.85,.45,.012)
  [Normal] _NormalBroad("큰 물결 노멀",2D)="bump" {}
  [Normal] _NormalDetail("잔물결 노멀",2D)="bump" {}
  _Normals("큰 기울기 · 작은 기울기 · 잔물결 크기 · 자체 속도",Vector)=(.24,.12,.28,.08)
  [HideInInspector] _PlanarAvailable("평면 반사 준비",Float)=0
  [HideInInspector] _PlanarReflection("평면 반사 영상",2D)="black" {}
  _StreakColor("스트레이크 색",Color)=(.72,.87,.95,1)
  _Streak("강도 · 밀도 · 길이 · 폭",Vector)=(.22,.34,.48,.065)
  _StreakBend("주선 최대 굽힘 · 감소 비율",Vector)=(1,.25,0,0)
  _StreakLifetime("전체 수명 · 감소 · 생성삭제 · 강도 변동",Vector)=(1,.35,.4,.35)
  _StreakShape("소멸 · 요철 연동 · 길이 다양성 · 폭 다양성",Vector)=(.06,1,.65,.6)
  _WaveShade("음영 강도 · 크기 · 부드러움",Vector)=(.3,1.2,.65,0)
  _Surface("물가색 폭 · 그림자",Vector)=(.85,.25,0,0)
  [HideInInspector] _StreakPattern("연속 주선 · 짧은 보조선",2D)="black" {}
  [HideInInspector] _UseWaterTime("독립 시간 사용",Float)=0
  [HideInInspector] _WaterTime("독립 시간",Float)=0
  [HideInInspector] _StreakWaterMask("수면 범위 마스크 준비",Float)=0
  [HideInInspector] _FlowMap("수로 방향 지도",2D)="gray" {}
  [HideInInspector] _FollowCanal("수로 따라 흐름",Float)=0
  [HideInInspector] _FlowMapBounds("방향 지도 범위",Vector)=(0,0,1,1)
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   ZWrite Off Cull Back
   Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _EdgeVariation,_Transmission;
    half4 _BaseColor,_EdgeColor,_StreakColor;float4 _Streak,_StreakShape,_StreakLifetime,_StreakBend,_Surface,_WaveShade;
    float _UseWaterTime,_WaterTime;float4 _Flow,_Reflection,_Normals;float _PlanarAvailable;float4x4 _PlanarVP;float _FollowCanal,_StreakWaterMask;float4 _FlowMapBounds;
   CBUFFER_END
   float _JcWaterReflectionPass;
   TEXTURE2D(_PlanarReflection);SAMPLER(sampler_PlanarReflection);
   TEXTURE2D(_NormalBroad);SAMPLER(sampler_NormalBroad);
   TEXTURE2D(_NormalDetail);SAMPLER(sampler_NormalDetail);
   TEXTURE2D(_StreakPattern);SAMPLER(sampler_StreakPattern);
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
   float2 FlowAt(float2 world) {
    float2 flow=_Flow.xy;
    if(_FollowCanal>.5)flow=SAMPLE_TEXTURE2D_LOD(_FlowMap,sampler_FlowMap,(world-_FlowMapBounds.xy)*_FlowMapBounds.zw,0).rg*2-1;
    return dot(flow,flow)>.001?normalize(flow):float2(1,0);
   }
   // 기울기는 미리 제작한 Normal map에서 읽고 두 위상의 이동을 섞습니다.
   // UV가 월드 X/Z이므로 탄젠트 X/Y를 월드 X/Z 기울기로 해석합니다.
   float2 SampleSlope(float2 world,float time) {
    float2 aUV=world/(max(.2,_WaveShade.y)*8);
    float2 dUV=(world+float2(.73,-.41)*time*_Normals.w)/(max(.05,_Normals.z)*8);
    float2 bUV=(world+float2(-.37,.61)*time*_Normals.w)/(max(.05,_Normals.z)*8*1.37)+float2(.37,.73);
    half3 a=UnpackNormal(SAMPLE_TEXTURE2D(_NormalBroad,sampler_NormalBroad,aUV));
    half3 d=UnpackNormal(SAMPLE_TEXTURE2D(_NormalDetail,sampler_NormalDetail,dUV));
    half3 b=UnpackNormal(SAMPLE_TEXTURE2D(_NormalDetail,sampler_NormalDetail,bUV));
    return a.xy/max(.2,a.z)*_Normals.x+(d.xy/max(.2,d.z)+b.xy/max(.2,b.z))*.5*_Normals.y;
   }
   float2 WaterSlope(float2 world,float2 flow,float time) {
    float span=max(1,_WaveShade.y*2);
    float phase=frac(time*_Flow.z/span),other=frac(phase+.5);
    float blend=smoothstep(0,1,abs(phase*2-1));
    return lerp(SampleSlope(world-flow*phase*span,time),SampleSlope(world-flow*other*span+float2(13.17,7.31),time),blend);
   }
   // 묶음의 중심은 월드 셀에 고정하고 방향은 중심에서 읽습니다.
   // 픽셀마다 회전축을 바꾸지 않아 T자 진입부에서 직선 줄기가 뒤틀리지 않습니다.
   bool StreakOnWater(float2 world) {
    if(_StreakWaterMask<.5)return true;
    float2 uv=(world-_FlowMapBounds.xy)*_FlowMapBounds.zw;
    if(any(uv<0)||any(uv>1))return false;
    return SAMPLE_TEXTURE2D_LOD(_FlowMap,sampler_FlowMap,uv,0).b>.55;
   }
   float StreakBundle(float2 world,float time) {
    // 길이/폭/밀도를 바꿔도 후보의 위치와 개수는 유지됩니다.
    const float cell=1.8;
    float2 dx=ddx(world),dy=ddy(world);
    float2 grid=floor(world/cell);float value=0;
    [unroll] for(int y=-2;y<=2;y++) [unroll] for(int x=-2;x<=2;x++) {
     float2 id=grid+float2(x,y);
     float seed=HashWater(id+19.7);
     float2 center=(id+.5+float2(HashWater(id+7),HashWater(id+31))*.35-.175)*cell;
     float active=smoothstep(0,.05,_Streak.y-HashWater(id+float2(117,63))*.95);
     if(active<=0)continue;
     // 가장자리 셀은 셀 안의 대체 후보를 검사합니다. 물 밖 후보는 그리지 않습니다.
     if(!StreakOnWater(center)) {
      bool found=false;
      [unroll] for(int probe=0;probe<4;probe++) {
       float2 offset=probe==0?float2(.28,0):probe==1?float2(-.28,0):probe==2?float2(0,.28):float2(0,-.28);
       float2 candidate=(id+.5+offset)*cell;
       if(!found && StreakOnWater(candidate)){center=candidate;found=true;}
      }
      if(!found)continue;
     }
     float2 axis=FlowAt(center),side=float2(-axis.y,axis.x);
     // 생성+완전 표시+삭제를 합한 전체 수명입니다. 생성/삭제는 같은 비율을 나눠 씁니다.
     float duration=max(.001,4*max(.1,_StreakLifetime.x)*(1-saturate(_StreakLifetime.y)*HashWater(id+float2(151.7,93.2))));
     float transition=duration*saturate(_StreakLifetime.z)*.5;
     float cycle=duration+.25; // 다음 출현 전 짧은 공백은 표시 수명에서 제외합니다.
     float clock=time/cycle+seed;
     float generation=floor(clock);
     float age=frac(clock)*cycle;
     // 생성 회차마다 강폭 방향의 다른 줄을 선택합니다. 표시 도중에는 고정합니다.
     // 황금비 간격으로 다음 회차가 같은 위치에 몰리는 것을 줄입니다.
     float lane=frac(HashWater(id+float2(211.3,317.9))+generation*.618033989);
     float lateral=(lane-.5)*1.1;
     float2 laneCenter=center+side*lateral;
     bool laneSafe=StreakOnWater(laneCenter)&&StreakOnWater(laneCenter+axis*.6)&&StreakOnWater(laneCenter-axis*.6);
     if(!laneSafe) {
      laneCenter=center-side*lateral;
      laneSafe=StreakOnWater(laneCenter)&&StreakOnWater(laneCenter+axis*.6)&&StreakOnWater(laneCenter-axis*.6);
     }
     if(!laneSafe) {
      laneCenter=center+side*lateral*.5;
      laneSafe=StreakOnWater(laneCenter)&&StreakOnWater(laneCenter+axis*.6)&&StreakOnWater(laneCenter-axis*.6);
     }
     if(laneSafe)center=laneCenter;
     // T자 교차점도 선택된 위치의 흐름에 평행하게 유지합니다.
     axis=FlowAt(center);side=float2(-axis.y,axis.x);
     float phase=age/cycle;
     float life=1;
     center+=axis*(phase-.5)*min(1.2,max(0,_Flow.z)*cycle);
     if(_StreakWaterMask>.5) {
      float2 waterUV=(center-_FlowMapBounds.xy)*_FlowMapBounds.zw;
      float waterMask=SAMPLE_TEXTURE2D_LOD(_FlowMap,sampler_FlowMap,waterUV,0).b;
      life*=smoothstep(.1,.8,waterMask)*step(0,waterUV.x)*step(waterUV.x,1)*step(0,waterUV.y)*step(waterUV.y,1);
     }
     if(life<=.001)continue;
     float2 delta=world-center;
     // 길이·폭은 밀도/수명과 독립된 고정 난수입니다. 이동 중 크기가 떨리지 않습니다.
     float strandLength=_Streak.z*lerp(1-.75*_StreakShape.z,1,HashWater(id+float2(43.1,17.8)));
     float strandWidth=_Streak.w*lerp(1-.75*_StreakShape.w,1,HashWater(id+float2(89.3,51.2)));
     float2 uv=float2(dot(delta,side)/max(.001,strandWidth*8),dot(delta,axis)/strandLength)+.5;
     if(any(uv<0)||any(uv>1))continue;
     // -흐름 방향(uv.y=0)이 꼬리. 생성과 삭제 모두 꼬리→머리 순서입니다.
     float distance=uv.y;
     float feather=clamp(_StreakShape.x,.02,.3);
     float appearProgress=saturate(age/max(.0001,transition))*(1+feather);
     float disappearProgress=saturate((age-(duration-transition))/max(.0001,transition))*(1+feather);
     float appear=smoothstep(distance,distance+feather,appearProgress);
     float disappear=smoothstep(distance,distance+feather,disappearProgress);
     life*=appear*(1-disappear)*step(age,duration);
     if(life<=.001)continue;
     float2 uvDX=float2(dot(dx,side)/max(.001,strandWidth*8),dot(dx,axis)/strandLength);
     float2 uvDY=float2(dot(dy,side)/max(.001,strandWidth*8),dot(dy,axis)/strandLength);
     // 원래 주선의 복합 굴곡을 유지합니다. 1=원래 진폭, 변동폭은 주선별 감소 비율입니다.
     float bendAmount=clamp(_StreakBend.x,0,2)*(1-saturate(_StreakBend.y)*HashWater(id+float2(271.8,13.6)));
     float bendY=uv.y*2-1;
     float bendOffset=bendAmount*(.013*sin(bendY*7)+.006*sin(bendY*19))*.5;
     float bendSlope=bendAmount*(.091*cos(bendY*7)+.114*cos(bendY*19));
     uv.x-=bendOffset;
     uvDX.x-=bendSlope*uvDX.y;uvDY.x-=bendSlope*uvDY.y;
     float3 strokes=SAMPLE_TEXTURE2D_GRAD(_StreakPattern,sampler_StreakPattern,uv,uvDX,uvDY).rgb;
     // R 주선은 끊지 않습니다. 짧은 G 보조선만 B 위상에 따라 서로 다르게 출현합니다.
     float detailLife=smoothstep(.15,.85,.5+.5*sin(time*.85+strokes.b*6.2831853+seed*9));
     float shape=max(strokes.r,strokes.g*detailLife*.3);
     shape*=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
     // 묶음 단위로 요철에 연동하여 가는 선이 점선으로 잘리지 않도록 합니다.
     float2 slope=UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalBroad,sampler_NormalBroad,(center-axis*time*_Flow.z)/(max(.2,_WaveShade.y)*8),0)).xy*_Normals.x;
     float support=lerp(1,lerp(.7,1,smoothstep(.02,.18,length(slope))),_StreakShape.y);
     float intensity=1-saturate(_StreakLifetime.w)*HashWater(id+float2(179.3,241.7));
     value=max(value,shape*life*active*support*intensity);
    }
    return value;
   }
   half4 frag(V i):SV_Target {
    clip(.5-_JcWaterReflectionPass);
    float time=(_UseWaterTime>.5?_WaterTime:_Time.y);
    float2 flow=FlowAt(i.world.xz);
    float2 slope=WaterSlope(i.world.xz,flow,time);
    half3 normal=normalize(half3(slope.x,1,slope.y));
    half3 view=GetWorldSpaceNormalizeViewDir(i.world);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    // 최종색이 아닌 수면 기울기를 공통 원인으로 사용합니다. 카메라·그림자 변화가 흐름을 생성하지 않습니다.
    float shadeField=saturate(.5+dot(slope*3,float2(.8,.6))*.6);
    float streakValue=StreakBundle(i.world.xz,time);
    float streak=streakValue; // 시간 페이드 후 밝기를 다시 깎지 않습니다.
    // 물가 거리의 좌우 대칭을 풀기 위해 월드 좌표로 색 전환 범위를 변화시킵니다.
    float2 edgeUV=i.world.xz/max(.5,_EdgeVariation.y);
    float edgeNoise=saturate(WaterNoise(edgeUV+float2(23,67))*.7+WaterNoise(edgeUV*.41+float2(97,31))*.3);
    float edgeWidth=max(.01,_Surface.x)*lerp(1,lerp(.35,1.8,edgeNoise),_EdgeVariation.x);
    float edgeMask=1-smoothstep(0,edgeWidth,i.shore);
    float awayFromBank=smoothstep(0,max(.02,_Surface.x*.2),i.shore);
    edgeMask*=lerp(1,lerp(.45,1,edgeNoise),_EdgeVariation.x*awayFromBank);
    half3 color=lerp(_BaseColor.rgb,_EdgeColor.rgb,edgeMask);
    float softness=max(.025,_WaveShade.z*.5);
    float shade=smoothstep(.5-softness,.5+softness,shadeField)*2-1;
    // 요철 기울기를 낮은 대비의 색면으로 읽히게 합니다.
    half3 crest=saturate(lerp(color,_EdgeColor.rgb,.5)+half3(.08,.12,.14));
    color=lerp(color,color*.55,saturate(-shade)*_WaveShade.x);
    color=lerp(color,crest,saturate(shade)*_WaveShade.x);
    if(_Reflection.x>.001){
     float2 ripple=slope*3;
     half3 reflection=GlossyEnvironmentReflection(reflect(-view,normal),i.world,1-_Reflection.y,1);
     if(_PlanarAvailable>.5){
      float4 projected=mul(_PlanarVP,float4(i.world,1));
      float2 uv=projected.xy/max(.0001,projected.w)*.5+.5;
      uv+=ripple*_Reflection.w;
      float inside=smoothstep(0,.04,min(min(uv.x,uv.y),min(1-uv.x,1-uv.y)))*step(.0001,projected.w);
      half3 planar=SAMPLE_TEXTURE2D_LOD(_PlanarReflection,sampler_PlanarReflection,saturate(uv),(1-_Reflection.y)*6).rgb;
      reflection=lerp(reflection,planar,inside);
     }
     float fresnel=pow(1-saturate(dot(view,normal)),4);
     float strength=_Reflection.x*lerp(1,lerp(.25,1,fresnel),_Reflection.z);
     color=lerp(color,reflection,strength);
    }
    // 광원 반짝임은 수면 노멀과 시선으로 계산합니다. 고정된 흰 곡선을 그리지 않습니다.
    float spec=pow(saturate(dot(normal,normalize(light.direction+view))),96);
    color+=light.color*spec*_Reflection.x*.25*light.shadowAttenuation;
    half3 streakTint=_StreakColor.rgb*lerp(half3(.15,.15,.15),saturate(light.color),.85);
    color=lerp(color,streakTint,saturate(streak*_Streak.x));
    color*=1-_Surface.y*(1-light.shadowAttenuation);
    float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
    float depth=SampleSceneDepth(screenUV);
    #if !UNITY_REVERSED_Z
     depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif
    float3 below=ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
    float thickness=max(0,i.world.y-below.y);
    float alpha=1-(1-saturate(_Transmission.x))*exp(-thickness*max(0,_Transmission.y));
    alpha=lerp(alpha,1,saturate(streak*_Streak.x));
    return half4(MixFog(color,i.fog),alpha);
   }
   ENDHLSL
  }
 }
}
