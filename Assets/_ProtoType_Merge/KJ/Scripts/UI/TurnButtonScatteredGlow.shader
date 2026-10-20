Shader "HeroInfluence/UI/ScatteredBacklight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Light Color", Color) = (0.18, 0.6, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.6
        _EffectTime ("Time", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha One
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float4 local:TEXCOORD1; };
            fixed4 _Color;
            float _Intensity, _EffectTime;
            float4 _ClipRect;
            Output vert(Input v)
            {
                Output o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            fixed4 frag(Output i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                float r=length(p);
                float a=atan2(p.y,p.x);
                float t=_EffectTime;
                // Integer angular frequencies keep the ray field seamless at -pi/pi.
                float rays=pow(saturate(.5+.5*sin(a*19+sin(a*7)*1.8+t*.35)),8);
                rays+=.45*pow(saturate(.5+.5*sin(a*31-sin(a*5)+t*.21)),12);
                float outward=.65+.35*sin(r*27-t*3+a*4);
                float fade=1-smoothstep(.42,1,r);
                float halo=exp(-r*r*4)*.3;
                float light=(halo+rays*outward*.95)*fade;
                light*=.9+.1*sin(t*1.7);
                fixed4 color=fixed4(i.color.rgb,saturate(light*_Intensity)*i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
