Shader "Testbed/FireOrbCore"
{
    Properties
    {
        [Header(Colors HDR)]
        [HDR] _ColorCore ("Core Color", Color) = (1.0, 0.15, 0.02, 1)
        [HDR] _ColorMid  ("Mid Color",  Color) = (1.0, 0.45, 0.05, 1)
        [HDR] _ColorRim  ("Rim Color",  Color) = (1.0, 0.85, 0.30, 1)
        _EmissionStrength ("Emission Strength", Range(0,8)) = 2.0

        [Header(Rim)]
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0,3)) = 1.4

        [Header(Alpha)]
        _BaseAlpha ("Base Alpha", Range(0,1)) = 0.25

        [Header(Noise Swirl)]
        _NoiseScale ("Noise Scale", Range(0.2, 8)) = 2.5
        _NoiseSpeed ("Noise Speed", Range(0, 3)) = 0.6
        _SwirlSpeed ("Swirl Speed", Range(0, 3)) = 0.5
        _Distort ("Domain Warp", Range(0, 1)) = 0.35
        _Steps ("Cartoon Steps (0=off)", Range(0, 12)) = 0

        [Header(Lighting)]
        _LitAmount ("Scene Light Amount", Range(0,1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---- procedural value noise + fbm ----
            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }
            float fbm(float3 p)
            {
                float a = 0.5; float s = 0.0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    s += a * vnoise(p);
                    p *= 2.02;
                    a *= 0.5;
                }
                return s;
            }
            float3x3 rotY(float a)
            {
                float c = cos(a); float s = sin(a);
                return float3x3(c, 0, s,  0, 1, 0,  -s, 0, c);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorCore, _ColorMid, _ColorRim;
                half  _EmissionStrength;
                half  _RimPower, _RimStrength;
                half  _BaseAlpha;
                half  _NoiseScale, _NoiseSpeed, _SwirlSpeed, _Distort, _Steps;
                half  _LitAmount;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS  = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);

                // Fresnel rim
                half fres = pow(saturate(1.0 - saturate(dot(N, V))), _RimPower);

                // --- swirling, upward-licking fbm in object space ---
                float t = _Time.y;
                float3 p = IN.positionOS * _NoiseScale;
                p = mul(rotY(t * _SwirlSpeed), p);          // swirl rotation
                p.y *= 0.5;                                 // vertical stretch => flame tongues
                float3 flow = float3(0, t * _NoiseSpeed, 0);// upward scroll => rising flames
                float warp = fbm(p * 0.7 + flow);
                float n = fbm(p + flow + _Distort * warp);  // 0..~1

                // vertical gradient: hotter toward top
                float vgrad = saturate(IN.positionOS.y * 0.5 + 0.5);

                // flame intensity field (hotter where noise high & upper)
                float flame = saturate(n * 1.05 + vgrad * 0.18);

                // ramp param: pow-contrast keeps mids red/orange, only peaks reach rim
                float ramp = saturate(pow(flame, 1.5) * 1.0 + fres * 0.15);

                // cartoon banding
                if (_Steps >= 1.0)
                    ramp = floor(ramp * _Steps) / max(_Steps - 1.0, 1.0);

                // 3-color ramp: core -> mid -> rim
                half3 col = (ramp < 0.5)
                    ? lerp(_ColorCore.rgb, _ColorMid.rgb, ramp * 2.0)
                    : lerp(_ColorMid.rgb,  _ColorRim.rgb, (ramp - 0.5) * 2.0);

                half hot = pow(saturate(flame), 3.0);       // sharp bright cores (rarer)
                half hotspot = hot;
                half3 emission = col * _EmissionStrength * (0.18 + ramp * 0.8 + hot * 1.6 + fres * _RimStrength);

                // --- scene lighting received on the base (req4: lit) ---
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(N, mainLight.direction)) * 0.5 + 0.5; // half-lambert, soft
                half3 ambient = SampleSH(N);
                half3 lit = ambient + mainLight.color * ndotl;
                half3 litBase = col * lit * _LitAmount;

                half3 finalRGB = emission + litBase;
                half alpha = saturate(_BaseAlpha + flame * 0.5 + fres * _RimStrength);

                return half4(finalRGB, alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "FlameShaderGUI"
    Fallback Off
}
