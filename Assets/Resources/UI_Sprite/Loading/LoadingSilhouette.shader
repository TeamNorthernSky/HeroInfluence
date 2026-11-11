Shader "UI/LoadingSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Shape ("Shape", Float) = 0
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
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
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex;
            float _Shape;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float a;
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                if (_Shape < .5)
                {
                    // The approved monochrome sheet supplies the silhouette; black is transparent at render time.
                    fixed4 source = tex2D(_MainTex, i.uv);
                    a = smoothstep(.08, .85, source.r) * source.a;
                }
                else if (_Shape < 1.5) a = 1 - smoothstep(.97, .99, r);
                else if (_Shape < 2.5)
                {
                    float edge = abs(r - .9);
                    a = saturate((1 - smoothstep(.006, .025, edge)) + .23 * (1 - smoothstep(.025, .09, edge)));
                }
                else
                {
                    // A compact four-point muzzle flame, with a softer outer edge.
                    float d = abs(p.x) + abs(p.y);
                    a = 1 - smoothstep(.78, 1, d);
                }
                fixed4 result = fixed4(i.color.rgb, i.color.a * a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - .001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
