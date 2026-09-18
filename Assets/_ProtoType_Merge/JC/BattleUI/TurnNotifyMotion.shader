Shader "JC/UI/TurnNotifyMotion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Enum(Blur,0,Streak,1)] _Mode ("Effect", Float) = 0
        _Padding ("Horizontal padding fraction", Range(0,.49)) = 0
        _BlurLength ("Directional blur length", Range(0,1)) = .1
        _Strength ("Blur strength", Range(0,2)) = .45
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float4 local:TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect;
            float _Mode, _Padding, _BlurLength, _Strength;
            v2f vert(appdata v)
            {
                v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 result;
                if (_Mode > .5)
                {
                    float y=abs(i.uv.y-.5)*2;
                    float shape=exp(-y*y*7)*smoothstep(0,.22,i.uv.x)*(1-smoothstep(.82,1,i.uv.x));
                    result=fixed4(i.color.rgb,shape*i.color.a);
                }
                else
                {
                    float2 uv=float2((i.uv.x-_Padding)/max(.02,1-2*_Padding),i.uv.y);
                    float4 sum=0;
                    [unroll] for(int k=0;k<12;k++)
                    {
                        float t=(k+.5)/12.0;
                        float2 sampleUV=uv+float2(_BlurLength*t,0);
                        float inside=step(0,sampleUV.x)*step(sampleUV.x,1)*step(0,sampleUV.y)*step(sampleUV.y,1);
                        float4 c=(tex2D(_MainTex,sampleUV)+_TextureSampleAdd);
                        c.a*=inside*(1-t*.7);
                        sum+=float4(c.rgb*c.a,c.a);
                    }
                    result=fixed4(sum.rgb/max(.0001,sum.a),saturate(sum.a/12*_Strength));
                    result*=i.color;
                }
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
