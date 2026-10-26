Shader "UliaVfx/VFX/UliaShieldRepel" {
 Properties { _Color("Color",Color)=(.56,.1,1,.8) _Progress("Progress",Float)=0 _Center("Center",Vector)=(0,0,0,0) _Hit("Hit",Vector)=(0,0,0,0) }
 SubShader { Tags {"Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Pass { Blend SrcAlpha One ZWrite Off Cull Back
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;}; struct V {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
 CBUFFER_START(UnityPerMaterial)
 float4 _Color,_Center,_Hit;float _Progress;
 CBUFFER_END
 V vert(A a){
 V o;float3 world=TransformObjectToWorld(a.positionOS.xyz);float3 radial=normalize(world-_Center.xyz);float3 front=normalize(_Hit.xyz-_Center.xyz);float radius=length(_Hit.xyz-_Center.xyz);float p=saturate(_Progress);
 float facing=saturate(dot(radial,front));float local=pow(facing,3.5);
 float dent=-radius*.65*sin(p*15.707963)*exp(-p*2.4)*(1-p)*local;
 float wave=radius*.12*sin(acos(clamp(dot(radial,front),-.999,.999))*9-p*25)*sin(p*3.14159265)*(1-p)*(1-p)*facing;
 world+=radial*(dent+wave);o.world=world;o.pos=TransformWorldToHClip(world);o.normal=TransformObjectToWorldNormal(a.normalOS);return o;
 }
 half4 frag(V i):SV_Target{
 float radius=length(_Hit.xyz-_Center.xyz);float3 n=normalize(i.world-_Center.xyz);float3 front=normalize(_Hit.xyz-_Center.xyz);float d=length(i.world-_Hit.xyz);float p=saturate(_Progress);
 float rim=pow(1-saturate(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),3);
 float gate=smoothstep(.02,.3,dot(n,front));float flash=exp(-d*d/.12)*exp(-p*24);
 float wave=exp(-pow((d-lerp(.02,radius*1.6,p))/.075,2))*(1-p);
 float echo=exp(-pow((d-lerp(0,radius*1.2,p))/.045,2))*pow(1-p,2)*.4;
 float response=(flash*.9+wave*.65+echo)*gate;
 float3 color=lerp(_Color.rgb*(.6+rim*1.5),float3(1.8,1.2,2.1),saturate(response));
 return half4(color,((.025+rim*.7)+response)*_Color.a);
 }
 ENDHLSL
 } } }
