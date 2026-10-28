Shader "HeroInfluence/UI/ButtonAmbientLight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Light Color", Color) = (.2,.65,1,1)
        _SpriteRect ("Sprite UV", Vector) = (0,0,1,1)
        _Spread ("Spread", Float) = .08
        _Intensity ("Intensity", Float) = 1.5
        _Aspect ("Aspect", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float4 _SpriteRect, _Color;
            float _Spread, _Intensity, _Aspect;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float alphaAt(float2 uv)
            {
                float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                return tex2D(_MainTex,_SpriteRect.xy+saturate(uv)*_SpriteRect.zw).a*inside;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=(i.uv-.5)*1.6+.5;
                float sum=0, weight=0;
                // Spiral sampling avoids visible repeated silhouettes along a square grid.
                for(int k=0;k<256;k++)
                {
                    float r=sqrt((k+.5)/256.0)*3;
                    float angle=k*2.39996323;
                    float w=exp(-r*r*.5);
                    float2 offset=float2(cos(angle)/_Aspect,sin(angle))*r*_Spread;
                    sum+=alphaAt(uv+offset)*w;
                    weight+=w;
                }
                return fixed4(_Color.rgb*i.color.rgb,saturate(sum/weight*_Intensity)*_Color.a*i.color.a);
            }
            ENDCG
        }
    }
}
