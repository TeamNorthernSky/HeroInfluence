// Unity Chan Original Shader - URP HLSL Port
// Core Skin Module

#ifndef KJ_CHARA_SKIN_URP_INCLUDED
#define KJ_CHARA_SKIN_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Textures
TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
TEXTURE2D(_FalloffSampler);   SAMPLER(sampler_FalloffSampler);
TEXTURE2D(_RimLightSampler);  SAMPLER(sampler_RimLightSampler);

#define FALLOFF_POWER 1.0

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 uv           : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float3 normalWS     : TEXCOORD0;
    float2 uv           : TEXCOORD1;
    float3 eyeDir       : TEXCOORD2;
    float3 lightDir     : TEXCOORD3;
    float3 positionWS   : TEXCOORD4;
};

// Vertex shader
Varyings vert(Attributes input)
{
    Varyings output;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    
    // Eye direction
    output.eyeDir = normalize(GetCameraPositionWS() - output.positionWS);

    Light mainLight = GetMainLight();
    output.lightDir = normalize(mainLight.direction);

    return output;
}

// Fragment shader
half4 frag(Varyings input) : SV_Target
{
    half4 diffSamplerColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

    // Falloff
    half normalDotEye = dot(normalize(input.normalWS), input.eyeDir);
    half falloffU = clamp(1.0 - abs(normalDotEye), 0.02, 0.98);
    half4 falloffSamplerColor = FALLOFF_POWER * SAMPLE_TEXTURE2D(_FalloffSampler, sampler_FalloffSampler, float2(falloffU, 0.25));
    
    half3 combinedColor = lerp(diffSamplerColor.rgb, falloffSamplerColor.rgb * diffSamplerColor.rgb, falloffSamplerColor.a);

    // Rimlight
    half rimlightDot = saturate(0.5 * (dot(normalize(input.normalWS), input.lightDir) + 1.0));
    falloffU = saturate(rimlightDot * falloffU);
    falloffU = SAMPLE_TEXTURE2D(_RimLightSampler, sampler_RimLightSampler, float2(falloffU, 0.25)).r;
    half3 lightColor = diffSamplerColor.rgb * 0.5;
    combinedColor += falloffU * lightColor;

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    half4 lightColor0 = half4(mainLight.color, 1.0);

#ifdef ENABLE_CAST_SHADOWS
    // Cast shadows
    half3 shadowColor = _ShadowColor.rgb * combinedColor;
    half attenuation = mainLight.shadowAttenuation;
    combinedColor = lerp(shadowColor, combinedColor, attenuation);
#endif

    return half4(combinedColor, diffSamplerColor.a) * _Color * lightColor0;
}

#endif
