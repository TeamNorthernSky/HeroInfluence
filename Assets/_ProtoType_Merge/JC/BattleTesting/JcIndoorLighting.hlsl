// JC | 2026-11-12 | 소유한 세 셰이더에서만 URP GI 호출을 연결합니다. 패키지·다른 셰이더의 전역 동작은 변경하지 않습니다.
#ifndef JC_INDOOR_LIGHTING_INCLUDED
#define JC_INDOOR_LIGHTING_INCLUDED
#include "JcIndoorIbl.hlsl"
#define GlobalIllumination JcIndoorGlobalIllumination
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#undef GlobalIllumination
#endif
