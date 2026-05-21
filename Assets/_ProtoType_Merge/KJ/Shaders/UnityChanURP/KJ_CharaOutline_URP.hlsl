// Unity Chan Original Shader - URP HLSL Port
// Core Outline Module

#ifndef KJ_CHARA_OUTLINE_URP_INCLUDED
#define KJ_CHARA_OUTLINE_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


// Textures
TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);

#define INV_EDGE_THICKNESS_DIVISOR 0.00285
#define SATURATION_FACTOR 0.6
#define BRIGHTNESS_FACTOR 0.8

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
};

// Vertex shader
Varyings vert(Attributes input)
{
    Varyings output;
    output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;

    float4 projSpacePos = TransformObjectToHClip(input.positionOS.xyz);
    
    // Original formula:
    // half4 projSpaceNormal = normalize( UnityObjectToClipPos( half4( v.normal, 0 ) ) );
    // half4 scaledNormal = _EdgeThickness * INV_EDGE_THICKNESS_DIVISOR * projSpaceNormal; 
    
    // In URP we transform the normal to clip space properly using normal matrix or world to clip.
    // Instead of using ClipPos on a normal which is technically wrong but works as a hack:
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 hclipDir = TransformWorldToHClipDir(normalWS, false);
    float4 projSpaceNormal = float4(normalize(hclipDir), 0.0);
    
    // The Unity Chan outline logic extrudes normals in clip space.
    float4 scaledNormal = _EdgeThickness * INV_EDGE_THICKNESS_DIVISOR * projSpaceNormal;
    scaledNormal.z += 0.00001;
    
    output.positionCS = projSpacePos + scaledNormal;

    return output;
}

// Fragment shader
half4 frag(Varyings input) : SV_Target
{
    half4 diffuseMapColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

    half maxChan = max(max(diffuseMapColor.r, diffuseMapColor.g), diffuseMapColor.b);
    half4 newMapColor = diffuseMapColor;

    maxChan -= (1.0 / 255.0);
    half3 lerpVals = saturate((newMapColor.rgb - half3(maxChan, maxChan, maxChan)) * 255.0);
    newMapColor.rgb = lerp(SATURATION_FACTOR * newMapColor.rgb, newMapColor.rgb, lerpVals);
    
    Light mainLight = GetMainLight();
    half4 lightColor0 = half4(mainLight.color, 1.0);
    
    return half4(BRIGHTNESS_FACTOR * newMapColor.rgb * diffuseMapColor.rgb, diffuseMapColor.a) * _Color * lightColor0;
}

#endif
