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
   struct V {float4 p:SV_POSITION;float3 d:TEXCOORD0;};
   V vert(float4 v:POSITION){V o;o.p=UnityObjectToClipPos(v);o.d=v.xyz;return o;}
   float3 rotateY(float3 v,float angle){float sn,cs;sincos(radians(angle),sn,cs);return float3(cs*v.x-sn*v.z,v.y,sn*v.x+cs*v.z);}
   float disk(float3 ray,float3 direction,float radius){float c=dot(ray,normalize(direction));return smoothstep(cos(radians(radius*1.15)),cos(radians(radius*.85)),c);}
   half4 frag(V i):SV_Target {
    float3 ray=normalize(i.d),baseDir=rotateY(ray,-_Rotation),cloudDir=rotateY(baseDir,-fmod(_CloudOffset+_JcSkyCloudTime*_CloudSpeed,360));
    float3 bg=texCUBE(_DayBase,baseDir).rgb*_Weights.x*_DayExposure+texCUBE(_DuskBase,baseDir).rgb*_Weights.y+texCUBE(_NightBase,baseDir).rgb*_Weights.z*_NightExposure;
    float4 cloud=texCUBE(_DayCloud,cloudDir)*_Weights.x+texCUBE(_DuskCloud,cloudDir)*_Weights.y+texCUBE(_NightCloud,cloudDir)*_Weights.z;
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
