Shader "JC/Environment/Time Sky" {
 Properties { _Top("상부",Color)=(.3,.5,.8,1) _Horizon("수평선",Color)=(.7,.8,.9,1) _Bottom("하부",Color)=(.2,.25,.2,1) }
 SubShader {Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Top,_Horizon,_Bottom;
 struct V {float4 p:SV_POSITION;float3 dir:TEXCOORD0;};
 V vert(float4 p:POSITION){V o;o.p=UnityObjectToClipPos(p);o.dir=p.xyz;return o;}
 half4 frag(V i):SV_Target {float h=normalize(i.dir).y;return h>=0?lerp(_Horizon,_Top,sqrt(h)):lerp(_Horizon,_Bottom,sqrt(-h));}
 ENDHLSL}
 }}
