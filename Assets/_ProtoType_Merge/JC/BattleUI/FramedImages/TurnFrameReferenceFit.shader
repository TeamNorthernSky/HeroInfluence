Shader "UI/JC/TurnFrameReferenceFit"
{
 Properties {
 [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
 _Color ("Tint", Color) = (1,1,1,1)
 _StencilComp ("Stencil Comparison", Float) = 8
 _Stencil ("Stencil ID", Float) = 0
 _StencilOp ("Stencil Operation", Float) = 0
 _StencilWriteMask ("Stencil Write Mask", Float) = 255
 _StencilReadMask ("Stencil Read Mask", Float) = 255
 _ColorMask ("Color Mask", Float) = 15
 [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
 }
 SubShader {
 Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
 Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
 Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
 #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
 #include "UnityCG.cginc"
 #include "UnityUI.cginc"
 struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
 sampler2D _MainTex; fixed4 _Color; fixed4 _TextureSampleAdd; float4 _ClipRect;
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
 // User-approved 375x1069 layout. Recess regions use uniform scale; only blue gaps interpolate.
 float sourceY(float y){
 const float k=0.56741573;
 if(y<10)return y;
 if(y<99)return lerp(10,330-97/k,(y-10)/89);
 if(y<293)return 330+(y-196)/k;
 if(y<311)return lerp(330+97/k,540,(y-293)/18);
 if(y<325)return lerp(540,695-80/k,(y-311)/14);
 if(y<485)return 695+(y-405)/k;
 if(y<504)return lerp(695+80/k,998-80/k,(y-485)/19);
 if(y<664)return 998+(y-584)/k;
 if(y<682)return lerp(998+80/k,1300-80/k,(y-664)/18);
 if(y<842)return 1300+(y-762)/k;
 if(y<858)return lerp(1300+80/k,1603-80/k,(y-842)/16);
 if(y<1018)return 1603+(y-938)/k;
 if(y<1060)return lerp(1603+80/k,1800,(y-1018)/42);
 return lerp(1800,1811,(y-1060)/9);
 }
 fixed4 frag(v2f i):SV_Target {
 float2 p=float2(i.uv.x*375,(1-i.uv.y)*1069);
 float2 uv=float2(((p.x-207)/0.56741573+433)/868,1-sourceY(p.y)/1811);
 fixed4 c=(tex2D(_MainTex,uv)+_TextureSampleAdd)*i.color;
 // Suppress isolated low-alpha fringe pixels without hardening the antialiased silhouette.
 c.a=smoothstep(0.10,0.85,c.a);
 if(any(uv<0)||any(uv>1))c.a=0;
 #ifdef UNITY_UI_CLIP_RECT
 c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
 #endif
 #ifdef UNITY_UI_ALPHACLIP
 clip(c.a-0.001);
 #endif
 return c;
 }
 ENDCG }
 }
}
