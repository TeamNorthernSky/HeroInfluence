#ifndef JC_CLOUD_SHADOW_INCLUDED
#define JC_CLOUD_SHADOW_INCLUDED
float4 _JcCloudSettings;
float4 _JcCloudMotion;
half4 _JcCloudColor;
            // 폐기된 FogOfWarHI의 GradientNoise를 재사용합니다. 시야/SDF/탐색 상태는 사용하지 않습니다.
            float2 HashGradient(float2 p)
            {
                p = float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)));
                return -1 + 2 * frac(sin(p) * 43758.5453123);
            }
            float GradientNoise(float2 p)
            {
                float2 i=floor(p), f=frac(p), u=f*f*(3-2*f);
                return .5+.5*lerp(lerp(dot(HashGradient(i),f),dot(HashGradient(i+float2(1,0)),f-float2(1,0)),u.x),
                    lerp(dot(HashGradient(i+float2(0,1)),f-float2(0,1)),dot(HashGradient(i+1),f-1),u.x),u.y);
            }
            float Cloud(float2 p, float evolution)
            {
                // 큰 덩어리와 약한 세부 변화. 각 층이 반대 방향으로 빠르게 흐르지 않도록 합니다.
                float2 distortion=float2(GradientNoise(p*.45+evolution),GradientNoise(p*.45+17-evolution))-.5;
                p+=distortion*.32;
                return GradientNoise(p)*.68+GradientNoise(p*2.03+13)*.23+GradientNoise(p*4.07-9)*.09;
            }

float JcCloudMask(float3 world)
{
    float2 p=(world.xz-_JcCloudMotion.xy)/max(1,_JcCloudSettings.y);
    float noise=Cloud(p,_JcCloudMotion.z/max(1,_JcCloudSettings.y)*.07);
    float threshold=lerp(.66,.34,_JcCloudSettings.w);
    return saturate(_JcCloudSettings.x)*smoothstep(threshold-_JcCloudSettings.z*.5,threshold+_JcCloudSettings.z*.5,noise);
}
half3 JcApplyCloud(half3 color,float3 world)
{
    if (_JcCloudSettings.x <= 0) return color;
    return lerp(color,_JcCloudColor.rgb,JcCloudMask(world));
}
#endif
