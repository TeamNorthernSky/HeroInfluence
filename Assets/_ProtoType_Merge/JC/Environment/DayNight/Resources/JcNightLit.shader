Shader "JC/Environment/Night Lit"

{

    Properties

    {

        _JcWindowMask("창문 마스크 (B)",2D)="black"{}

        _JcSpecialWindow("특수 건물 창문 면",Float)=0
        _JcCombatOverlay("전투 범위 야간 보정",Float)=0
        _JcHumanoid("인간형 야간 보정",Float)=0

        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}

        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)

        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0

        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0

        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005

        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0

        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)

        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}

        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0

        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}

        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0

        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0

        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        _Surface("__surface", Float) = 0.0

        _Blend("__blend", Float) = 0.0

        _Cull("__cull", Float) = 2.0

        [ToggleUI] _AlphaClip("__clip", Float) = 0.0

        [HideInInspector] _SrcBlend("__src", Float) = 1.0

        [HideInInspector] _DstBlend("__dst", Float) = 0.0

        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0

        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0

        [HideInInspector] _ZWrite("__zw", Float) = 1.0

        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0

        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0

        _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}

        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)

        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0

        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0

        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}

        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}

        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

    }

    SubShader

    {

        Tags

        {

            "RenderType" = "Opaque"

            "RenderPipeline" = "UniversalPipeline"

            "UniversalMaterialType" = "Lit"

            "IgnoreProjector" = "True"

        }

        LOD 300

        Pass

        {

            Name "ForwardLit"

            Tags

            {

                "LightMode" = "UniversalForwardOnly"

            }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]

            ZWrite[_ZWrite]

            Cull[_Cull]

            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM

            #pragma target 2.0

            #pragma vertex LitPassVertex

            #pragma fragment LitPassFragment

            #pragma shader_feature_local _NORMALMAP

            #pragma shader_feature_local _PARALLAXMAP

            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT

            #pragma shader_feature_local_fragment _ALPHATEST_ON

            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON

            #pragma shader_feature_local_fragment _EMISSION

            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP

            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma shader_feature_local_fragment _OCCLUSIONMAP

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF

            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX

            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING

            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma multi_compile _ _LIGHT_LAYERS

            #pragma multi_compile _ _FORWARD_PLUS

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING

            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #pragma multi_compile _ DIRLIGHTMAP_COMBINED

            #pragma multi_compile _ LIGHTMAP_ON

            #pragma multi_compile _ DYNAMICLIGHTMAP_ON

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #pragma multi_compile_fog

            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma multi_compile_instancing

            #pragma instancing_options renderinglayer

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"

            TEXTURE2D(_JcWindowMask); SAMPLER(sampler_JcWindowMask);

            float _JcHumanoid, _JcCombatOverlay, _JcSpecialWindow;

            float4 _JcWindowLight, _JcWindowTint, _JcDayNight, _JcNightReadability;

            void JcNightSurface(float2 uv, out SurfaceData surfaceData)

            {

                float specialWindow=_JcSpecialWindow*step(1,uv.x);
                uv.x-=specialWindow;
                InitializeStandardLitSurfaceData(uv,surfaceData);

                float window=max(specialWindow,SAMPLE_TEXTURE2D(_JcWindowMask,sampler_JcWindowMask,uv).b)*saturate(_JcWindowLight.x);

                half3 originalWindow=surfaceData.albedo;
                half luminance=dot(originalWindow,half3(.2126,.7152,.0722));
                originalWindow=max(0,lerp(luminance.xxx,originalWindow,1.15));
                originalWindow/=max(.001,max(originalWindow.r,max(originalWindow.g,originalWindow.b)));
                // White 65%, original window hue 20%, soft orange 15%.
                half3 litWindow=_JcWindowTint.rgb*.65+originalWindow*.20+half3(1,.72,.42)*.15;
                // Preserve peak brightness and the existing emission intensity.
                litWindow*=max(_JcWindowTint.r,max(_JcWindowTint.g,_JcWindowTint.b))/max(.001,max(litWindow.r,max(litWindow.g,litWindow.b)));
                surfaceData.albedo=lerp(surfaceData.albedo,litWindow,window*.7);
                surfaceData.emission+=litWindow*window*_JcWindowLight.y;

                // Red encounter cells only. Preserve surface alpha and every non-combat overlay.
                float red=saturate((surfaceData.albedo.r-max(surfaceData.albedo.g,surfaceData.albedo.b))*4);
                surfaceData.emission+=surfaceData.albedo*_JcCombatOverlay*red*_JcDayNight.x*(1-saturate(_JcDayNight.y))*.22;
                float characterFill = _JcHumanoid > 2.5 ? _JcNightReadability.w : (_JcHumanoid > 1.5 ? _JcNightReadability.z : _JcNightReadability.x);
                // Dark enemy clothing needs a small neutral fill as well as albedo-scaled light.
                // Keep the party/NPC response unchanged and preserve detail between dark texels.
                half peak=max(surfaceData.albedo.r,max(surfaceData.albedo.g,surfaceData.albedo.b));
                half3 fillColor=surfaceData.albedo+step(2.5,_JcHumanoid)*.06*(1-saturate(peak));
                surfaceData.emission+=fillColor*step(.5,_JcHumanoid)*_JcDayNight.x*(1-saturate(_JcDayNight.y))*characterFill;

            }

            #define InitializeStandardLitSurfaceData JcNightSurface

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            ENDHLSL

        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"

        UsePass "Universal Render Pipeline/Lit/DepthOnly"

        UsePass "Universal Render Pipeline/Lit/DepthNormals"

        UsePass "Universal Render Pipeline/Lit/Meta"

    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"

}
