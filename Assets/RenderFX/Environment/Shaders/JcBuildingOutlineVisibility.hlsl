#ifndef JC_BUILDING_OUTLINE_VISIBILITY_INCLUDED
#define JC_BUILDING_OUTLINE_VISIBILITY_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
TEXTURE2D_X(_JcBuildingShapeDepth);
// JC | 2026-11-04 | 외곽선에 쓸 실루엣 경계를 분리해 건물 내부의 가림 경계까지 강조되는 것을 막습니다.
float _JcBuildingHighlight, _JcBuildingExteriorOnly;
float4 _JcBuildingExteriorRadius;
// JC | 2026-11-09 | 오브젝트 호버만 실제로 보이는 느낌표 뒤에 합성합니다. 건물 뒤에 숨은 아이콘은 마스크가 0입니다.
TEXTURE2D(_JcEventMarkerCoverage);
float _JcTriggerDepthActive;

half JcOutlineVisibility(float2 uv, float2 radius, float bias)
{
    if (_JcBuildingHighlight < .5) return 1;
    // 최종 합성에서도 원본 실루엣을 제외해 AA가 메시 안으로 번지지 않게 한다.
    if (_JcBuildingHighlight > 1.5)
    {
        if (SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv).r > .0001) return 0;
        return _JcTriggerDepthActive > .5 ? 1 - SAMPLE_TEXTURE2D(_JcEventMarkerCoverage, sampler_PointClamp, uv).r : 1;
    }
    // JC | 2026-11-04 | 건물 외곽선은 몸체 내부를 제외하되 앞쪽 캐릭터·지형에 대한 가림은 유지합니다.
    if (_JcBuildingExteriorOnly > .5 && SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv).r > .0001) return 0;
    // 포인트 샘플은 건물 자기 깊이를 이웃의 경사 깊이와 섞지 않는다.
    float inverseDepth = SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv).g;
    if (inverseDepth <= 0)
    {
        // 최종 AA가 건물 바깥 1px로 퍼진 곳도 인접한 건물 깊이로 가림 처리한다.
        inverseDepth = max(inverseDepth, SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv + radius).g);
        inverseDepth = max(inverseDepth, SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv - radius).g);
        inverseDepth = max(inverseDepth, SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv + float2(radius.x, -radius.y)).g);
        inverseDepth = max(inverseDepth, SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv + float2(-radius.x, radius.y)).g);
    }
    if (_JcBuildingExteriorOnly > .5 && inverseDepth <= 0)
    {
        [unroll] for (int i = 0; i < 16; i++)
        {
            float angle = i * (6.28318530718 / 16.0);
            float2 offset = float2(cos(angle), sin(angle)) * _JcBuildingExteriorRadius.xy;
            inverseDepth = max(inverseDepth, SAMPLE_TEXTURE2D_X(_JcBuildingShapeDepth, sampler_PointClamp, uv + offset * _JcBuildingExteriorRadius.z).g);
        }
    }
    if (inverseDepth <= 0) return 0;
    float rawDepth = SampleSceneDepth(uv);
    float sceneDepth = unity_OrthoParams.w > .5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
    return step(rcp(inverseDepth), sceneDepth + max(bias, .0001));
}
#endif
