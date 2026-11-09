Shader "JC/Battle/CellBorderGeometry"
{
    Properties
    {
        _BaseMap ("Fill Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Size ("Cell Size", Vector) = (3.3,3.3,0,0)
        _Inset ("Inset", Float) = 0
        _Thickness ("Thickness", Float) = .045
        _CornerCut ("Corner Cut", Float) = .25
        // JC | 2026-11-04 | 트리거와 도로·건물의 깊이를 비교해 가려진 부분의 불투명도를 따로 적용합니다.
        _TriggerDepthKind ("Trigger Depth Kind", Float) = 0
        // JC | 2026-11-05 | 트리거 윤곽선의 동률 깊이에서 사용할 고정 표시순서입니다.
        _TriggerBorderOrder ("Trigger Border Order", Float) = 0
        _TriggerFillSidewalkOpacity ("Fill Sidewalk Occlusion Multiplier", Float) = .25
        _TriggerBuildingOpacity ("Building Occlusion Multiplier", Float) = .15
        _TriggerSidewalkOpacity ("Sidewalk Occlusion Multiplier", Float) = .05
        _IsFill ("Interior", Float) = 0
        _FillOcclusionHeight ("Fill Occlusion World Height", Float) = 0.01
        _ProjectFillToGround ("Project Fill Occlusion To Ground", Float) = 1
        _ZWrite ("Depth Write", Float) = 1
        _ColorMask ("Color Write Mask", Float) = 15
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        _Cull ("Cull Mode", Float) = 2
        _SweepEnabled ("Sweep Enabled", Float) = 1
        _SweepColor ("Sweep Tint", Color) = (1,1,1,1)
        _SweepIntensity ("Sweep Intensity", Float) = 2
        _SweepWidth ("Sweep Width", Float) = .12
        _SweepSoftness ("Sweep Softness", Float) = .5
        _SweepTilt ("Sweep Tilt", Float) = 18
        _SweepDuration ("Sweep Duration", Float) = .8
        _SweepInterval ("Sweep Interval", Float) = 1.5
        _SweepReverse ("Sweep Reverse", Float) = 0
        _SweepTime ("Sweep Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _Size;
                half4 _BaseColor, _SweepColor;
                float _TriggerDepthKind, _TriggerBorderOrder;
                float _TriggerBuildingOpacity, _TriggerSidewalkOpacity, _TriggerFillSidewalkOpacity;
                float _Inset, _Thickness, _IsFill, _CornerCut, _FillOcclusionHeight, _ProjectFillToGround;
                float _SweepEnabled, _SweepIntensity, _SweepWidth, _SweepSoftness, _SweepTilt;
                float _SweepDuration, _SweepInterval, _SweepReverse, _SweepTime;
            CBUFFER_END
            TEXTURE2D(_JcTriggerBorderDepth); SAMPLER(sampler_JcTriggerBorderDepth);
            TEXTURE2D(_JcTriggerBorderOwners); SAMPLER(sampler_JcTriggerBorderOwners);
            TEXTURE2D(_JcTriggerOccluderDepth); SAMPLER(sampler_JcTriggerOccluderDepth);
            // JC | 2026-11-09 | 부유 채움보다 낮은 발·소품도 원래 메시 실루엣을 보존합니다.
            TEXTURE2D(_JcTriggerMeshDepth); SAMPLER(sampler_JcTriggerMeshDepth);
            float4x4 _JcTriggerDepthVP, _JcTriggerDepthView;
            float _JcTriggerDepthActive, _JcTriggerDepthUvFlip;
            float _TriggerSurfaceKind;
            float4x4 _TriggerSurfaceRoot;
            // JC | 2026-11-05 | 깊이 수집·겹침 선택에서 같은 좌표 및 허용 오차를 사용합니다.
            float2 TriggerDepthUv(float3 world)
            {
                float4 projected = mul(_JcTriggerDepthVP, float4(world, 1));
                float2 uv = projected.xy / projected.w * .5 + .5;
                if (_JcTriggerDepthUvFlip > .5) uv.y = 1 - uv.y;
                return uv;
            }
            float TriggerDepthTolerance(float eyeDepth) { return max(.0001, eyeDepth * .000002); }
            struct DepthAttributes { float4 positionOS:POSITION; };
            struct DepthVaryings { float4 positionCS:SV_POSITION; float eyeDepth:TEXCOORD0; float3 world:TEXCOORD1; };
            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                float4 world = mul(unity_ObjectToWorld, float4(input.positionOS.xyz, 1));
                output.positionCS = mul(_JcTriggerDepthVP, world);
                output.eyeDepth = -mul(_JcTriggerDepthView, world).z;
                output.world = world.xyz;
                return output;
            }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ColorMask [_ColorMask]
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 cellUV:TEXCOORD1; half fog:TEXCOORD2; half shade:TEXCOORD3; float3 positionWS:TEXCOORD4; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.cellUV=v.positionOS.xz/max(_Size.xy,float2(.001,.001))+.5;
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                float3 normal=TransformObjectToWorldNormal(v.normalOS);
                o.shade=_IsFill>.5?1:saturate(.6+.4*saturate(normal.y)+.12*dot(normal,float3(-.6,0,-.8)));
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings v, out float depth:SV_Depth):SV_Target
            {
                depth=v.positionCS.z;
                if (_IsFill>.5 && _ProjectFillToGround>.5)
                {
                    // 표시는 떠 있는 위치를 유지하고, 같은 화면 픽셀의 바닥 가까이에서 가림을 판정합니다.
                    float3 ray=unity_OrthoParams.w>.5 ? -UNITY_MATRIX_I_V._m02_m12_m22 : v.positionWS-_WorldSpaceCameraPos;
                    if (abs(ray.y)>0.00001)
                    {
                        float distanceToFloor=(_FillOcclusionHeight-v.positionWS.y)/ray.y;
                        float4 floorCS=TransformWorldToHClip(v.positionWS+ray*max(0,distanceToFloor));
                        float floorDepth=floorCS.z/floorCS.w;
                        if (UNITY_NEAR_CLIP_VALUE < 0) floorDepth=floorDepth*.5+.5;
                        #if UNITY_REVERSED_Z
                            depth=min(depth,saturate(floorDepth));
                        #else
                            depth=max(depth,saturate(floorDepth));
                        #endif
                    }
                }
                float occludedAlpha=1;
                if (_TriggerDepthKind>.5 && _JcTriggerDepthActive>.5)
                {
                    float2 uv=TriggerDepthUv(v.positionWS);
                    float2 nearest=SAMPLE_TEXTURE2D_LOD(_JcTriggerBorderDepth,sampler_JcTriggerBorderDepth,uv,0).rg;
                    float eyeDepth=-mul(_JcTriggerDepthView,float4(v.positionWS,1)).z;
                    // 본체는 자기 표면끼리만 비교하고, 보조선은 본체와 두 보조선 모두와 비교합니다.
                    float occluder=_TriggerDepthKind<1.5?nearest.r:nearest.g;
                    if (_TriggerDepthKind<2.5)
                    {
                        float tolerance=TriggerDepthTolerance(eyeDepth);
                        clip(occluder+tolerance-eyeDepth);
                        // JC | 2026-11-05 | 동률인 선은 한 번만 합성합니다. 앞쪽의 반투명 보조선 너머 본체를 보이는 기존 표현은 유지합니다.
                        float2 owners=SAMPLE_TEXTURE2D_LOD(_JcTriggerBorderOwners,sampler_JcTriggerBorderOwners,uv,0).rg;
                        float winner=_TriggerDepthKind<1.5 && eyeDepth>nearest.g+tolerance ? owners.r : owners.g;
                        clip(.25-abs(winner-_TriggerBorderOrder));
                    }
                    // JC | 2026-11-04 | 실제로 픽셀을 가리는 표면만 분류합니다. 캐릭터 뒤의 건물을 잘못 투과하지 않습니다.
                    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(v.positionCS));
                    float sceneEye=unity_OrthoParams.w>.5?LinearDepthToEyeDepth(raw):LinearEyeDepth(raw,_ZBufferParams);
                    if (_TriggerDepthKind>2.5)
                    {
                        float objectEye=SAMPLE_TEXTURE2D_LOD(_JcTriggerMeshDepth,sampler_JcTriggerMeshDepth,uv,0).g;
                        // JC | 2026-11-09 | 장면 표면보다 앞이거나 같은 내부 메시를 보호합니다. 투명 소품/깊이 샘플 경계도 포함하되 건물·인도 뒤에 숨은 메시의 구멍은 만들지 않습니다.
                        if (objectEye<65000 && objectEye<=sceneEye+max(.004,sceneEye*.00003)) clip(-1);
                    }
                    if (sceneEye+max(.002,eyeDepth*.00002)<eyeDepth)
                    {
                        float4 surface=SAMPLE_TEXTURE2D_LOD(_JcTriggerOccluderDepth,sampler_JcTriggerOccluderDepth,uv,0);
                        float tolerance=max(.004,sceneEye*.00003);
                        float4 match=1-step(tolerance,abs(surface-sceneEye));
                        // 계단은 정상 선명도, 난간은 기존 가림, 건물·인도는 각각의 약한 알파를 사용합니다.
                        // JC | 2026-11-04 | 채움은 인도만 투과하며 건물/계단/캐릭터 위로 출력하지 않습니다.
                        occludedAlpha=_TriggerDepthKind>2.5 ? (match.g>.5 && max(match.r,max(match.b,match.a))<.5 ? _TriggerFillSidewalkOpacity : 0) :
                            match.a>.5?0:match.b>.5?1:match.r>.5?_TriggerBuildingOpacity:match.g>.5?_TriggerSidewalkOpacity:0;
                    }
                }
                half4 result=_BaseColor;
                result.a*=occludedAlpha;
                result.rgb*=v.shade;
                if (_IsFill>.5) result*=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv);
                clip(result.a-.001);
                float duration=max(.05,_SweepDuration);
                float phase=fmod(max(0,_SweepTime),duration+max(0,_SweepInterval));
                if (_IsFill<.5 && _SweepEnabled>.5 && phase<duration)
                {
                    float angle=radians(_SweepTilt);
                    float2 axis=float2(cos(angle),sin(angle));
                    float width=max(.01,_SweepWidth);
                    float outer=width*(1+3*saturate(_SweepSoftness));
                    float extent=.5*(abs(axis.x)+abs(axis.y))+outer;
                    float t=phase/duration;
                    if (_SweepReverse>.5) t=1-t;
                    float distance=abs(dot(v.cellUV-.5,axis)-lerp(-extent,extent,t));
                    float core=1-smoothstep(width*.2,width,distance);
                    float halo=1-smoothstep(width,max(width+.001,outer),distance);
                    float glow=(core+halo*.3*saturate(_SweepSoftness))*max(0,_SweepIntensity)*_SweepColor.a;
                    result.rgb+=lerp(_BaseColor.rgb,half3(1,1,1),.45)*_SweepColor.rgb*glow;
                }
                result.rgb=MixFog(result.rgb,v.fog);
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "TriggerOccluderDepth"
            Tags { "LightMode"="JcTriggerOccluderDepth" }
            Blend One One
            BlendOp Min
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex SurfaceVert
            #pragma fragment SurfaceFrag
            struct SurfaceAttributes { float4 positionOS:POSITION; };
            struct SurfaceVaryings { float4 positionCS:SV_POSITION; float eyeDepth:TEXCOORD0; float3 root:TEXCOORD1; };
            SurfaceVaryings SurfaceVert(SurfaceAttributes input)
            {
                SurfaceVaryings output;
                float4 world=mul(unity_ObjectToWorld,float4(input.positionOS.xyz,1));
                output.positionCS=mul(_JcTriggerDepthVP,world);
                output.eyeDepth=-mul(_JcTriggerDepthView,world).z;
                output.root=mul(_TriggerSurfaceRoot,world).xyz;
                return output;
            }
            float4 SurfaceFrag(SurfaceVaryings input):SV_Target
            {
                float kind=_TriggerSurfaceKind;
                float3 p=input.root;
                // JC | 2026-11-04 | 단일 메시인 기존 협회·은행을 루트 좌표로 분류합니다. 모델/GUID/난간은 변경하지 않습니다.
                if (kind>4.5 && kind<5.5)
                {
                    float x=abs(p.x-.0015);
                    bool entrance=p.z>-.70 && p.z<-.385 && p.y>-.02 && p.y<.20;
                    kind=entrance && x>.145 && x<.24 && p.z<-.48?4:entrance && x<=.145 && p.y<.09?3:1;
                }
                else if (kind>5.5)
                    kind=p.x>-.58 && p.x<.66 && p.z>-.90 && p.z<-.49 && p.y>-.02 && p.y<.22?3:1;
                float4 value=65504;
                if (kind<1.5) value.r=input.eyeDepth;
                else if (kind<2.5) value.g=input.eyeDepth;
                else if (kind<3.5) value.b=input.eyeDepth;
                else value.a=input.eyeDepth;
                return value;
            }
            ENDHLSL
        }
        Pass
        {
            Name "TriggerAreaDepth"
            Tags { "LightMode"="JcTriggerAreaDepth" }
            Blend One One
            BlendOp Min
            ColorMask RG
            ZWrite Off
            ZTest Always
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthFrag(DepthVaryings input):SV_Target
            {
                clip(_BaseColor.a-.001);
                return float4(_TriggerDepthKind<1.5?input.eyeDepth:65504,input.eyeDepth,0,0);
            }
            ENDHLSL
        }
        // JC | 2026-11-05 | 최근접 본체와 전체 선의 표시순서를 분리해 동일 평면 겹침과 기존 반투명 가림을 함께 처리합니다.
        Pass
        {
            Name "TriggerAreaOwner"
            Tags { "LightMode"="JcTriggerAreaOwner" }
            Blend One One
            BlendOp Min
            ColorMask RG
            ZWrite Off
            ZTest Always
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment OwnerFrag
            float4 OwnerFrag(DepthVaryings input):SV_Target
            {
                clip(_BaseColor.a-.001);
                float2 nearest=SAMPLE_TEXTURE2D_LOD(_JcTriggerBorderDepth,sampler_JcTriggerBorderDepth,TriggerDepthUv(input.world),0).rg;
                float tolerance=TriggerDepthTolerance(input.eyeDepth);
                float mainOrder=_TriggerDepthKind<1.5 && input.eyeDepth<=nearest.r+tolerance ? _TriggerBorderOrder : 3.402823e+38;
                float anyOrder=input.eyeDepth<=nearest.g+tolerance ? _TriggerBorderOrder : 3.402823e+38;
                return float4(mainOrder,anyOrder,0,0);
            }
            ENDHLSL
        }
    }
}
