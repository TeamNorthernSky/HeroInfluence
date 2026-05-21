// Unity Chan Original Shader - URP HLSL Port
// Core Main Module

#ifndef KJ_CHARA_MAIN_URP_INCLUDED
#define KJ_CHARA_MAIN_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Textures
TEXTURE2D(_MainTex);                   SAMPLER(sampler_MainTex);
TEXTURE2D(_FalloffSampler);            SAMPLER(sampler_FalloffSampler);
TEXTURE2D(_RimLightSampler);           SAMPLER(sampler_RimLightSampler);
TEXTURE2D(_SpecularReflectionSampler); SAMPLER(sampler_SpecularReflectionSampler);
TEXTURE2D(_EnvMapSampler);             SAMPLER(sampler_EnvMapSampler);
#ifdef ENABLE_NORMAL_MAP
TEXTURE2D(_NormalMapSampler);          SAMPLER(sampler_NormalMapSampler);
#endif

#define FALLOFF_POWER 0.3

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 uv           : TEXCOORD0;
#ifdef ENABLE_NORMAL_MAP
    float4 tangentOS    : TANGENT;
#endif
};

struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
    float3 eyeDir       : TEXCOORD1;
    float3 lightDir     : TEXCOORD2;
    float3 normalWS     : TEXCOORD3;
    float3 positionWS   : TEXCOORD4;
#ifdef ENABLE_NORMAL_MAP
    float3 tangentWS    : TEXCOORD5;
    float3 binormalWS   : TEXCOORD6;
#endif
};

// Vertex shader
Varyings vert(Attributes input)
{
    Varyings output;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    
    // Eye direction vector
    output.eyeDir = normalize(GetCameraPositionWS() - output.positionWS);
    
    // Note: WorldSpaceLightDir is dynamic in URP. We get main light per pixel or vertex.
    Light mainLight = GetMainLight();
    output.lightDir = normalize(mainLight.direction);
    
#ifdef ENABLE_NORMAL_MAP    
    output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
    output.binormalWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
#endif

    return output;
}

// Overlay blend
inline half3 GetOverlayColor(half3 inUpper, half3 inLower)
{
    half3 oneMinusLower = half3(1.0, 1.0, 1.0) - inLower;
    half3 valUnit = 2.0 * oneMinusLower;
    half3 minValue = 2.0 * inLower - half3(1.0, 1.0, 1.0);
    half3 greaterResult = inUpper * valUnit + minValue;
    half3 lowerResult = 2.0 * inLower * inUpper;
    half3 lerpVals = round(inLower);
    return lerp(lowerResult, greaterResult, lerpVals);
}

#ifdef ENABLE_NORMAL_MAP
inline half3 GetNormalFromMap(Varyings input)
{
    half3 packedNormal = SAMPLE_TEXTURE2D(_NormalMapSampler, sampler_NormalMapSampler, input.uv).xyz;
    half3 normalVec = normalize(packedNormal * 2.0 - 1.0);
    half3x3 localToWorldTranspose = half3x3(input.tangentWS, input.binormalWS, input.normalWS);
    return normalize(mul(normalVec, localToWorldTranspose));
}
#endif

// Lit function recreation since URP doesn't natively have CG's lit()
half4 UnityChan_Lit(half NdotL, half NdotH, half specPower)
{
    half ambient = 1.0;
    half diffuse = max(0.0, NdotL);
    half specular = (NdotL > 0.0) ? pow(max(0.0, NdotH), specPower) : 0.0;
    return half4(ambient, diffuse, specular, 1.0);
}

// Fragment shader
half4 frag(Varyings input) : SV_Target
{
    half4 diffSamplerColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

#ifdef ENABLE_NORMAL_MAP
    half3 normalVec = GetNormalFromMap(input);
#else
    half3 normalVec = normalize(input.normalWS);
#endif

    // Falloff
    half normalDotEye = dot(normalVec, input.eyeDir);
    half falloffU = clamp(1.0 - abs(normalDotEye), 0.02, 0.98);
    half4 falloffSamplerColor = FALLOFF_POWER * SAMPLE_TEXTURE2D(_FalloffSampler, sampler_FalloffSampler, float2(falloffU, 0.25));
    half3 shadowColor = diffSamplerColor.rgb * diffSamplerColor.rgb;
    half3 combinedColor = lerp(diffSamplerColor.rgb, shadowColor, falloffSamplerColor.r);
    combinedColor *= (1.0 + falloffSamplerColor.rgb * falloffSamplerColor.a);

    // Specular
    half4 reflectionMaskColor = SAMPLE_TEXTURE2D(_SpecularReflectionSampler, sampler_SpecularReflectionSampler, input.uv);
    half specularDot = dot(normalVec, input.eyeDir);
    half4 lighting = UnityChan_Lit(normalDotEye, specularDot, _SpecularPower);
    half3 specularColor = saturate(lighting.z) * reflectionMaskColor.rgb * diffSamplerColor.rgb;
    combinedColor += specularColor;
    
    // Reflection
    half3 reflectVector = reflect(-input.eyeDir, normalVec).xzy;
    half2 sphereMapCoords = 0.5 * (half2(1.0, 1.0) + reflectVector.xy);
    half3 reflectColor = SAMPLE_TEXTURE2D(_EnvMapSampler, sampler_EnvMapSampler, sphereMapCoords).rgb;
    reflectColor = GetOverlayColor(reflectColor, combinedColor);

    combinedColor = lerp(combinedColor, reflectColor, reflectionMaskColor.a);
    
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
    half4 lightColor0 = half4(mainLight.color, 1.0);
    combinedColor *= _Color.rgb * lightColor0.rgb;
    half opacity = diffSamplerColor.a * _Color.a * lightColor0.a;

#ifdef ENABLE_CAST_SHADOWS
    shadowColor = _ShadowColor.rgb * combinedColor;
    half attenuation = mainLight.shadowAttenuation;
    combinedColor = lerp(shadowColor, combinedColor, attenuation);
#endif

    // Rimlight
    half rimlightDot = saturate(0.5 * (dot(normalVec, input.lightDir) + 1.0));
    falloffU = saturate(rimlightDot * falloffU);
    falloffU = SAMPLE_TEXTURE2D(_RimLightSampler, sampler_RimLightSampler, float2(falloffU, 0.25)).r;
    half3 lightColor = diffSamplerColor.rgb;
    combinedColor += falloffU * lightColor;

    return half4(combinedColor, opacity);
}

#endif
