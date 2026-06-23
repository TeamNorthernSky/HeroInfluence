Shader "UI/HoverGlowSweep"
{
    // [JC 260622] 버튼 호버 오버레이 — 외곽선 글로우(알파+휘도엣지) + 스윕(회전 밴드) + 블룸(sprite 색).
    //   글로우: sprite 알파 실루엣 + RGB 휘도 엣지(불투명 타일 아이콘도 외곽선) / 폭은 UV 비율(해상도 독립).
    //   스윕: 기울어진 띠, 좌->우 수평 진행, 각도 위치보간(-30..+45), 3페이즈(두께/투명도 보간).
    //   블룸: 복사된 sprite RGB의 밝은 영역을 블러해 가산(청색 혼합 + soft-knee).
    //   가산(additive) 블렌딩. 아틀라스 미사용 전제.
    //   주의: ShaderLab [Header()]/표시명에 하이픈/괄호 등 특수문자 금지(파스 에러).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Master Tint (Image.color)", Color) = (1,1,1,1)

        [Header(Outline Glow)]
        _GlowColor ("Glow Color", Color) = (0.25, 0.55, 1.0, 1)
        _GlowWidth ("Glow Outer Width (UV frac of height)", Range(0,0.2)) = 0.008
        _GlowInnerWidth ("Glow Inner Width (UV frac of height)", Range(0,0.4)) = 0.025
        _GlowIntensity ("Glow Intensity", Range(0,8)) = 1.0
        _GlowLumEdge ("Glow Luminance Edge", Range(0,3)) = 1.0

        [Header(Sweep gleam texture)]
        [NoScaleOffset] _GleamTex ("Gleam Texture (alpha streak)", 2D) = "black" {}
        _SweepColor ("Sweep Color", Color) = (1.0, 0.93, 0.80, 1)
        _SweepIntensity ("Sweep Intensity", Range(0,8)) = 1.3
        _GleamTilt ("Gleam Tilt deg", Range(-90,90)) = 18
        _GleamScale ("Gleam Travel Scale", Range(0.2,3)) = 1.2
        _GleamWidth ("Gleam UV Width", Range(0.05,2)) = 1.0
        _SweepDelay ("Sweep Start Delay (s)", Range(0,10)) = 0.5
        _SweepDuration ("Sweep Duration (s)", Range(0.05,10)) = 0.4
        _SweepCycle ("Sweep Cycle (s)", Range(0.1,30)) = 1.4
        _SweepTime ("Sweep Time (driven by SweepCooldownReset)", Float) = 0

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

            sampler2D _GleamTex;
            fixed4 _SweepColor;
            float _SweepIntensity;
            float _GleamTilt;
            float _GleamScale;
            float _GleamWidth;
            float _SweepDelay;
            float _SweepDuration;
            float _SweepCycle;
            float _SweepTime;

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
            // 휘도(알파 가중) — 글로우 휘도엣지/블룸 공용
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
                float aspect = _MainTex_TexelSize.y / _MainTex_TexelSize.x; // texW/texH

                // ===== 글로우: 알파 실루엣(외/내측) + 휘도 엣지 =====
                float2 texO = float2(_GlowWidth / aspect, _GlowWidth);
                float2 texI = float2(_GlowInnerWidth / aspect, _GlowInnerWidth);
                float dil = max(RingAvg(uv, texO), RingAvg(uv, texO * 0.5));
                float ero = min(RingMin(uv, texI), RingMin(uv, texI * 0.5));
                float alphaGlow = saturate(dil - baseA) + saturate(baseA - ero);
                float lumC = LumA(uv);
                float lumEdge = max(abs(lumC - LumRing(uv, texO)), abs(lumC - LumRing(uv, texI)));
                float glow = (alphaGlow + lumEdge * _GlowLumEdge) * _GlowIntensity;

                // ===== 스윕: gleam 텍스처 스크롤 + 형상 마스킹 =====
                // 타이밍(_SweepDelay/_SweepDuration/_SweepCycle/_SweepTime)·쿨다운(SweepCooldownReset)은 그대로 재사용.
                // 차이: 절차적 밴드 대신 _GleamTex(알파 광택)를 기울인 UV로 스크롤 샘플 → falloff/두께/곡률/색을 텍스처로 페인팅.
                float t = max(_SweepTime, 0.0);   // C#(SweepCooldownReset)이 호버 시작 0부터 먹여줌
                float phase = fmod(t, _SweepCycle);
                float st = phase - _SweepDelay;   // 호버 후 지연 뒤 스윕 시작
                float active = (st >= 0.0 && st <= _SweepDuration) ? 1.0 : 0.0;
                float u = saturate(st / _SweepDuration); // 0..1 진행
                // 좌->우 스크롤 오프셋. 텍스처 가장자리 알파 0(Clamp)이 진입/이탈 페이드 담당.
                float off = lerp(-0.6, 0.6, u) * _GleamScale;
                float tA = tan(radians(_GleamTilt));
                // 버튼 uv를 기울이고(_GleamTilt) 폭 보정(_GleamWidth) 후 스크롤 → gleam 텍스처 좌표
                float gx = ((uv.x - 0.5) - tA * (uv.y - 0.5)) / _GleamWidth - off + 0.5;
                float gleamA = tex2D(_GleamTex, float2(gx, uv.y)).a; // Clamp wrap = 텍스처 밖 알파 0
                float sweep = gleamA * active * _SweepIntensity * baseA;

                // ===== 글로우+스윕 색 합성(가산) =====
                float gsAmount = glow + sweep;
                float gsDenom = max(glow + sweep, 1e-4);
                float3 gsCol = (_GlowColor.rgb * glow + _SweepColor.rgb * sweep) / gsDenom;
                float3 outRGB = gsCol * gsAmount;

                // ===== 블룸: sprite 밝은 색 블러 + 청색 혼합 + soft-knee =====
                float2 texB = _MainTex_TexelSize.xy * _BloomRadius;
                float3 raw = (BrightAt(uv) * 0.34 + RingBright(uv, texB) * 0.33 + RingBright(uv, texB * 0.5) * 0.33) * _BloomIntensity;
                float bl = max(raw.r, max(raw.g, raw.b));
                float3 bloom = lerp(raw, _BloomColor.rgb * bl, _BloomTint);
                bloom = bloom / (1.0 + bloom * _BloomSoftKnee);
                outRGB += bloom;

                float master = i.color.a;
                outRGB *= i.color.rgb * master;
                float a = saturate(max(gsAmount, max(bloom.r, max(bloom.g, bloom.b))) * master);
                return fixed4(outRGB, a);
            }
            ENDCG
        }
    }
}
