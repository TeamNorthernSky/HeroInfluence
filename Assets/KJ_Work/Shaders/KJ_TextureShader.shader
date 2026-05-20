Shader "Custom/DomainWarpCloud"
{
    Properties
    {
        _Scale ("Scale", Float) = 3.0
        _WarpStrength ("Warp Strength", Float) = 0.5
        _Speed ("Flow Speed", Float) = 0.1
        _CloudThreshold ("Cloud Threshold", Range(0,1)) = 0.5
        _CloudSoftness ("Cloud Softness", Range(0.01,0.5)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float _Scale;
            float _WarpStrength;
            float _Speed;
            float _CloudThreshold;
            float _CloudSoftness;

            // =========================
            // Perlin Fade Function
            // =========================
            float fade(float t)
            {
                return t * t * t * (t * (t * 6 - 15) + 10);
            }

            // 해시
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Gradient Noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                float2 u = float2(fade(f.x), fade(f.y));

                return lerp(lerp(a, b, u.x),
                            lerp(c, d, u.x), u.y);
            }

            // =========================
            // FBM
            // =========================
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < 5; i++)
                {
                    value += noise(p * frequency) * amplitude;
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }

                return value;
            }

            // =========================
            // Domain Warping
            // =========================
            float2 domainWarp(float2 uv)
            {
                float2 q;
                q.x = fbm(uv + float2(0.0, 0.0));
                q.y = fbm(uv + float2(5.2, 1.3));

                float2 r;
                r.x = fbm(uv + _WarpStrength * q + float2(1.7, 9.2));
                r.y = fbm(uv + _WarpStrength * q + float2(8.3, 2.8));

                return uv + r * _WarpStrength;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv * _Scale;

                // 흐르는 효과 (구름 이동)
                uv.x += _Time.y * _Speed;

                // Domain Warping 적용
                float2 warpedUV = domainWarp(uv);

                // FBM 두 번 (PPT 구조 반영)
                float n = fbm(warpedUV);
                n += fbm(warpedUV * 1.5);

                // 구름 형태 만들기 (smoothstep)
                float cloud = smoothstep(
                    _CloudThreshold,
                    _CloudThreshold + _CloudSoftness,
                    n
                );

                return float4(cloud, cloud, cloud, 1.0);
            }
            ENDCG
        }
    }
}