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
    float2 directions[8]={float2(1,0),float2(-1,0),float2(0,1),float2(0,-1),float2(.707,.707),float2(-.707,.707),float2(.707,-.707),float2(-.707,-.707)};
    float occlusion=0;
    [unroll] for(int j=0;j<8;j++)
    {
        float2 sampleUV=uv+directions[j]*screenRadius*(j<4?.45:.85);
        if(any(sampleUV<0)||any(sampleUV>1))continue;
        float depth=SampleSceneDepth(sampleUV);
        #if !UNITY_REVERSED_Z
        depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
        #endif
        float3 delta=ComputeWorldSpacePosition(sampleUV,depth,UNITY_MATRIX_I_VP)-positionWS;
        float distance=length(delta);
        float facing=saturate((dot(normalWS,delta)-.015)/max(.001,distance));
        occlusion+=facing*saturate(1-distance/radius);
    }
    return saturate(1-occlusion*.5*_JcIndoorSsao.x*_JcIndoorSsao.z);
}
#endif
