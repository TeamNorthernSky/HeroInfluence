Shader "URP/UnityChan/Eye - Transparent"
{
    Properties
    {
		_Color ("Main Color", Color) = (1, 1, 1, 1)
		_ShadowColor ("Shadow Color", Color) = (0.8, 0.8, 1, 1)
		
		_MainTex ("Diffuse", 2D) = "white" {}
		_FalloffSampler ("Falloff Control", 2D) = "white" {}
		_RimLightSampler ("RimLight Control", 2D) = "white" {}
	}
    SubShader
    {
        Tags
        {
            "Queue"="Geometry+1" // Transparent+1"
			"IgnoreProjector"="True"
			"RenderType"="Overlay"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Cull Back
            ZTest LEqual
            
            Blend SrcAlpha OneMinusSrcAlpha, One One

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
        float4 _MainTex_ST;
        float4 _FalloffSampler_ST;
        float4 _RimLightSampler_ST;
        CBUFFER_END

            #include "KJ_CharaSkin_URP.hlsl"
            ENDHLSL
        }

        
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"

    }
}
