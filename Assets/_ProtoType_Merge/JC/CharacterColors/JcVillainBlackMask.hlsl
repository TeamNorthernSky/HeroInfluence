#ifndef JC_VILLAIN_BLACK_MASK_INCLUDED
#define JC_VILLAIN_BLACK_MASK_INCLUDED
// Both color baking and live surface controls select from the original unlit texture.
// JC | 2026-11-04 | 조명이나 베이크 결과에 따라 범위가 달라지지 않도록 원본 몸체의 어두운 저채도 영역을 기준으로 보정합니다.
float JcVillainBlackWeight(float3 srgb,float threshold,float feather,float saturationLimit)
{
    float hi=max(srgb.r,max(srgb.g,srgb.b)),lo=min(srgb.r,min(srgb.g,srgb.b));
    float saturation=(hi-lo)/max(hi,.0001);
    return (1-smoothstep(threshold,threshold+feather,hi))*(1-smoothstep(.08,saturationLimit,saturation));
}
#endif
