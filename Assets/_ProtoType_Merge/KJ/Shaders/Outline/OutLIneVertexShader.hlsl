#include "Shared.fxh"

PS_OUTLINE_INPUT main( VS_OUTLINE_INPUT input )
{
    float OutlineThickness = 2.0f;
    float4 OutlineColor = float4(1.0, 0.0, 0.0, 1.0);
    
    PS_OUTLINE_INPUT Output;
    float3 Normal = normalize(input.Normal);
    //float3 viewNormal = mul(Normal, (float3x3) World);
    //viewNormal = mul(Normal, (float3x3) View);
    float3 worldPos = mul(input.Position, World);
    float3 viewDir = normalize(camerapos - worldPos);
    float edgefactor = 1 - abs(dot(viewDir, Normal));
    
    float4 expandedPos = input.Position + float4(Normal * edgefactor, 0);
    //float4 expandedPos = input.Position + float4(Normal.xyz*2.0, 0);
    
    Output.Position = mul(expandedPos, World);
    Output.Position = mul(Output.Position, View);
    Output.Position = mul(Output.Position, Projection);
    
    Output.Color = OutlineColor;
    
	return Output;
}