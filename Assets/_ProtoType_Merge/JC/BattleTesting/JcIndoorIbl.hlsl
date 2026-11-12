// JC | 2026-11-12 | 전용 환경만 샘플링합니다. 도시의 하늘·Light Probe·RenderSettings를 읽지 않습니다.
#ifndef JC_INDOOR_IBL_INCLUDED
#define JC_INDOOR_IBL_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
TEXTURECUBE(_JcIblSpecular); SAMPLER(sampler_JcIblSpecular);
float4 _JcIblControl, _JcIblTint, _JcIblRotation;
float4 _JcIblSH[9];
float3 JcIblDirection(float3 d)
{
    return float3(_JcIblRotation.x*d.x-_JcIblRotation.y*d.z,d.y,_JcIblRotation.y*d.x+_JcIblRotation.x*d.z);
}
half3 JcIblDiffuse(float3 n)
{
    n=JcIblDirection(n);
    float3 c=_JcIblSH[0].rgb*.2820947918;
    c+=_JcIblSH[1].rgb*(.4886025119*n.y)+_JcIblSH[2].rgb*(.4886025119*n.z)+_JcIblSH[3].rgb*(.4886025119*n.x);
    c+=_JcIblSH[4].rgb*(1.092548431*n.x*n.y)+_JcIblSH[5].rgb*(1.092548431*n.y*n.z);
    c+=_JcIblSH[6].rgb*(.3153915653*(3*n.z*n.z-1))+_JcIblSH[7].rgb*(1.092548431*n.x*n.z);
    c+=_JcIblSH[8].rgb*(.5462742153*(n.x*n.x-n.y*n.y));
    return max(0,c)*_JcIblTint.rgb*_JcIblControl.y;
}
half3 JcIblReflection(float3 r, half roughness)
{
    float mip=roughness*(1.7-.7*roughness)*_JcIblRotation.z;
    return SAMPLE_TEXTURECUBE_LOD(_JcIblSpecular,sampler_JcIblSpecular,JcIblDirection(r),mip).rgb*_JcIblTint.rgb*_JcIblControl.z;
}
half3 JcIblLighting(BRDFData brdf,BRDFData coat,half coatMask,half occlusion,float3 position,half3 normal,half3 view,float2 uv,bool allowBankProbe)
{
    half3 r=reflect(-view,normal);
    half fresnel=Pow4(1-saturate(dot(normal,view)));
    half3 reflection=0;
    // JC | 2026-11-12 | 기존 재질의 환경 반사 끄기 옵션을 IBL에서도 존중합니다.
    #ifndef _ENVIRONMENTREFLECTIONS_OFF
    reflection=JcIblReflection(r,brdf.perceptualRoughness);
    if(allowBankProbe && _JcIblControl.w>0)
        reflection=lerp(reflection,GlossyEnvironmentReflection(r,position,brdf.perceptualRoughness,1,uv),_JcIblControl.w);
    #endif
    half3 color=EnvironmentBRDF(brdf,JcIblDiffuse(normal),reflection,fresnel);
    #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half3 coatReflection=0;
    #ifndef _ENVIRONMENTREFLECTIONS_OFF
    coatReflection=JcIblReflection(r,coat.perceptualRoughness);
    if(allowBankProbe && _JcIblControl.w>0)
        coatReflection=lerp(coatReflection,GlossyEnvironmentReflection(r,position,coat.perceptualRoughness,1,uv),_JcIblControl.w);
    #endif
    color=color*(1-(kDielectricSpec.x+kDielectricSpec.a*fresnel)*coatMask)+EnvironmentBRDFClearCoat(coat,coatMask,coatReflection,fresnel);
    #endif
    return color*occlusion;
}
// JC | 2026-11-12 | URP의 직접광 계산은 그대로 두고 GI 함수 호출만 교체합니다. 비활성 시 원래 함수를 그대로 호출합니다.
half3 JcIndoorGlobalIllumination(BRDFData brdf,BRDFData coat,float coatMask,half3 bakedGI,half occlusion,float3 position,half3 normal,half3 view,float2 uv)
{
    // JC | 2026-11-12 | 같은 셰이더를 쓰는 원경/야외 재질은 전용 복제본 키워드가 없으면 영향을 받지 않습니다.
    #if !defined(_JC_INDOOR_IBL_TARGET)
    return GlobalIllumination(brdf,coat,coatMask,bakedGI,occlusion,position,normal,view,uv);
    #else
    if(_JcIblControl.x<.5) return GlobalIllumination(brdf,coat,coatMask,bakedGI,occlusion,position,normal,view,uv);
    return JcIblLighting(brdf,coat,coatMask,occlusion,position,normal,view,uv,true);
    #endif
}
#endif
