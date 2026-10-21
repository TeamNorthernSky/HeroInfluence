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
  _DirectionalWave("방향성 혼합 · 늘어짐 · 굴곡 · 크기",Vector)=(0,5,.65,4)
  _WaveBend("형태 속도 · 각도",Vector)=(.08,0,0,0)
  _FakeHighlight("연출 강도 · 폭 · 선택 기울기 · 그림자",Vector)=(0,.2,.12,.5)
  _FakeHighlightColor("연출 하이라이트 색",Color)=(.7,.85,1,1)
  _Specular("광원 하이라이트 강도 · 거칠기 · 부드러움 · 잔반짝임",Vector)=(1,.32,.8,.25)
  _WaveShade("음영 강도 · 크기 · 부드러움",Vector)=(.3,1.2,.65,0)
  _Surface("물가색 폭 · 그림자",Vector)=(.85,.25,0,0)
  [HideInInspector] _UseWaterTime("독립 시간 사용",Float)=0
  [HideInInspector] _WaterTime("독립 시간",Float)=0
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
    half4 _BaseColor,_EdgeColor,_FakeHighlightColor;float4 _FakeHighlight;float4 _Surface,_WaveShade,_Specular,_DirectionalWave,_WaveBend;
    float _UseWaterTime,_WaterTime;float4 _Flow,_Reflection,_Normals;float _PlanarAvailable;float4x4 _PlanarVP;float _FollowCanal;float4 _FlowMapBounds;
   CBUFFER_END
   float _JcWaterReflectionPass;
   TEXTURE2D(_PlanarReflection);SAMPLER(sampler_PlanarReflection);
   TEXTURE2D(_NormalBroad);SAMPLER(sampler_NormalBroad);
   TEXTURE2D(_NormalDetail);SAMPLER(sampler_NormalDetail);
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
   // 고정 격자마다 방향을 정하고 기울기를 부드럽게 혼합합니다.
   // 픽셀별 회전 좌표가 수로 합류부에서 급격히 변하는 것을 피합니다.
   float2 DirectionalSlopeCell(float2 world,float2 cell,float grid,float time) {
    float2 dir=FlowAt(cell*grid);
    float sn=sin(_WaveBend.y),cs=cos(_WaveBend.y);
    dir=float2(cs*dir.x-sn*dir.y,sn*dir.x+cs*dir.y);
    float2 side=float2(-dir.y,dir.x);
    float2 p=world-dir*time*_Flow.z;
    float length=max(.2,_WaveShade.y),bendSize=max(.5,_DirectionalWave.w);
    float2 q=float2(dot(p,dir),dot(p,side));
    float2 k1=float2(.53,1)/bendSize;
    float2 k2=float2(-.71,.63)/bendSize;
    float b1=dot(q,k1)+time*_WaveBend.x/bendSize;
    float b2=dot(q,k2)-time*_WaveBend.x*1.37/bendSize+1.7;
    // 높이 함수의 위상과 진폭 미분을 함께 반영한 연속 기울기입니다.
    float warp=_DirectionalWave.z;
    float phase=q.x*6.2831853/length+warp*(sin(b1)+.5*sin(b2));
    float2 phaseGradient=float2(6.2831853/length,0)+warp*(cos(b1)*k1+.5*cos(b2)*k2);
    float envelopePhase=q.y/(length*max(1,_DirectionalWave.y))+.3;
    float envelope=.8+.2*sin(envelopePhase);
    float2 envelopeGradient=float2(0,.2*cos(envelopePhase)/(length*max(1,_DirectionalWave.y)));
    float2 gradient=(cos(phase)*phaseGradient*envelope+sin(phase)*envelopeGradient)*length/6.2831853;
    // 부드러운 보조 파형으로 지나치게 동일한 마루 반복을 완화합니다.
    float2 secondary=float2(.83,.19)/length*6.2831853;
    gradient+=.18*cos(dot(q,secondary)+.9)*secondary*length/6.2831853;
    return -(dir*gradient.x+side*gradient.y)*_Normals.x;
   }
   float2 DirectionalSlope(float2 world,float time) {
    float grid=max(2,_WaveShade.y*2);
    float2 cell=floor(world/grid),f=frac(world/grid);
    f=f*f*f*(f*(f*6-15)+10);
    return lerp(lerp(DirectionalSlopeCell(world,cell,grid,time),DirectionalSlopeCell(world,cell+float2(1,0),grid,time),f.x),
                lerp(DirectionalSlopeCell(world,cell+float2(0,1),grid,time),DirectionalSlopeCell(world,cell+1,grid,time),f.x),f.y);
   }
   // 기울기는 미리 제작한 Normal map에서 읽고 두 위상의 이동을 섞습니다.
   // UV가 월드 X/Z이므로 탄젠트 X/Y를 월드 X/Z 기울기로 해석합니다.
   float2 SampleSlope(float2 world,float time,float2 worldDx,float2 worldDy) {
    float2 aUV=world/(max(.2,_WaveShade.y)*8);
    float2 dUV=(world+float2(.73,-.41)*time*_Normals.w)/(max(.05,_Normals.z)*8);
    float2 bUV=(world+float2(-.37,.61)*time*_Normals.w)/(max(.05,_Normals.z)*8*1.37)+float2(.37,.73);
    // 셀 경계의 샘플 이동량 차이가 mip 선택에 섞이지 않도록 원래 월드 미분을 사용합니다.
    float broadScale=max(.2,_WaveShade.y)*8,detailScale=max(.05,_Normals.z)*8;
    half3 a=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NormalBroad,sampler_NormalBroad,aUV,worldDx/broadScale,worldDy/broadScale));
    half3 d=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NormalDetail,sampler_NormalDetail,dUV,worldDx/detailScale,worldDy/detailScale));
    half3 b=UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_NormalDetail,sampler_NormalDetail,bUV,worldDx/(detailScale*1.37),worldDy/(detailScale*1.37)));
    return a.xy/max(.2,a.z)*_Normals.x*(1-saturate(_DirectionalWave.x))+(d.xy/max(.2,d.z)+b.xy/max(.2,b.z))*.5*_Normals.y;
   }
   float2 AdvectedSlope(float2 world,float2 flow,float time,float2 worldDx,float2 worldDy) {
    float span=max(1,_WaveShade.y*2);
    float phase=frac(time*_Flow.z/span),other=frac(phase+.5);
    float blend=smoothstep(0,1,abs(phase*2-1));
    return lerp(SampleSlope(world-flow*phase*span,time,worldDx,worldDy),SampleSlope(world-flow*other*span+float2(13.17,7.31),time,worldDx,worldDy),blend);
   }
   float2 WaterSlope(float2 world,float time) {
    float2 worldDx=ddx(world),worldDy=ddy(world);
    float2 result=float2(0,0);
    if(_FollowCanal<.5) {
     result=AdvectedSlope(world,FlowAt(world),time,worldDx,worldDy);
    } else {
    // 픽셀별 방향으로 UV를 휘지 않습니다. 각 고정 셀에서는 평행이동만 하고,
    // 네 셀에서 읽은 월드 기울기를 혼합해 합류부의 좌표 접힘을 방지합니다.
    float grid=max(2,_WaveShade.y*2);
    float2 cell=floor(world/grid),f=frac(world/grid);
    f=f*f*f*(f*(f*6-15)+10);
    float2 d00=FlowAt(cell*grid),d10=FlowAt((cell+float2(1,0))*grid);
    float2 d01=FlowAt((cell+float2(0,1))*grid),d11=FlowAt((cell+1)*grid);
    // 방향이 같은 직선부는 기존과 동일한 한 쌍의 위상만 샘플링합니다.
    if(dot(d00-d10,d00-d10)+dot(d00-d01,d00-d01)+dot(d00-d11,d00-d11)<1e-8)
     result=AdvectedSlope(world,d00,time,worldDx,worldDy);
    else
     result=lerp(lerp(AdvectedSlope(world,d00,time,worldDx,worldDy),AdvectedSlope(world,d10,time,worldDx,worldDy),f.x),
                 lerp(AdvectedSlope(world,d01,time,worldDx,worldDy),AdvectedSlope(world,d11,time,worldDx,worldDy),f.x),f.y);
    }
    return result;
   }
   // 픽셀 안에서 노멀이 크게 변하면 로브를 넓히고 피크를 낮춰 원거리 점멸을 줄입니다.
   float WaterLightLobe(float ndh,float exponent,float variance,float softness) {
    float filtered=exponent/(1+exponent*variance);
    float lobe=pow(ndh,filtered);
    float edge=lerp(.025,.5,saturate(softness));
    float shaped=smoothstep(.5-edge,.5+edge,lobe);
    return lerp(shaped,lobe,saturate(softness))*filtered/exponent;
   }
   half4 frag(V i):SV_Target {
    clip(.5-_JcWaterReflectionPass);
    float time=(_UseWaterTime>.5?_WaterTime:_Time.y);
    float2 slope=WaterSlope(i.world.xz,time);
    if(_DirectionalWave.x>.001)slope+=DirectionalSlope(i.world.xz,time)*saturate(_DirectionalWave.x);
    half3 normal=normalize(half3(slope.x,1,slope.y));
    half3 view=GetWorldSpaceNormalizeViewDir(i.world);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    // 최종색이 아닌 수면 기울기를 공통 원인으로 사용합니다. 카메라·그림자 변화가 흐름을 생성하지 않습니다.
    // 평평한 수면 대비 광원 쪽으로 기운 면을 밝게, 반대쪽을 어둡게 합니다.
    float shadeField=saturate(.5+(dot(normal,light.direction)-light.direction.y)*1.8);
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
    color*=1-_Surface.y*(1-light.shadowAttenuation);
    // 직접광은 환경 반사 강도와 독립적이며 그림자 속에서는 빛나지 않습니다.
    float3 halfDirection=light.direction+view;
    halfDirection*=rsqrt(max(dot(halfDirection,halfDirection),1e-5));
    float ndh=saturate(dot(normal,halfDirection));
    float3 dx=ddx(normal),dy=ddy(normal);
    float variance=min(.5,.5*(dot(dx,dx)+dot(dy,dy)));
    float exponent=lerp(256,8,saturate(_Specular.y));
    float broad=WaterLightLobe(ndh,exponent,variance,_Specular.z);
    float glint=WaterLightLobe(ndh,exponent*4,variance,_Specular.z);
    float visibility=saturate(dot(normal,light.direction))*light.shadowAttenuation*light.distanceAttenuation;
    color+=light.color*(broad+glint*_Specular.w)*_Specular.x*visibility;
    // 카메라 추종 가상광의 half vector를 수면 위로 유지합니다.
    // 실제 주광원과 독립적으로 같은 물결 노멀을 읽으며 평평한 수면의 균일 발광은 억제합니다.
    if(_FakeHighlight.x>.001){
     float3 fakeHalf=normalize(float3(view.x*_FakeHighlight.z,1,view.z*_FakeHighlight.z));
     float fakeLobe=WaterLightLobe(saturate(dot(normal,fakeHalf)),lerp(512,32,_FakeHighlight.y),variance,_Specular.z);
     float movingSurface=smoothstep(.015,.12,length(slope));
     float fakeVisibility=lerp(1,light.shadowAttenuation,_FakeHighlight.w);
     color+=_FakeHighlightColor.rgb*(_FakeHighlight.x*fakeLobe*movingSurface*fakeVisibility);
    }
    float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
    float depth=SampleSceneDepth(screenUV);
    #if !UNITY_REVERSED_Z
     depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif
    float3 below=ComputeWorldSpacePosition(screenUV,depth,UNITY_MATRIX_I_VP);
    float thickness=max(0,i.world.y-below.y);
    float alpha=1-(1-saturate(_Transmission.x))*exp(-thickness*max(0,_Transmission.y));
    return half4(MixFog(color,i.fog),alpha);
   }
   ENDHLSL
  }
 }
}
