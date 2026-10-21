Shader "JC/Environment/Layered Cloud Sky"
{
 Properties {
  [NoScaleOffset] _Tex("고정 하늘 · 태양 · 풍경", Cube)="grey" {}
  [NoScaleOffset] _CloudTex("구름 HDR · 투과 알파", Cube)="black" {}
  _Tint("색조", Color)=(.5,.5,.5,1)
  _Exposure("노출", Range(0,8))=1
  _Rotation("하늘 기준 회전", Range(0,360))=0
  _CloudSpeed("구름 공전 속도 (도/초)", Float)=.2
  _CloudOffset("구름 시작 각도", Float)=0
  _CloudOpacity("구름 표시 강도", Range(0,1))=1
 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   samplerCUBE _Tex,_CloudTex;
   half4 _Tex_HDR,_Tint;
   float _Exposure,_Rotation,_CloudSpeed,_CloudOffset,_CloudOpacity;
   float _JcSkyCloudTime;
   struct A {float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 position:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.position=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;}
   float3 Rotated(float3 v,float angle){float sn,cs;sincos(radians(angle),sn,cs);return float3(cs*v.x-sn*v.z,v.y,sn*v.x+cs*v.z);}
   half4 frag(V i):SV_Target {
    float3 baseDir=Rotated(i.direction,-_Rotation);
    half3 background=DecodeHDR(texCUBE(_Tex,baseDir),_Tex_HDR);
    float orbit=fmod(_CloudOffset+_JcSkyCloudTime*_CloudSpeed,360);
    // 태양·지형은 baseDir만 사용하고 구름 샘플 방향만 회전합니다.
    float4 cloud=texCUBE(_CloudTex,Rotated(baseDir,-orbit));
    // EXR 구름은 premultiplied radiance와 볼륨 불투명도를 함께 저장합니다.
    half3 color=background*(1-saturate(cloud.a)*_CloudOpacity)+cloud.rgb*_CloudOpacity;
    return half4(color*_Tint.rgb*unity_ColorSpaceDouble.rgb*_Exposure,1);
   }
   ENDCG
  }
 }
}
