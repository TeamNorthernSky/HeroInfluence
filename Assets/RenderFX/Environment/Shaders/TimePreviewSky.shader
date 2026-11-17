Shader "JC/Environment/Time Preview Sky"
{
 Properties {
  [NoScaleOffset] _DayBase("낮 풍경",Cube)="grey"{}
  [NoScaleOffset] _DuskBase("여명·석양 풍경",Cube)="grey"{}
  [NoScaleOffset] _NightBase("밤 풍경",Cube)="black"{}
  [NoScaleOffset] _DayCloud("낮 구름",Cube)="black"{}
  [NoScaleOffset] _DuskCloud("여명·석양 구름",Cube)="black"{}
  [NoScaleOffset] _NightCloud("밤 구름",Cube)="black"{}
  _Rotation("고정 풍경 방위",Float)=0
  _CloudSpeed("구름 공전 속도",Float)=.2
  _CloudOffset("구름 시작 각도",Float)=0
  _CloudOpacity("구름 표시 강도",Range(0,1))=1
  _Weights("낮·석양·밤 비중",Vector)=(1,0,0,0)
  _SunDirection("태양 방향",Vector)=(1,0,0,0)
  _MoonDirection("달 방향",Vector)=(-1,0,0,0)
  _SunColor("태양 HDR 색",Color)=(10,7,3,1)
  _MoonColor("달 HDR 색",Color)=(1.5,1.8,2.3,1)
  _SunRadius("태양 반지름 (도)",Float)=.45
  _MoonRadius("달 반지름 (도)",Float)=.5
  _DayExposure("낮 노출",Float)=1
  _NightExposure("밤 노출",Float)=1
  // JC | 2026-11-17 | 기본 OFF로 탐사 하늘은 유지하고 보스의 복제 재질에서만 초원경 지표를 숨깁니다.
  _JcBossHorizonBlend("보스 원경 지표 숨김",Range(0,1))=0
  _JcBossWaterColor("보스 하부 물색",Color)=(.12,.28,.38,1)
 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   samplerCUBE _DayBase,_DuskBase,_NightBase,_DayCloud,_DuskCloud,_NightCloud;
   float4 _Weights,_SunDirection,_MoonDirection,_SunColor,_MoonColor;
   float _Rotation,_CloudSpeed,_CloudOffset,_CloudOpacity,_JcSkyCloudTime,_SunRadius,_MoonRadius,_DayExposure,_NightExposure;
   // JC | 2026-11-17 | 표시 전용 값이며 조명/반사 프로브를 생성하거나 전역 상태에 쓰지 않습니다.
   float _JcBossHorizonBlend;
   float4 _JcBossWaterColor;
   struct V {float4 p:SV_POSITION;float3 d:TEXCOORD0;};
   V vert(float4 v:POSITION){V o;o.p=UnityObjectToClipPos(v);o.d=v.xyz;return o;}
   float3 rotateY(float3 v,float angle){float sn,cs;sincos(radians(angle),sn,cs);return float3(cs*v.x-sn*v.z,v.y,sn*v.x+cs*v.z);}
   float disk(float3 ray,float3 direction,float radius){float c=dot(ray,normalize(direction));return smoothstep(cos(radians(radius*1.15)),cos(radians(radius*.85)),c);}
   half4 frag(V i):SV_Target {
    float3 ray=normalize(i.d),baseDir=rotateY(ray,-_Rotation);
    // JC | 2026-11-17 | 보스의 낮은 구름을 아래로 내리되 천정 쪽 구름과 탐사 표시 방향은 유지합니다.
    float cloudDrop=_JcBossHorizonBlend*.22*(1-smoothstep(.2,.8,ray.y));
    float3 cloudBase=normalize(float3(baseDir.x,baseDir.y+cloudDrop,baseDir.z));
    float3 cloudDir=rotateY(cloudBase,-fmod(_CloudOffset+_JcSkyCloudTime*_CloudSpeed,360));
    float3 bg=texCUBE(_DayBase,baseDir).rgb*_Weights.x*_DayExposure+texCUBE(_DuskBase,baseDir).rgb*_Weights.y+texCUBE(_NightBase,baseDir).rgb*_Weights.z*_NightExposure;
    float4 cloud=texCUBE(_DayCloud,cloudDir)*_Weights.x+texCUBE(_DuskCloud,cloudDir)*_Weights.y+texCUBE(_NightCloud,cloudDir)*_Weights.z;
    // JC | 2026-11-17 | 하늘 윗부분은 원본 그대로 두고 수평선의 지형 실루엣부터 하부 풍경·파도 묘사까지 부드럽게 덮습니다.
    // 낮은 태양/달은 아래 기존 천체 합성을 유지하며 땅/바다 텍스처를 다시 드러내지 않습니다.
    float conceal=_JcBossHorizonBlend*(1-smoothstep(.08,.18,ray.y));
    if(conceal>0){
     // JC | 2026-11-17 | 조명용 수평선 색 대신 실제 HDR 하늘의 지표 위 색을 읽어 색 띠 없이 연결합니다.
     float3 skyDir=normalize(float3(baseDir.x,.35,baseDir.z));
     float3 skyColor=texCUBE(_DayBase,skyDir).rgb*_Weights.x*_DayExposure+texCUBE(_DuskBase,skyDir).rgb*_Weights.y+texCUBE(_NightBase,skyDir).rgb*_Weights.z*_NightExposure;
     // JC | 2026-11-17 | 하늘색을 수평선 아래까지 연장한 뒤 더 아래에서 물색으로 연결합니다.
     float water=smoothstep(0,.55,-.18-ray.y);
     bg=lerp(bg,lerp(skyColor,_JcBossWaterColor.rgb,water),conceal);
    }
    // JC | 2026-11-17 | 지표를 가리는 경계와 구름의 끝을 분리해 낮아진 구름이 하부 하늘에서도 보이게 합니다.
    cloud*=lerp(1,smoothstep(-.35,-.12,ray.y),_JcBossHorizonBlend);
    float above=smoothstep(0,.025,ray.y);
    float sun=disk(ray,_SunDirection.xyz,_SunRadius);
    float moon=disk(ray,_MoonDirection.xyz,_MoonRadius);
    float halo=pow(saturate(dot(ray,normalize(_SunDirection.xyz))),600)*.04*smoothstep(-.025,.025,_SunDirection.y);
    bg+=above*((sun+halo)*_SunColor.rgb+moon*_MoonColor.rgb);
    return half4(bg*(1-saturate(cloud.a)*_CloudOpacity)+cloud.rgb*_CloudOpacity,1);
   }
   ENDCG
  }
 }
}
