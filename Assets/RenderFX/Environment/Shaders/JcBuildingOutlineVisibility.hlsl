#ifndef JC_BUILDING_OUTLINE_VISIBILITY_INCLUDED
#define JC_BUILDING_OUTLINE_VISIBILITY_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
TEXTURE2D_X(_JcBuildingShapeDepth);
float _JcBuildingHighlight;

half JcOutlineVisibility(float2 uv, float2 radius, float bias)
{
    if (_JcBuildingHighlight < .5) return 1;
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
    if (inverseDepth <= 0) return 0;
    float rawDepth = SampleSceneDepth(uv);
    float sceneDepth = unity_OrthoParams.w > .5 ? LinearDepthToEyeDepth(rawDepth) : LinearEyeDepth(rawDepth, _ZBufferParams);
    return step(rcp(inverseDepth), sceneDepth + max(bias, .0001));
}
#endif
