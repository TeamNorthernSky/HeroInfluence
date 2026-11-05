// JC | 2026-11-04 | 버튼 면과 강조 효과에 같은 종횡비와 윤곽을 적용해 확대하거나 선택해도 테두리가 어긋나지 않게 합니다.
Shader "UI/JC/SystemButtonFit"
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
 // Authored button ratio is 3.10. Keep the lettering uniformly scaled while
 // adding the requested extra height to the blank space near the edges.
 fixed4 frag(v2f i):SV_Target {
 float y = (i.uv.y - .5) * (87.75 / (160.0 / 3.10 * 1.35));
 float ay = abs(y);
 if (ay > .5) return 0;
 float sourceY = ay <= .25 ? ay * 1.125 : lerp(.28125, .5, (ay - .25) * 4);
 // JC | 2026-11-04 | 상하 여백을 원본 비율로 환산해 버튼의 글자와 아이콘이 세로로 늘어나지 않게 합니다.
 float2 uv = float2(i.uv.x, .5 + sign(y) * sourceY);
 fixed4 c = (tex2D(_MainTex, uv) + _TextureSampleAdd) * i.color;
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
