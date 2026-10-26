Shader "UliaVfx/VFX/UliaWormhole"
{
 Properties { _Opacity("Opacity",Float)=1 _Phase("Phase",Float)=0 _Power("Power",Float)=1 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   CBUFFER_START(UnityPerMaterial)
   float _Opacity,_Phase,_Power;
   CBUFFER_END
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
   half4 frag(V i):SV_Target {
    float2 p=(i.uv-.5)*2;float r=length(p);clip(1-r);
    float a=atan2(p.y,p.x);
    float arm=pow(saturate(sin(a*3+r*15-_Phase*8)*.5+.5),6);
    float rim=exp(-pow((r-.86)*22,2));
    float core=exp(-r*r*45);
    float3 col=float3(.008,.002,.025)+float3(.4,.025,.95)*arm*smoothstep(.08,.4,r)*(1-r)*_Power;
    col+=float3(1.4,.2,3.5)*rim*_Power+float3(.4,.04,1)*core*_Power;
    return half4(col,_Opacity*(1-smoothstep(.94,1,r)));
   }
   ENDHLSL
  }
 }
}
