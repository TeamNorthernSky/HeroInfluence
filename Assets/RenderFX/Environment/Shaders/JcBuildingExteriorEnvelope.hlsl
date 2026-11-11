// JC | 2026-11-04 | 네 방향에서 건물로 둘러싸인 빈 픽셀을 채워 내부 틈의 확대별 선 깜빡임을 막습니다.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
TEXTURE2D_X(_JcBuildingEnvelopeSource);
float4 _JcBuildingEnvelopeTexel;
float _JcBuildingEnvelopeStep;
// JC | 2026-11-10 | 좌/우/하/상 이진 상태를 1/2/4/8 비트로 보존합니다. R8 정규화값은 점 샘플 후 정수로 복원합니다.
uint EnvelopeSample(float2 uv)
{
    if (any(uv < 0) || any(uv > 1)) return 0u;
    return (uint)(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r * 255.0 + .5);
}
float4 EnvelopeInit(Varyings input):SV_Target
{
    uint bits = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).r >= .5 ? 15u : 0u;
    return float4(bits / 255.0, 0, 0, 0);
}
float4 EnvelopeScan(Varyings input):SV_Target
{
    float2 d = _JcBuildingEnvelopeTexel.xy * _JcBuildingEnvelopeStep;
    uint bits = EnvelopeSample(input.texcoord);
    // JC | 2026-11-10 | 기존 방향별 max(0,1)를 해당 비트의 OR로 치환하며 반복 간격과 화면 밖 처리는 유지합니다.
    bits |= EnvelopeSample(input.texcoord - float2(d.x, 0)) & 1u;
    bits |= EnvelopeSample(input.texcoord + float2(d.x, 0)) & 2u;
    bits |= EnvelopeSample(input.texcoord - float2(0, d.y)) & 4u;
    bits |= EnvelopeSample(input.texcoord + float2(0, d.y)) & 8u;
    return float4(bits / 255.0, 0, 0, 0);
}
float4 EnvelopeCombine(Varyings input):SV_Target
{
    uint directions = EnvelopeSample(input.texcoord);
    float4 original = SAMPLE_TEXTURE2D_X(_JcBuildingEnvelopeSource, sampler_PointClamp, input.texcoord);
    // JC | 2026-11-10 | 네 방향이 모두 참일 때만 채웁니다. 원본 깊이 등 나머지 채널은 그대로 전달합니다.
    original.r = max(original.r, (directions & 15u) == 15u ? 1.0 : 0.0);
    return original;
}
