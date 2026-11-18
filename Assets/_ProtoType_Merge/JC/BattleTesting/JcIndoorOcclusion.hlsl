// JC | 2026-11-09 | 보스 주변 음영은 실내 표면/캐릭터의 셰이딩에서만 샘플합니다.
// 화면 전체 합성이나 URP 전역 SSAO 키워드를 켜지 않아 도시와 말풍선의 외형을 유지합니다.
#ifndef JC_INDOOR_OCCLUSION_INCLUDED
#define JC_INDOOR_OCCLUSION_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
float4 _JcIndoorSsao, _JcIndoorMode;
half JcIndoorOcclusion(float3 positionWS, float3 normalWS)
{
    if(_JcIndoorSsao.x<=0) return 1;
    float radius=max(.02,_JcIndoorSsao.y);
    float4 clip=TransformWorldToHClip(positionWS);
    float2 uv=ComputeScreenPos(clip).xy / clip.w;
    float2 screenRadius=radius*float2(UNITY_MATRIX_P._m00,UNITY_MATRIX_P._m11)*.5/max(.01,clip.w);
    // JC | 2026-11-17 | 같은 거리의 8개 복제 윤곽 대신 반경을 분산한 16개 표본으로 접점만 누적합니다.
    // 화면 노이즈/시간 회전 없이 안정적으로 유지하며 동일 평면의 깊이 오차는 제외합니다.
    float planeBias=max(.015,radius*.08);
    float occlusion=0;
    [unroll] for(int j=0;j<16;j++)
    {
        float angle=j*2.39996323;
        float2 sampleUV=uv+float2(cos(angle),sin(angle))*screenRadius*sqrt((j+.5)/16);
        if(any(sampleUV<0)||any(sampleUV>1))continue;
        float depth=SampleSceneDepth(sampleUV);
        #if !UNITY_REVERSED_Z
        depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
        #endif
        float3 delta=ComputeWorldSpacePosition(sampleUV,depth,UNITY_MATRIX_I_VP)-positionWS;
        float distance=length(delta);
        float facing=smoothstep(planeBias,planeBias+radius*.25,dot(normalWS,delta));
        float falloff=1-smoothstep(radius*.15,radius,distance);
        occlusion+=facing*falloff;
    }
    // JC | 2026-11-17 | 직접광 영향은 조명 항에서 적용합니다. 강도 증가가 검은 얼룩으로 즉시 포화되지 않게 합니다.
    return exp2(-occlusion*(2.0/16)*_JcIndoorSsao.x);
}
#endif
