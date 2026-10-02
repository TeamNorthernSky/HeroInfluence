Shader "JC/Tutorial/Goal"
{
    Properties
    {
        [HideInInspector] _Color("Color", Color) = (0.03,0.38,1,1)
        [HideInInspector] _Mode("Floor or solid", Float) = 0
        [HideInInspector] _Clock("Clock", Float) = 0
        [HideInInspector] _Intensity("Intensity", Float) = 1.6
        [HideInInspector] _SweepPeriod("Sweep period", Float) = 2.4
        [HideInInspector] _SweepIntensity("Sweep intensity", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZTest LEqual
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD0; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Mode, _Clock, _Intensity, _SweepPeriod, _SweepIntensity;
            CBUFFER_END
            V Vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(a.normalOS); o.uv=a.uv; o.color=a.color; return o; }
            half4 Frag(V i):SV_Target
            {
                float3 normal = normalize(i.normalWS);
                float light = .55 + .45 * saturate(dot(normal, normalize(float3(-.3,.8,-.6))));
                if (_Mode > .5) return half4(i.color.rgb * light * _Intensity, 1);
                float radius = length(i.uv);
                float ring = 1 - smoothstep(.009, .023, abs(radius - .22));
                float center = 1 - smoothstep(.055, .08, radius);
                float top = saturate((normal.y - .6) * 2.5);
                float phase = frac(_Clock / max(.1, _SweepPeriod));
                float sweepDistance = abs(i.uv.x + i.uv.y - lerp(-1.3, 1.3, phase));
                float shine = exp(-sweepDistance * sweepDistance / .008) * _SweepIntensity * top;
                float3 baseColor = _Color.rgb * light;
                return half4((baseColor + float3(.38,.72,1) * shine) * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
