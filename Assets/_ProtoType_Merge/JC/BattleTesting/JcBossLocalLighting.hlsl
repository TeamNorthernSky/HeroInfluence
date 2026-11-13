// JC | 2026-11-12 | 보스 실내/캐릭터는 등록한 창문광·추가광과 선택한 실내 IBL만 받습니다.
// URP 감쇠·쿠키·그림자를 재사용하며 외부 태양광/GI/환경 반사는 계속 차단합니다.
#ifndef JC_BOSS_LOCAL_LIGHTING_INCLUDED
#define JC_BOSS_LOCAL_LIGHTING_INCLUDED
// JC | 2026-11-09 | 실내와 캐릭터 모두 같은 툰/주변 음영 계산을 사용하며 비활성 시 기존 결과를 유지합니다.
#include "JcBankToonPbr.hlsl"
#include "JcIndoorOcclusion.hlsl"
half4 JcBossLocalSurface(InputData inputData, SurfaceData surface, half toonTarget = 1)
{
    BRDFData brdf; InitializeBRDFData(surface, brdf);
    // JC | 2026-11-12 | 외부 GI는 계속 차단하고 실내 전용 IBL만 간접광으로 합산합니다.
    half3 color = 0;
    #if defined(_JC_INDOOR_IBL_TARGET)
    if(_JcIblControl.x>.5)
    {
        BRDFData coat=(BRDFData)0;
        color=JcIblLighting(brdf,coat,0,surface.occlusion,inputData.positionWS,inputData.normalWS,inputData.viewDirectionWS,inputData.normalizedScreenSpaceUV,false);
    }
    #endif
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
        if (light.layerMask == 128u)
        {
            color += LightingPhysicallyBased(brdf, light, inputData.normalWS, inputData.viewDirectionWS);
            half strength=saturate(_JcBankToon.x*(toonTarget>1.5?_JcBankToonOptions.y:_JcBankToonOptions.x));
            if(strength>0) color += JcBankDiffuseDelta(brdf,light,inputData.normalWS)*strength;
        }
    LIGHT_LOOP_END
    return half4(max(0,color)*JcIndoorOcclusion(inputData.positionWS,inputData.normalWS), 1);
}
#endif
