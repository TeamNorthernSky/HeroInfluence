// JC | 2026-11-06 | URP 14의 PBR 결과에서 확산광 차이만 더합니다. 금속 반사·광택·GI·SSAO·그림자 감쇠는 유지합니다.
#ifndef JC_BANK_TOON_PBR_INCLUDED
#define JC_BANK_TOON_PBR_INCLUDED
#include "JcBankToonCommon.hlsl"
half3 JcBankDiffuseDelta(BRDFData brdf, Light light, half3 normalWS)
{
    half lambert = saturate(dot(normalWS,light.direction));
    half band = JcBankBand(lambert);
    half3 toon = band * lerp(_JcBankToonTint.rgb,half3(1,1,1),band);
    return brdf.diffuse * light.color * (light.distanceAttenuation * light.shadowAttenuation) * (toon - lambert);
}
half4 JcBankPbr(InputData data, SurfaceData surface)
{
    half4 color = UniversalFragmentPBR(data,surface);
    half strength = saturate(_JcBankToon.x * _JcBankToonOptions.x);
    if(strength <= 0) return color;
    BRDFData brdf;
    InitializeBRDFData(surface,brdf);
    half4 shadowMask = CalculateShadowMask(data);
    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(data,surface);
    Light main = GetMainLight(data,shadowMask,ao);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    half3 delta = 0;
    #ifdef _LIGHT_LAYERS
    if(IsMatchingLightLayer(main.layerMask,meshRenderingLayers))
    #endif
    delta += JcBankDiffuseDelta(brdf,main,data.normalWS);
    #if defined(_ADDITIONAL_LIGHTS)
    // JC | 2026-11-06 | Forward+의 광원 순회 매크로가 사용하는 입력 이름을 유지합니다.
    InputData inputData = data;
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_FORWARD_PLUS
    for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light=GetAdditionalLight(lightIndex,data,shadowMask,ao);
        #ifdef _LIGHT_LAYERS
        if(IsMatchingLightLayer(light.layerMask,meshRenderingLayers))
        #endif
        delta += JcBankDiffuseDelta(brdf,light,data.normalWS);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light=GetAdditionalLight(lightIndex,data,shadowMask,ao);
        #ifdef _LIGHT_LAYERS
        if(IsMatchingLightLayer(light.layerMask,meshRenderingLayers))
        #endif
        delta += JcBankDiffuseDelta(brdf,light,data.normalWS);
    LIGHT_LOOP_END
    #endif
    color.rgb=max(0,color.rgb+delta*strength);
    return color;
}
#endif
