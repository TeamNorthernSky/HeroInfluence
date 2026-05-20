Shader "URP/UnityChan/Hair - Double-sided"
{
    Properties
    {
		_Color ("Main Color", Color) = (1, 1, 1, 1)
		_ShadowColor ("Shadow Color", Color) = (0.8, 0.8, 1, 1)
		_SpecularPower ("Specular Power", Float) = 20
		_EdgeThickness ("Outline Thickness", Float) = 1
		
		_MainTex ("Diffuse", 2D) = "white" {}
		_FalloffSampler ("Falloff Control", 2D) = "white" {}
		_RimLightSampler ("RimLight Control", 2D) = "white" {}
		_SpecularReflectionSampler ("Specular / Reflection Mask", 2D) = "white" {}
		_EnvMapSampler ("Environment Map", 2D) = "" {} 
		_NormalMapSampler ("Normal Map", 2D) = "" {} 
	}
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
			"Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half4 _ShadowColor;
        float _SpecularPower;
        float _EdgeThickness;
        float4 _MainTex_ST;
        float4 _FalloffSampler_ST;
        float4 _RimLightSampler_ST;
        float4 _SpecularReflectionSampler_ST;
        float4 _EnvMapSampler_ST;
        float4 _NormalMapSampler_ST;
        CBUFFER_END

            #include "KJ_CharaMain_URP.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            
            Cull Front
            ZTest Less

            HLSLPROGRAM
            #pragma target 2.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half4 _ShadowColor;
        float _SpecularPower;
        float _EdgeThickness;
        float4 _MainTex_ST;
        float4 _FalloffSampler_ST;
        float4 _RimLightSampler_ST;
        float4 _SpecularReflectionSampler_ST;
        float4 _EnvMapSampler_ST;
        float4 _NormalMapSampler_ST;
        CBUFFER_END

            #include "KJ_CharaOutline_URP.hlsl"
            ENDHLSL
        }

        
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"

    }
}
