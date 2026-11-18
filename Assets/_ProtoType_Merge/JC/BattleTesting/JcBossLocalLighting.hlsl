// JC | 2026-11-12 | 보스 실내/캐릭터는 등록한 창문광·추가광과 선택한 실내 IBL만 받습니다.
// URP 감쇠·쿠키·그림자를 재사용하며 외부 태양광/GI/환경 반사는 계속 차단합니다.
#ifndef JC_BOSS_LOCAL_LIGHTING_INCLUDED
#define JC_BOSS_LOCAL_LIGHTING_INCLUDED
// JC | 2026-11-09 | 실내와 캐릭터 모두 같은 툰/주변 음영 계산을 사용하며 비활성 시 기존 결과를 유지합니다.
#include "JcBankToonPbr.hlsl"
#include "JcIndoorOcclusion.hlsl"
// JC | 2026-11-18 | 증폭기만 반사 배율과 베이크 접점 AO를 전달합니다. 기본 인수 1로 기존 방/캐릭터 결과를 보존합니다.
half4 JcBossLocalSurface(InputData inputData, SurfaceData surface, half toonTarget = 1, half reflection = 1, half bakedDirectAo = 1, half2 bossSpotResponse = half2(1,1))
{
    BRDFData brdf; InitializeBRDFData(surface, brdf);
    brdf.specular *= reflection;
    brdf.grazingTerm *= reflection;
    // JC | 2026-11-12 | 외부 GI는 계속 차단하고 실내 전용 IBL만 간접광으로 합산합니다.
    half3 color = 0;
    // JC | 2026-11-17 | AO는 간접광과 직접 확산광에 분리 적용하고 정반사/IBL 설정 자체는 바꾸지 않습니다.
    half ao=JcIndoorOcclusion(inputData.positionWS,inputData.normalWS);
    half directAo=lerp(1,ao,saturate(_JcIndoorSsao.z))*bakedDirectAo;
    #if defined(_JC_INDOOR_IBL_TARGET)
    if(_JcIblControl.x>.5)
    {
        BRDFData coat=(BRDFData)0;
        color=JcIblLighting(brdf,coat,0,surface.occlusion*ao,inputData.positionWS,inputData.normalWS,inputData.viewDirectionWS,inputData.normalizedScreenSpaceUV,false);
    }
    #endif
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1));
        // JC | 2026-11-18 | 일반 실내광(128)과 보스 조준 스팟(192)만 수용합니다. 방/아군 호출의 배율 기본값은 1입니다.
        if (light.layerMask == 128u || light.layerMask == 192u)
        {
            half reception=light.layerMask==192u ? max(0,bossSpotResponse.x) : 1;
            BRDFData directBrdf=brdf;
            // JC | 2026-11-18 | 해당 스팟의 정반사 성분만 조절합니다. 확산광·다른 광원·위에서 계산한 IBL에는 반사 배율을 적용하지 않습니다.
            if(light.layerMask==192u) directBrdf.specular*=max(0,bossSpotResponse.y);
            color += LightingPhysicallyBased(directBrdf, light, inputData.normalWS, inputData.viewDirectionWS)*reception;
            half3 diffuse=brdf.diffuse*light.color*(light.distanceAttenuation*light.shadowAttenuation)*saturate(dot(inputData.normalWS,light.direction));
            color += diffuse*(directAo-1)*reception;
            half strength=saturate(_JcBankToon.x*(toonTarget>1.5?_JcBankToonOptions.y:_JcBankToonOptions.x));
            if(strength>0) color += JcBankDiffuseDelta(brdf,light,inputData.normalWS)*strength*directAo*reception;
        }
    LIGHT_LOOP_END
    return half4(max(0,color), 1);
}
#endif
