#ifndef JC_VILLAIN_BLACK_MASK_INCLUDED
#define JC_VILLAIN_BLACK_MASK_INCLUDED
// Both color baking and live surface controls select from the original unlit texture.
float JcVillainBlackWeight(float3 srgb,float threshold,float feather,float saturationLimit)
{
    float hi=max(srgb.r,max(srgb.g,srgb.b)),lo=min(srgb.r,min(srgb.g,srgb.b));
    float saturation=(hi-lo)/max(hi,.0001);
    return (1-smoothstep(threshold,threshold+feather,hi))*(1-smoothstep(.08,saturationLimit,saturation));
}
#endif
