// JC | 2026-11-12 | 비교 씬 전용: 중심이 일치하는 8방향 대칭 버스트와 4점 서브픽셀 샘플링. 별도 텍스처/RT를 만들지 않습니다.
Shader "JC/UI/Loading Symmetric Burst Preview"
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
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o;
            }
            float BurstDistance(float2 p)
            {
                // JC | 2026-11-12 | 22.5도 부채꼴로 접어 같은 직선 경계를 반복합니다. 상하좌우·대각선 끝과 회전축이 정확히 대칭입니다.
                p=abs(p); p=float2(max(p.x,p.y),min(p.x,p.y));
                if(p.y>p.x*0.41421356237) p=float2(p.x+p.y,p.x-p.y)*0.70710678118;
                float2 normal=normalize(float2(0.32*0.38268343236,0.94-0.32*0.92387953251));
                return dot(p-float2(0.94,0),normal);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1, dx=ddx(p), dy=ddy(p);
                float width=max(0.00001,max(length(dx),length(dy)));
                // JC | 2026-11-12 | 4개 서브픽셀과 화면 미분 폭으로 얇은 끝의 회전 깜박임을 완화합니다. 삼각함수·텍스처 읽기는 없습니다.
                float a=0;
                a+=saturate(0.5-BurstDistance(p+(dx+dy)*0.25)/width);
                a+=saturate(0.5-BurstDistance(p+(dx-dy)*0.25)/width);
                a+=saturate(0.5-BurstDistance(p+(-dx+dy)*0.25)/width);
                a+=saturate(0.5-BurstDistance(p-(dx+dy)*0.25)/width);
                fixed4 result=fixed4(i.color.rgb,i.color.a*a*0.25);
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
