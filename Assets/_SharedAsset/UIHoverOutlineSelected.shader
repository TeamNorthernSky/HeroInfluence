Shader "UI/HoverOutlineSelected"
{
    // [JC 260622] UIHoverGlowSweep의 "선택 표시용" 변형: 스윕 제거 + 외곽선 글로우 강조 + 블룸 유지.
    //   현재 선택된 스킬 버튼에 상시 표시(호버 토글 아님). 글로우=알파 실루엣+RGB 휘도엣지.
    //   주의: ShaderLab [Header()]/표시명에 하이픈/괄호 등 특수문자 금지.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Master Tint (Image.color)", Color) = (1,1,1,1)

        [Header(Outline Glow emphasized)]
        _GlowColor ("Glow Color", Color) = (0.10, 0.45, 1.0, 1)
        _GlowWidth ("Glow Outer Width (UV frac of height)", Range(0,0.2)) = 0.012
        _GlowInnerWidth ("Glow Inner Width (UV frac of height)", Range(0,0.4)) = 0.045
        _GlowIntensity ("Glow Intensity", Range(0,8)) = 2.6
        _GlowLumEdge ("Glow Luminance Edge", Range(0,3)) = 1.4

        [Header(Bloom on copied SPRITE color)]
        _BloomColor ("Bloom Tint Color", Color) = (0.439, 0.671, 0.918, 1)
        _BloomTint ("Bloom Tint Mix", Range(0,1)) = 0.85
        _BloomThreshold ("Bloom Luma Threshold", Range(0,1)) = 0.5
        _BloomRadius ("Bloom Radius (texels)", Range(2,80)) = 14
        _BloomIntensity ("Bloom Intensity", Range(0,12)) = 2.2
        _BloomSoftKnee ("Bloom Soft Knee", Range(0,4)) = 2.2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
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
        Blend One One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _GlowWidth;
            float _GlowInnerWidth;
            float _GlowIntensity;
            float _GlowLumEdge;
            fixed4 _BloomColor;
            float _BloomTint;
            float _BloomThreshold;
            float _BloomRadius;
            float _BloomIntensity;
            float _BloomSoftKnee;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float RingAvg(float2 uv, float2 texel)
            {
                float a = 0;
                a += tex2D(_MainTex, uv + float2( texel.x,  0)).a;
                a += tex2D(_MainTex, uv + float2(-texel.x,  0)).a;
                a += tex2D(_MainTex, uv + float2( 0,  texel.y)).a;
                a += tex2D(_MainTex, uv + float2( 0, -texel.y)).a;
                a += tex2D(_MainTex, uv + float2( texel.x,  texel.y)).a;
                a += tex2D(_MainTex, uv + float2( texel.x, -texel.y)).a;
                a += tex2D(_MainTex, uv + float2(-texel.x,  texel.y)).a;
                a += tex2D(_MainTex, uv + float2(-texel.x, -texel.y)).a;
                return a * 0.125;
            }
            float RingMin(float2 uv, float2 texel)
            {
                float a = 1;
                a = min(a, tex2D(_MainTex, uv + float2( texel.x,  0)).a);
                a = min(a, tex2D(_MainTex, uv + float2(-texel.x,  0)).a);
                a = min(a, tex2D(_MainTex, uv + float2( 0,  texel.y)).a);
                a = min(a, tex2D(_MainTex, uv + float2( 0, -texel.y)).a);
                a = min(a, tex2D(_MainTex, uv + float2( texel.x,  texel.y)).a);
                a = min(a, tex2D(_MainTex, uv + float2( texel.x, -texel.y)).a);
                a = min(a, tex2D(_MainTex, uv + float2(-texel.x,  texel.y)).a);
                a = min(a, tex2D(_MainTex, uv + float2(-texel.x, -texel.y)).a);
                return a;
            }
            float LumA(float2 uv)
            {
                float4 c = tex2D(_MainTex, uv);
                return max(c.r, max(c.g, c.b)) * c.a;
            }
            float LumRing(float2 uv, float2 texel)
            {
                float s = 0;
                s += LumA(uv + float2( texel.x,  0));
                s += LumA(uv + float2(-texel.x,  0));
                s += LumA(uv + float2( 0,  texel.y));
                s += LumA(uv + float2( 0, -texel.y));
                s += LumA(uv + float2( texel.x,  texel.y));
                s += LumA(uv + float2( texel.x, -texel.y));
                s += LumA(uv + float2(-texel.x,  texel.y));
                s += LumA(uv + float2(-texel.x, -texel.y));
                return s * 0.125;
            }
            float3 BrightAt(float2 uv)
            {
                float4 c = tex2D(_MainTex, uv);
                float lum = max(c.r, max(c.g, c.b)) * c.a;
                return c.rgb * max(lum - _BloomThreshold, 0.0);
            }
            float3 RingBright(float2 uv, float2 texel)
            {
                float3 s = 0;
                s += BrightAt(uv + float2( texel.x,  0));
                s += BrightAt(uv + float2(-texel.x,  0));
                s += BrightAt(uv + float2( 0,  texel.y));
                s += BrightAt(uv + float2( 0, -texel.y));
                s += BrightAt(uv + float2( texel.x,  texel.y));
                s += BrightAt(uv + float2( texel.x, -texel.y));
                s += BrightAt(uv + float2(-texel.x,  texel.y));
                s += BrightAt(uv + float2(-texel.x, -texel.y));
                return s * 0.125;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float baseA = tex2D(_MainTex, uv).a;
                float aspect = _MainTex_TexelSize.y / _MainTex_TexelSize.x;

                // 외곽선 글로우(알파 실루엣 + 휘도 엣지) — 강조
                float2 texO = float2(_GlowWidth / aspect, _GlowWidth);
                float2 texI = float2(_GlowInnerWidth / aspect, _GlowInnerWidth);
                float dil = max(RingAvg(uv, texO), RingAvg(uv, texO * 0.5));
                float ero = min(RingMin(uv, texI), RingMin(uv, texI * 0.5));
                float alphaGlow = saturate(dil - baseA) + saturate(baseA - ero);
                float lumC = LumA(uv);
                float lumEdge = max(abs(lumC - LumRing(uv, texO)), abs(lumC - LumRing(uv, texI)));
                float glow = (alphaGlow + lumEdge * _GlowLumEdge) * _GlowIntensity;

                float3 outRGB = _GlowColor.rgb * glow;

                // 블룸(sprite 밝은 색) — 유지
                float2 texB = _MainTex_TexelSize.xy * _BloomRadius;
                float3 raw = (BrightAt(uv) * 0.34 + RingBright(uv, texB) * 0.33 + RingBright(uv, texB * 0.5) * 0.33) * _BloomIntensity;
                float bl = max(raw.r, max(raw.g, raw.b));
                float3 bloom = lerp(raw, _BloomColor.rgb * bl, _BloomTint);
                bloom = bloom / (1.0 + bloom * _BloomSoftKnee);
                outRGB += bloom;

                float master = i.color.a;
                outRGB *= i.color.rgb * master;
                float a = saturate(max(glow, max(bloom.r, max(bloom.g, bloom.b))) * master);
                return fixed4(outRGB, a);
            }
            ENDCG
        }
    }
}
