// JC | 2026-11-05 | 보스 실내/캐릭터는 명시적으로 등록한 창문광과 추가광만 받습니다.
// URP의 실제 감쇠·쿠키·그림자를 재사용하며 태양광/GI/환경 반사/발광을 합산하지 않습니다.
#ifndef JC_BOSS_LOCAL_LIGHTING_INCLUDED
#define JC_BOSS_LOCAL_LIGHTING_INCLUDED
// JC | 2026-11-09 | 실내와 캐릭터 모두 같은 툰/주변 음영 계산을 사용하며 비활성 시 기존 결과를 유지합니다.
#include "JcBankToonPbr.hlsl"
#include "JcIndoorOcclusion.hlsl"
half4 JcBossLocalSurface(InputData inputData, SurfaceData surface, half toonTarget = 1)
{
    BRDFData brdf; InitializeBRDFData(surface, brdf);
    half3 color = 0;
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
