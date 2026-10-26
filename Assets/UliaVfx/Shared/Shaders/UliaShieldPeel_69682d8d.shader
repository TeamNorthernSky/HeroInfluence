Shader "UliaVfx/VFX/UliaShieldPeel"
{
 Properties {
  _Color("Color",Color)=(.56,.1,1,.8)
  _Progress("Peel progress",Range(0,1))=0
  _Center("Center",Vector)=(0,0,0,0)
  _Front("Impact direction",Vector)=(0,0,-1,0)
  _Fill("Shell opacity",Float)=.11
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass {
   Blend SrcAlpha One
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float4 pos:POSITION; float3 normal:NORMAL; };
   struct V { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float3 original:TEXCOORD2; };
   CBUFFER_START(UnityPerMaterial)
   float4 _Color, _Center, _Front;
   float _Progress, _Fill;
   CBUFFER_END
   V vert(A i) {
    V o;
    float3 w=TransformObjectToWorld(i.pos.xyz);
    o.original=w;
    float3 radial=normalize(w-_Center.xyz);
    float band=1-saturate(abs(dot(radial,_Front.xyz)-(1-2*_Progress))/.28);
    float curl=band*band*.19*step(.001,_Progress)*step(_Progress,.999);
    w+=radial*curl-_Front.xyz*curl*.4;
    o.world=w; o.pos=TransformWorldToHClip(w);
    o.normal=TransformObjectToWorldNormal(i.normal);
    return o;
   }
   half4 frag(V i):SV_Target {
    float3 n=normalize(i.original-_Center.xyz);
    float ripple=.035*sin(n.y*27+n.x*13)+.024*sin(n.z*38-n.y*17);
    float remain=(1-2.12*_Progress)-dot(n,_Front.xyz)+ripple;
    if(_Progress>.001) clip(remain);
    float edge=(_Progress>.001)?1-smoothstep(0,.13,remain):0;
    float rim=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),3);
    float a=(_Fill+rim*.55+edge*.7)*_Color.a;
    float3 color=_Color.rgb*(.6+rim*1.5)+float3(2.5,1.2,3.5)*edge;
    return half4(color,a);
   }
   ENDHLSL
  }
 }
}
