// JC | 2026-11-04 | 네 방향에서 건물로 둘러싸인 빈 픽셀을 채워 내부 틈의 확대별 선 깜빡임을 막습니다.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
TEXTURE2D_X(_JcBuildingEnvelopeSource);
float4 _JcBuildingEnvelopeTexel;
float _JcBuildingEnvelopeStep;
float4 EnvelopeSample(float2 uv)
{
    if (any(uv < 0) || any(uv > 1)) return 0;
    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
}
float4 EnvelopeInit(Varyings input):SV_Target
{
    return step(.5, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).r).xxxx;
}
float4 EnvelopeScan(Varyings input):SV_Target
{
    float2 d = _JcBuildingEnvelopeTexel.xy * _JcBuildingEnvelopeStep;
    float4 v = EnvelopeSample(input.texcoord);
    v.r = max(v.r, EnvelopeSample(input.texcoord - float2(d.x, 0)).r);
    v.g = max(v.g, EnvelopeSample(input.texcoord + float2(d.x, 0)).g);
    v.b = max(v.b, EnvelopeSample(input.texcoord - float2(0, d.y)).b);
    v.a = max(v.a, EnvelopeSample(input.texcoord + float2(0, d.y)).a);
    return v;
}
float4 EnvelopeCombine(Varyings input):SV_Target
{
    float4 directions = EnvelopeSample(input.texcoord);
    float4 original = SAMPLE_TEXTURE2D_X(_JcBuildingEnvelopeSource, sampler_PointClamp, input.texcoord);
    original.r = max(original.r, min(min(directions.r, directions.g), min(directions.b, directions.a)));
    return original;
}
