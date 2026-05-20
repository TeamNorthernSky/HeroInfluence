Texture2D txDiffuse : register(t0);
TextureCube SkyBox : register(t1);
Texture2D txNormal : register(t2);
Texture2D txSpecular : register(t3);
Texture2D txAmbient : register(t4);
Texture2D txEmissive : register(t5);
Texture2D txOpacity : register(t6);
SamplerState samLinear : register(s0);


cbuffer ConstantBuffer : register(b0)
{
    matrix World;
    matrix View;
    matrix Projection;
    
    float3 vDirectionalLight;
    //float padding;
    float4 vDirectionalColor;
    
    float4 DiffuseColor;
    float4 AmbientColor;
    float4 SpecularColor;
    
    float4 DiffuseMaterial;
    float4 AmbientMaterial;
    float4 SpecularMaterial;
    
    float3 camerapos;
    float shininess;
}

//cbuffer LightBuffer : register(b1)
//{
//    float4 vDirectionalLights;
//    float4 vDirectionalColors;
    
//    float4 DiffuseColors;
//    float4 DiffuseMaterials;
     
//    float4 AmbientColors;
//    float4 AmbientMaterials;
    
//    float4 SpecularColors;
//    float4 SpecularMaterials;
    
//    float shininess2;
//    float3 padding;
//}

struct VS_INPUT
{
    float4 pos : POSITION;
    float3 normal : NORMAL;
    float2 Tex : TEXCOORD0;
    float3 tangent : TANGENT;
    float3 binormal : BINORMAL;
};

struct PS_INPUT
{
    float4 pos : SV_POSITION;
    float3 worldpos : TEXCOORD0;
    float2 Tex : TEXCOORD1;
    float3 norm : TEXCOORD2;
    float3 tangent : TEXCOORD3;
    float3 binormal : TEXCOORD4;
};

struct VS_SKYBOX_INPUT
{
    float4 pos : POSITION;
};

struct PS_SKYBOX_INPUT
{
    float4 pos : SV_Position;
    float3 Tex : TEXCOORD0;
};

struct VS_OUTLINE_INPUT
{
    float4 Position : POSITION;
    float3 Normal : NORMAL;
};

struct PS_OUTLINE_INPUT
{
    float4 Position : SV_Position;
    float4 Color : TEXCOORD0;
};