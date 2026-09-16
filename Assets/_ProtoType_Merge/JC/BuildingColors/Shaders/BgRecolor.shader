Shader "Hidden/JC/BuildingRecolor"
{
 Properties{_MainTex("Source",2D)="white"{} _Parts("Parts",2D)="black"{} _Floor("Floor",2D)="black"{} _Extra("Extra",2D)="black"{} _Extra2("Extra2",2D)="black"{}}
 SubShader { Tags {"RenderType"="Opaque"} Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex,_Parts,_Floor,_Extra,_Extra2;float4 _Changes[16],_FlatColors[16];float _Highlight;int _PartCount;
 float3 toLab(float3 c){float3 lms=mul(float3x3(.4122214708,.5363325363,.0514459929,.2119034982,.6806995451,.1073969566,.0883024619,.2817188376,.6299787005),c);lms=pow(max(lms,0),1.0/3.0);return mul(float3x3(.2104542553,.793617785,-.0040720468,1.9779984951,-2.428592205,.4505937099,.0259040371,.7827717662,-.808675766),lms);}
 float3 toRgb(float3 v){float3 lms=mul(float3x3(1,.3963377774,.2158037573,1,-.1055613458,-.0638541728,1,-.0894841775,-1.291485548),v);lms=lms*lms*lms;return saturate(mul(float3x3(4.0767416621,-3.3077115913,.2309699292,-1.2684380046,2.6097574011,-.3413193965,-.0041960863,-.7034186147,1.707614701),lms));}
 float4 frag(v2f_img i):SV_Target{
  float4 src=tex2D(_MainTex,i.uv);float3 linearColor=src.rgb;
  #ifdef UNITY_COLORSPACE_GAMMA
  linearColor=GammaToLinearSpace(linearColor);
  #endif
  float4 m=tex2D(_Parts,i.uv);float4 e=tex2D(_Extra,i.uv),f=tex2D(_Extra2,i.uv);float weights[16]={m.r,m.g,m.b,m.a,tex2D(_Floor,i.uv).r,e.r,e.g,e.b,e.a,f.r,f.g,f.b,f.a,0,0,0};
  float sum=0;for(int j=0;j<_PartCount;j++)sum+=weights[j];float norm=max(1,sum);
  if(_Highlight>=0&&_Highlight<_PartCount){float weight=weights[(int)_Highlight]/norm;float3 view=lerp(linearColor*.3,float3(1,.03,.5),weight);return float4(view,src.a);}
  float3 lab=toLab(linearColor);float chroma=length(lab.yz);float hue=atan2(lab.z,lab.y);float3 edited=lab;
  for(int k=0;k<_PartCount;k++){float3 change=_Changes[k].xyz;float ratio=_Changes[k].w;float c=ratio>=0?chroma*ratio:max(0,chroma+change.y);float h=hue+change.z;float3 target=_FlatColors[k].w>.5?_FlatColors[k].xyz:float3(lab.x+change.x,c*cos(h),c*sin(h));edited+=(target-lab)*weights[k]/norm;}
  float3 result=toRgb(edited);
  #ifdef UNITY_COLORSPACE_GAMMA
  result=LinearToGammaSpace(result);
  #endif
  return float4(result,src.a);
 }
 ENDCG }
 }
}

