Shader "JC/Environment/Boss Interior Surface"
{
    Properties
    {
        _BaseMap("원본 색상 무늬",2D)="white"{} _BaseColor("원본 색상",Color)=(1,1,1,1)
        _BumpMap("기본 노멀",2D)="bump"{} _BumpScale("기본 노멀 강도",Float)=0
        _MetallicGlossMap("매끄러움 맵",2D)="white"{} _SpecGlossMap("반사 맵",2D)="white"{}
        _Smoothness("매끄러움",Range(0,1))=.18 _Metallic("금속",Float)=0
        _SpecColor("반사 색",Color)=(.02,.02,.02,1)
        _OcclusionMap("음영 맵",2D)="white"{} _OcclusionStrength("음영",Float)=1
        _EmissionMap("발광 맵",2D)="white"{} _EmissionColor("발광",Color)=(0,0,0,0)
        _SurfaceVariation("표면 거칠기 변화",Range(0,.2))=.035
        _FloorTiles("바닥 줄눈 사용",Float)=0
        _TileSize("타일 한 변 길이",Float)=4
        _JointWidth("줄눈 폭",Float)=.025
        _JointContrast("줄눈 명암",Range(0,.5))=.12
        _JointNormal("얕은 줄눈 노멀",Range(0,.08))=.018
        _TileVariation("타일 색 변화",Range(0,.08))=.012
        [HideInInspector] _Surface("Surface",Float)=0
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // JC | 2026-11-05 | 반사량과 퍼짐을 분리하기 위해 기존 URP의 스페큘러 조명 경로를 재사용합니다.
            #define _SPECULAR_SETUP 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            float _SurfaceVariation,_FloorTiles,_TileSize,_JointWidth,_JointContrast,_JointNormal,_TileVariation;
            float4x4 _WorldToStage;
            float4 _StageScale;
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float SoftNoise(float2 p)
            {
                float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            half4 SurfaceFragment(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;InitializeStandardLitSurfaceData(input.uv,surface);
                float3 local=mul(_WorldToStage,float4(input.positionWS,1)).xyz*_StageScale.xyz;
                float noise=SoftNoise(local.xz*.7+local.y*.3)-.5;
                surface.smoothness=saturate(_Smoothness+noise*_SurfaceVariation);
                InputData data;InitializeInputData(input,surface.normalTS,data);
                if(_FloorTiles>.5 && data.normalWS.y>.8)
                {
                    // JC | 2026-11-05 | 넓은 타일을 월드 길이로 배치하고 깊이 변위 없이 얕은 줄눈만 표현합니다. 전투 셀/충돌은 그대로입니다.
                    float size=max(.5,_TileSize);float2 uv=local.xz/size;
                    float2 signedDistance=(frac(uv+.5)-.5)*size;
                    float2 distance=abs(signedDistance);
                    float2 aa=max(fwidth(local.xz),.002);
                    float halfWidth=max(.002,_JointWidth)*.5;
                    float2 jointMask=1-smoothstep(halfWidth,halfWidth+aa,distance);
                    float joint=max(jointMask.x,jointMask.y);
                    float tile=Hash(floor(uv));
                    surface.albedo*=1+(tile-.5)*_TileVariation;
                    surface.albedo*=1-joint*_JointContrast;
                    surface.smoothness=saturate(surface.smoothness-joint*.06);
                    float2 slope=sign(signedDistance)*jointMask*(1-jointMask)*4*_JointNormal;
                    float3 right=normalize(float3(_WorldToStage[0][0],_WorldToStage[0][1],_WorldToStage[0][2]));
                    float3 forward=normalize(float3(_WorldToStage[2][0],_WorldToStage[2][1],_WorldToStage[2][2]));
                    data.normalWS=normalize(data.normalWS-right*slope.x-forward*slope.y);
                }
                // JC | 2026-11-05 | 원본 색·큰 면을 보존하면서 약한 반사, 기존 그림자와 추가 라이트를 그대로 받습니다.
                half4 color=UniversalFragmentPBR(data,surface);
                color.rgb=MixFog(color.rgb,data.fogCoord);color.a=1;return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
