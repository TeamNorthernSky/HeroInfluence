Shader "JC/Indicators/Move Range Grid"
{
    Properties
    {
        _BaseColor("Grid Color", Color) = (0.15, 1, 0.35, 1)
        _Opacity("Opacity", Range(0,1)) = 0.4
        _Intensity("Emission Intensity", Float) = 3
        _CurtainHeight("Curtain Height", Float) = 0.18
        _CurtainOpacity("Curtain Opacity", Range(0,1)) = 0.16
        _CurtainIntensity("Curtain Emission", Float) = 1.5
        _CurtainSoftness("Curtain Softness", Float) = 2
        _UniformCurtain("Uniform Screen Height", Float) = 0
        _CurtainScreenHeight("Screen Height Fraction", Float) = 0.012
        _CurtainMaxHeight("Maximum World Height", Float) = 1.5
        _AdaptDayNight("Adapt To Day Night", Float) = 0
        _NightIntensityMultiplier("Night Intensity Multiplier", Float) = 0.2
        _EdgeSoftness("Line Edge Softness", Range(0,0.5)) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "MovementGrid"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            // 지면/연석과 같은 깊이의 조각만 작은 래스터 깊이 보정으로 안정화합니다.
            // 실제 앞쪽 건물의 가림은 LEqual로 유지합니다.
            ZTest LEqual
            Offset -1, -1
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Opacity, _Intensity;
                float _CurtainHeight, _CurtainOpacity, _CurtainIntensity, _CurtainSoftness;
                float _EdgeSoftness;
                float _UniformCurtain, _CurtainScreenHeight, _CurtainMaxHeight;
                float _AdaptDayNight, _NightIntensityMultiplier;
            CBUFFER_END
            // 기존 낮밤 시스템이 설정하는 전역 값: x=활성, y=낮 밝기 비율.
            float4 _JcDayNight;
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 edges : TEXCOORD1;
                float2 mode : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 edges : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fog : TEXCOORD3;
                float mode : TEXCOORD4;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float height = _CurtainHeight;
                if (v.mode.x > 0.5 && _UniformCurtain > 0.5 && height > 0)
                {
                    float4 baseClip = TransformWorldToHClip(world);
                    float4 upClip = mul(UNITY_MATRIX_VP, float4(0, 1, 0, 0));
                    float w = max(baseClip.w, 0.0001);
                    float2 projectedUp = upClip.xy - baseClip.xy / w * upClip.w;
                    projectedUp.x *= _ScreenParams.x / max(_ScreenParams.y, 1);
                    float target = 2 * _CurtainScreenHeight;
                    float denominator = length(projectedUp) - target * upClip.w;
                    height = min(_CurtainMaxHeight, target * w / max(denominator, 0.0001));
                }
                world.y += v.mode.x * v.uv.y * height;
                o.positionCS = TransformWorldToHClip(world);
                o.positionWS = world;
                o.uv = v.uv;
                o.edges = v.edges;
                o.mode = v.mode.x;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half alpha;
                half3 color;
                if (i.mode > 0.5)
                {
                    // 수평 방향은 균일하게 유지하고 높이 방향으로만 부드럽게 감쇠합니다.
                    float fade = pow(saturate(1 - i.uv.y), _CurtainSoftness);
                    alpha = _CurtainOpacity * fade * step(0.0001, _CurtainHeight);
                    color = _BaseColor.rgb * _CurtainIntensity;
                }
                else
                {
                    // 연결면은 유지하고 노출된 가장자리만 픽셀 폭으로 부드럽게 처리합니다.
                    float4 distanceToEdge = float4(i.uv.x, 1-i.uv.x, i.uv.y, 1-i.uv.y);
                    float4 aa = max(float4(fwidth(i.uv.x).xx, fwidth(i.uv.y).xx), 0.00001);
                    float4 coverage = lerp(1, smoothstep(0, max(aa, _EdgeSoftness), distanceToEdge), i.edges);
                    alpha = min(min(coverage.x, coverage.y), min(coverage.z, coverage.w)) * _Opacity;
                    color = _BaseColor.rgb * _Intensity;
                }
                float daylight = lerp(1, saturate(_JcDayNight.y), saturate(_JcDayNight.x) * _AdaptDayNight);
                color *= lerp(_NightIntensityMultiplier, 1, daylight);
                return half4(MixFog(color, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}
