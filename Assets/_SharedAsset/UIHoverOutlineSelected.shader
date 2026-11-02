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

        [Header(Selected Orbit and Inner Sweep)]
        _SelectedMotion ("Animated Selection 0 Original", Float) = 0
        _SelectedTime ("Selection Time", Float) = 0
        _OrbitPeriod ("Orbit Period Seconds", Float) = 2.4
        _OrbitTrail ("Orbit Trail Length", Range(0.05,0.95)) = 0.55
        _OrbitWhiteRim ("Orbit White Rim Intensity", Range(0,3)) = 0
        _OrbitGlowSpread ("Orbit Glow Spread", Range(1,4)) = 1
        [HideInInspector] _ConfigurableOrbitRim ("Use Configurable Battle Rim", Float) = 0
        [HideInInspector] _BattleRimRect ("Battle Rim Local Rect", Vector) = (0,0,320,88)
        [HideInInspector] _OrbitUVPerPixel ("Orbit UV Per Screen Pixel", Vector) = (0.00556,0.01351,0,0)
        _SelectedSweepPeriod ("Inner Sweep Period Seconds", Float) = 2
        _SelectedSweepWidth ("Inner Sweep Soft Width", Range(0.02,0.4)) = 0.14
        _SelectedSweepIntensity ("Inner Sweep Intensity", Range(0,3)) = 0.45
        _SelectedSweepColor ("Inner Sweep Color", Color) = (0.75,0.9,1,1)

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
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
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
        Blend One [_DstBlend]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; float4 rimUV : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 rimUV : TEXCOORD1; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _GlowColor;
            float _GlowWidth;
            float _GlowInnerWidth;
            float _GlowIntensity;
            float _GlowLumEdge;
            float _SelectedMotion, _SelectedTime, _OrbitPeriod, _OrbitTrail;
            float _OrbitWhiteRim, _OrbitGlowSpread;
            float _ConfigurableOrbitRim;
            float4 _BattleRimRect;
            float4 _OrbitUVPerPixel;
            float _SelectedSweepPeriod, _SelectedSweepWidth, _SelectedSweepIntensity;
            fixed4 _SelectedSweepColor;
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
                o.rimUV = v.rimUV;
                o.texcoord = v.texcoord;
                if (_ConfigurableOrbitRim < 0.5 && _SelectedMotion > 0.5 && _OrbitWhiteRim > 0.0)
                {
                    // 원본 버튼 크기는 유지하고 효과 메시만 화면 기준 2픽셀 확장합니다.
                    float2 direction = sign(v.texcoord - 0.5);
                    o.vertex.xy += direction * 4.0 / _ScreenParams.xy * o.vertex.w;
                    o.texcoord += direction * 2.0 * _OrbitUVPerPixel.xy;
                }
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
            float OrbitAlpha(float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2D(_MainTex, saturate(uv)).a * inside;
            }
            float OrbitDilatedAlpha(float2 uv, float2 radius)
            {
                float a = OrbitAlpha(uv);
                a = max(a, OrbitAlpha(uv + float2(radius.x, 0)));
                a = max(a, OrbitAlpha(uv - float2(radius.x, 0)));
                a = max(a, OrbitAlpha(uv + float2(0, radius.y)));
                a = max(a, OrbitAlpha(uv - float2(0, radius.y)));
                a = max(a, OrbitAlpha(uv + radius * 0.7071));
                a = max(a, OrbitAlpha(uv - radius * 0.7071));
                a = max(a, OrbitAlpha(uv + float2(radius.x, -radius.y) * 0.7071));
                a = max(a, OrbitAlpha(uv + float2(-radius.x, radius.y) * 0.7071));
                return a;
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
            float OrbitErodedAlpha(float2 uv, float2 radius)
            {
                // Clamp 샘플링은 직선 변 밖에서도 불투명 픽셀을 반복하므로
                // 안쪽 경계도 바깥을 투명하게 읽어야 네 변의 띠 두께가 유지됩니다.
                float a = OrbitAlpha(uv);
                a = min(a, OrbitAlpha(uv + float2(radius.x, 0)));
                a = min(a, OrbitAlpha(uv - float2(radius.x, 0)));
                a = min(a, OrbitAlpha(uv + float2(0, radius.y)));
                a = min(a, OrbitAlpha(uv - float2(0, radius.y)));
                a = min(a, OrbitAlpha(uv + radius * 0.7071));
                a = min(a, OrbitAlpha(uv - radius * 0.7071));
                a = min(a, OrbitAlpha(uv + float2(radius.x, -radius.y) * 0.7071));
                a = min(a, OrbitAlpha(uv + float2(-radius.x, radius.y) * 0.7071));
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

            float4 BattleRim(v2f i)
            {
                // UV1 is written before batching; unlike POSITION it stays image-relative
                // in Overlay, Camera and World Space canvases, including nine-sliced Images.
                float2 uv = i.rimUV.xy;
                float aspect = max(i.rimUV.z, 0.001);
                float2 p = (uv - 0.5) * float2(aspect, 1.0);
                float radius = min(0.08, min(aspect, 1.0) * 0.5);
                float2 q = abs(p) - (float2(aspect, 1.0) * 0.5 - radius);
                float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                float aa = max(fwidth(distance), 0.0001);
                float width = max(_GlowWidth, 0.0);
                float ring = (1.0 - smoothstep(-aa, aa, distance))
                    * smoothstep(-width - aa, -width + aa, distance) * step(0.00001, width);
                float2 edge = (uv * 2.0 - 1.0);
                float2 projected = edge / max(max(abs(edge.x), abs(edge.y)), 0.0001);
                float perimeter;
                if (abs(edge.y) >= abs(edge.x))
                    perimeter = projected.y >= 0 ? (projected.x + 1) * aspect : 2 * aspect + 2 + (1 - projected.x) * aspect;
                else
                    perimeter = projected.x >= 0 ? 2 * aspect + (1 - projected.y) : 4 * aspect + 2 + (projected.y + 1);
                perimeter /= 4 * (aspect + 1);
                float age = frac((_SelectedTime / max(_OrbitPeriod, 0.1) - perimeter) * 2);
                float head = exp(-pow(min(age, 1-age) / 0.035, 2));
                float tail = 1 - smoothstep(max(_OrbitTrail, 0.05) * 0.35, max(_OrbitTrail, 0.05), age);
                float coverage = ring * (_SelectedMotion > 0.5 ? lerp(0.04, 1.0, tail) : 1.0);
                float3 color = lerp(_GlowColor.rgb, float3(1,1,1), head * saturate(_OrbitWhiteRim) * 0.2);
                float intensity = max(_GlowIntensity, 0);
                float travel = lerp(-0.6, 1.6, frac(_SelectedTime / max(_SelectedSweepPeriod, 0.1)));
                float sweep = exp(-pow((uv.x - (uv.y - 0.5)*0.28 - travel) / max(_SelectedSweepWidth, 0.02), 2));
                float interior = (1 - ring) * (1 - smoothstep(-aa, aa, distance)) * sweep * _SelectedSweepIntensity;
                float3 rgb = color * coverage * intensity + _SelectedSweepColor.rgb * interior;
                return float4(rgb * i.color.rgb * i.color.a, saturate(coverage * intensity + interior) * i.color.a);
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_ConfigurableOrbitRim > 0.5) return BattleRim(i);
                float2 uv = i.texcoord;
                float baseA = tex2D(_MainTex, uv).a;
                if (_SelectedMotion > 0.5 && _OrbitWhiteRim > 0.0) baseA = OrbitAlpha(uv);
                float aspect = _MainTex_TexelSize.y / _MainTex_TexelSize.x;

                // 외곽선 글로우(알파 실루엣 + 휘도 엣지) — 강조
                float2 texO = float2(_GlowWidth / aspect, _GlowWidth);
                float2 texI = float2(_GlowInnerWidth / aspect, _GlowInnerWidth);
                float dil = max(RingAvg(uv, texO), RingAvg(uv, texO * 0.5));
                float ero = min(RingMin(uv, texI), RingMin(uv, texI * 0.5));
                if (_SelectedMotion > 0.5 && _OrbitWhiteRim > 0.0) ero = OrbitErodedAlpha(uv, texI);
                float alphaGlow = saturate(dil - baseA) + saturate(baseA - ero);
                float lumC = LumA(uv);
                float lumEdge = max(abs(lumC - LumRing(uv, texO)), abs(lumC - LumRing(uv, texI)));
                float glow = (alphaGlow + lumEdge * _GlowLumEdge) * _GlowIntensity;

                if (_SelectedMotion > 0.5)
                {
                    // 중심에서 사각 외곽으로 투영하고 변 길이로 정규화한 시계 방향 좌표입니다.
                    float2 p = uv * 2.0 - 1.0;
                    float2 q = p / max(max(abs(p.x), abs(p.y)), 1e-4);
                    float perimeter;
                    if (abs(p.y) >= abs(p.x))
                        perimeter = q.y >= 0.0 ? (q.x + 1.0) * aspect : 2.0 * aspect + 2.0 + (1.0 - q.x) * aspect;
                    else
                        perimeter = q.x >= 0.0 ? 2.0 * aspect + (1.0 - q.y) : 4.0 * aspect + 2.0 + (q.y + 1.0);
                    perimeter /= 4.0 * (aspect + 1.0);
                    // 마주 보는 두 빛이 밝은 머리와 부드러운 꼬리를 남기며 외곽만 순환합니다.
                    float age = frac((_SelectedTime / max(_OrbitPeriod, 0.1) - perimeter) * 2.0);
                    float headDistance = min(age, 1.0 - age);
                    float head = exp(-pow(headDistance / 0.035, 2.0));
                    float trail = (1.0 - smoothstep(0.0, max(_OrbitTrail, 0.05), age)) * smoothstep(0.0, 0.035, age);
                    float movingRim = alphaGlow * _GlowIntensity * (0.04 + max(head, trail * 0.8));
                    float3 rimColor = lerp(_GlowColor.rgb, float3(1,1,1), head * 0.65);
                    float3 rimRGB = rimColor * movingRim;
                    float cover = 0.0;
                    if (_OrbitWhiteRim > 0.0)
                    {
                        // Opt in per battle instance; catalog and other shared users retain their look.
                        float rimWidth = _ConfigurableOrbitRim > 0.5 ? max(0.0, _GlowWidth) : 0.055;
                        float2 coreWidth = float2(rimWidth / aspect, rimWidth);
                        float inner = OrbitErodedAlpha(uv, coreWidth);
                        float core = saturate(baseA - inner);
                        // 테두리 내부와 외부를 하나의 불투명 띠로 합성합니다.
                        // 배경에 색을 더하지 않아 모서리 안팎의 녹색이 동일합니다.
                        float outer = OrbitDilatedAlpha(uv, _OrbitUVPerPixel.xy * 2.0);
                        float bandMask = saturate(smoothstep(0.35, 0.65, outer) - smoothstep(0.35, 0.65, inner));
                        // 선두부터 꼬리 길이의 35%까지 불투명하게 유지하고 이후만 감쇠합니다.
                        // 가산 발광만으로는 밑의 흰 선을 덮을 수 없어 해당 구간은 덮어 합성합니다.
                        float tailLength = max(_OrbitTrail, 0.05);
                        float solidHead = 1.0 - smoothstep(tailLength * 0.35, tailLength, age);
                        cover = bandMask * solidHead * saturate(_GlowIntensity);
                        rimRGB = float3(1,1,1) * core * _OrbitWhiteRim * 0.8 * (1.0 - cover)
                               + _GlowColor.rgb * cover;
                        // Coverage stays bounded; radiance must still respond above intensity 1.
                        if (_ConfigurableOrbitRim > 0.5)
                            rimRGB *= max(0.0, _GlowIntensity);
                    }

                    // 내부의 상시 블룸 대신, 화면 밖에서 진입·이탈하는 가우시안 빛띠를 반복합니다.
                    float travel = frac(_SelectedTime / max(_SelectedSweepPeriod, 0.1));
                    float center = lerp(-0.6, 1.6, travel);
                    float bandX = uv.x - (uv.y - 0.5) * 0.28;
                    float band = exp(-pow((bandX - center) / max(_SelectedSweepWidth, 0.02), 2.0));
                    float interior = band * baseA * ero * _SelectedSweepIntensity;
                    float3 animatedRGB = rimRGB + _SelectedSweepColor.rgb * interior * (1.0 - cover);
                    animatedRGB *= i.color.rgb * i.color.a;
                    float outputAlpha = _OrbitWhiteRim > 0.0 ? cover : saturate(max(movingRim, interior));
                    return fixed4(animatedRGB, outputAlpha * i.color.a);
                }

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
