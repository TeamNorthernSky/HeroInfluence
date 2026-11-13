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
        // JC | 2026-11-05 | 프로필의 강화된 거칠기 조절 범위와 재질 Inspector 범위를 일치시킵니다.
        _SurfaceVariation("표면 거칠기 변화",Range(0,1))=.035
        _FloorTiles("바닥 줄눈 사용",Float)=0
        _TileSize("타일 한 변 길이",Float)=4
        _JointWidth("줄눈 폭",Float)=.025
        _JointContrast("줄눈 명암",Range(0,.5))=.12
        _JointNormal("얕은 줄눈 노멀",Range(0,.08))=.018
        _TileVariation("타일 색 변화",Range(0,.08))=.012
        [HideInInspector] _Surface("Surface",Float)=0
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _BossLocalOnly("보스 전용 조명만",Float)=0
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
            // JC | 2026-11-05 | 창문 마스크가 실제 추가광에 적용되도록 URP 쿠키 변형을 포함합니다.
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            // JC | 2026-11-12 | 보스 실내로 등록한 재질만 전용 IBL을 받습니다.
            #pragma multi_compile_local_fragment _ _JC_INDOOR_IBL_TARGET
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // JC | 2026-11-05 | 반사량과 퍼짐을 분리하기 위해 기존 URP의 스페큘러 조명 경로를 재사용합니다.
            #define _SPECULAR_SETUP 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            // JC | 2026-11-12 | 월드 차단 해제 경로도 동일 실내 IBL을 사용하며 기본 차단 경로는 별도로 합산합니다.
            #include "Assets/_ProtoType_Merge/JC/BattleTesting/JcIndoorLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "Assets/_ProtoType_Merge/JC/BattleTesting/JcBossLocalLighting.hlsl"
            // JC | 2026-11-09 | 보스 표면에만 공통 접점 음영을 적용하며 도시 재질은 등록하지 않습니다.
            #include "Assets/_ProtoType_Merge/JC/Environment/ContactShading/JcContactShading.hlsl"
            float _BossLocalOnly;
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
                // JC | 2026-11-05 | 실내 차단 모드에서는 안개/환경 반사도 밝기 하한을 만들지 않게 제외합니다.
                surface.albedo=JcApplyContactShading(surface.albedo,input.positionWS);
                if(_BossLocalOnly>.5)return JcBossLocalSurface(data,surface);
                half4 color=JcBankPbr(data,surface);
                color.rgb*=JcIndoorOcclusion(data.positionWS,data.normalWS);
                color.rgb=MixFog(color.rgb,data.fogCoord);color.a=1;return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
