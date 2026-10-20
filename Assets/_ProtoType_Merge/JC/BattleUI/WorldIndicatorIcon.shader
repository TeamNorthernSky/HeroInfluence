Shader "JC/Battle/WorldIndicatorIcon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Output { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            Output vert(Input v)
            {
                Output o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.uv = v.uv; return o;
            }
            fixed4 frag(Output i) : SV_Target
            {
                fixed4 face = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                float2 pixel = fwidth(i.uv);
                float2 d = pixel * 1.25;
                half edge = 0;
                edge = max(edge, tex2D(_MainTex, i.uv + float2(d.x,0)).a);
                edge = max(edge, tex2D(_MainTex, i.uv - float2(d.x,0)).a);
                edge = max(edge, tex2D(_MainTex, i.uv + float2(0,d.y)).a);
                edge = max(edge, tex2D(_MainTex, i.uv - float2(0,d.y)).a);
                edge = max(edge, tex2D(_MainTex, i.uv + d).a);
                edge = max(edge, tex2D(_MainTex, i.uv - d).a);
                edge = max(edge, tex2D(_MainTex, i.uv + float2(d.x,-d.y)).a);
                edge = max(edge, tex2D(_MainTex, i.uv + float2(-d.x,d.y)).a);
                half a = edge * i.color.a;
                half shadow = tex2D(_MainTex, i.uv + pixel * float2(-2,2)).a * i.color.a * .65;
                half3 rgb = .2 * shadow * (1-a) + a;
                half alpha = a + shadow * (1-a);
                rgb = face.rgb * face.a + rgb * (1-face.a);
                alpha = face.a + alpha * (1-face.a);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
