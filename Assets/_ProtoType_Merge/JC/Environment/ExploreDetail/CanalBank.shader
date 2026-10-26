Shader "JC/Environment/Canal Bank"
{
 Properties {
  _BaseColor("콘크리트 색",Color)=(.57,.59,.58,1)
  _PanelLength("패널 간격",Float)=.8
  _JointStrength("줄눈 명암",Float)=.15
  _WetStrength("젖은 띠 명암",Float)=.22
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  // 모든 패스의 재질 레이아웃을 일치시킵니다.
  CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;float _PanelLength;half _JointStrength,_WetStrength;
  CBUFFER_END
  ENDHLSL
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
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 shore:TEXCOORD1;};
   struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float depth:TEXCOORD2;half fog:TEXCOORD3;float4 shadow:TEXCOORD4;};
   V vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.normal=TransformObjectToWorldNormal(i.normalOS);o.depth=i.shore.y;o.fog=ComputeFogFactor(p.positionCS.z);o.shadow=GetShadowCoord(p);return o;}
   half4 frag(V i):SV_Target {
    float2 uv=i.world.xz/max(.1,_PanelLength);float2 grid=abs(frac(uv+.5)-.5);
    float2 aa=max(fwidth(uv),.0005);float2 gridLine=1-smoothstep(.004,.004+aa,grid);
    half joint=max(gridLine.x,gridLine.y);
    half grain=(sin(i.world.x*71+i.world.z*47)*sin(i.world.z*83-i.world.x*31))*.007;
    half wet=smoothstep(.75,.94,i.depth)*_WetStrength;
    half3 c=_BaseColor.rgb*(1-joint*_JointStrength-wet)+grain;
    Light l=GetMainLight(TransformWorldToShadowCoord(i.world));half3 n=normalize(i.normal);
    c*=max(SampleSH(n),half3(.4,.4,.4))+l.color*saturate(dot(n,l.direction))*.55*lerp(.55,1,l.shadowAttenuation);
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
  // 색 화면과 깊이 화면에 같은 호안 형상을 기록합니다.
  // 깊이 사전 렌더에서 누락되면 구름 합성이 뒤쪽 지형을 읽게 됩니다.
  Pass {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ZWrite On Cull Back ColorMask R
   HLSLPROGRAM
   #pragma vertex BankDepthVertex
   #pragma fragment BankDepthFragment
   struct DepthAttributes {float4 positionOS:POSITION;};
   struct DepthVaryings {float4 positionCS:SV_POSITION;};
   DepthVaryings BankDepthVertex(DepthAttributes i) {
    DepthVaryings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);return o;
   }
   half BankDepthFragment(DepthVaryings i):SV_Target {return i.positionCS.z;}
   ENDHLSL
  }
  Pass {
   Name "DepthNormals"
   Tags {"LightMode"="DepthNormals"}
   ZWrite On Cull Back
   HLSLPROGRAM
   #pragma vertex DepthNormalsVertex
   #pragma fragment DepthNormalsFragment
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
   // SSAO의 DepthNormals 경로에서 사용하는 URP 법선 인코딩과 일치시킵니다.
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
   ENDHLSL
  }
 }
}
