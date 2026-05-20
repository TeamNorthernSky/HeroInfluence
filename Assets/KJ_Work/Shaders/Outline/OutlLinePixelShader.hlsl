#include "Shared.fxh"

float4 main(PS_OUTLINE_INPUT input) : SV_TARGET
{
	return input.Color;
}