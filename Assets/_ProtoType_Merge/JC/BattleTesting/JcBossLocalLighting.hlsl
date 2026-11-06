// JC | 2026-11-05 | 보스 실내/캐릭터는 명시적으로 등록한 창문광과 추가광만 받습니다.
// URP의 실제 감쇠·쿠키·그림자를 재사용하며 태양광/GI/환경 반사/발광을 합산하지 않습니다.
#ifndef JC_BOSS_LOCAL_LIGHTING_INCLUDED
#define JC_BOSS_LOCAL_LIGHTING_INCLUDED
half4 JcBossLocalSurface(InputData inputData, SurfaceData surface)
{
    BRDFData brdf; InitializeBRDFData(surface, brdf);
    half3 color = 0;
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
        if (light.layerMask == 128u)
            color += LightingPhysicallyBased(brdf, light, inputData.normalWS, inputData.viewDirectionWS);
    LIGHT_LOOP_END
    return half4(color, 1);
}
#endif
