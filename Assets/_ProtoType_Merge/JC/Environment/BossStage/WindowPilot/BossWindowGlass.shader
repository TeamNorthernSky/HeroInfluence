Shader "JC/Environment/Boss Window Glass"
{
    Properties
    {
        _Tint ("투과 색상", Color) = (0.63,0.94,1,1)
        // JC | 2026-11-17 | 기존 프로필의 투과감은 0 기본값으로 유지하고 불투명도를 독립 조절합니다.
        _Opacity ("불투명도", Range(0,1)) = 0
        _FaceAlpha ("정면 착색량", Range(0,1)) = .05
        _EdgeAlpha ("측면 착색량", Range(0,1)) = .14
        _Thickness ("유리 두께 (월드 단위)", Range(.005,.3)) = .06
        _IOR ("굴절률", Range(1,2)) = 1.45
        _RefractionStrength ("굴절 표현 강도", Range(0,3)) = 1
        _Absorption ("착색 강도", Range(0,8)) = 2
        _ReflectionStrength ("표면 반사 강도", Range(0,1)) = .1
        _ReflectionRoughness ("표면 반사 거칠기", Range(0,1)) = .25
        // JC | 2026-11-17 | 실제 광원 하이라이트를 환경 반사와 별도 제어합니다.
        _GlossStrength ("광택 강도", Range(0,4)) = .35
        _GlossSmoothness ("광택 선명도", Range(0,1)) = .75
        _GlossTint ("광택 색상", Color) = (1,1,1,1)
        // JC | 2026-11-17 | 실광/IBL과 독립적인 유리 표현이며 창틀 재질에는 적용하지 않습니다.
        _HighlightStrength ("연출 하이라이트 강도", Range(0,2)) = .18
        _HighlightWidth ("연출 하이라이트 폭", Range(.01,.5)) = .12
        _HighlightAngle ("연출 하이라이트 각도", Range(-90,90)) = -30
        _HighlightSoftness ("연출 하이라이트 부드러움", Range(.05,1)) = .8
        _HighlightTint ("연출 하이라이트 색", Color) = (.78,.88,1,1)
        _RefractionEdgeFade ("창 가장자리 굴절 완화", Range(.005,.25)) = .06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            // JC | 2026-11-05 | 입체의 앞면만 그려 중복 착색을 막고, 흰색 덧칠 대신 배경 투과·흡수를 계산합니다.
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One Zero
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // JC | 2026-11-17 | 보스 Forward+의 창문광·추가광도 광택 하이라이트에 반영합니다.
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _LIGHT_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            // JC | 2026-11-05 | 보스와 해당 무대를 보는 SceneView는 각각 자기 카메라의 원본 해상도 투과색을 사용합니다.
            TEXTURE2D_X(_JcBossGlassColor); SAMPLER(sampler_JcBossGlassColor);
            float _JcBossGlassColorReady;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _GlossTint, _HighlightTint;
                float _HighlightStrength, _HighlightWidth, _HighlightAngle, _HighlightSoftness, _RefractionEdgeFade;
                half _GlossStrength, _GlossSmoothness;
                float _Thickness, _IOR, _RefractionStrength, _Absorption;
                half _FaceAlpha, _EdgeAlpha, _ReflectionStrength, _ReflectionRoughness, _Opacity;
            CBUFFER_END
            // JC | 2026-11-17 | 판의 UV와 앞/뒷면 마스크로 창틀 경계 및 얇은 옆면의 굴절/하이라이트를 제한합니다.
            struct Attributes { float4 vertex:POSITION; float3 normal:NORMAL; float3 pane:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; float3 pane:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.vertex.xyz);
                o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.pane=v.pane;
                return o;
            }
            // JC | 2026-11-17 | 시선·법선·실제 광원 방향으로 부드러운 반짝임을 계산하며 광량 감쇠를 따릅니다.
            half3 GlassGloss(Light light,float3 n,float3 v)
            {
                #ifdef _LIGHT_LAYERS
                if(!IsMatchingLightLayer(light.layerMask,GetMeshRenderingLayer())) return 0;
                #endif
                float3 h=SafeNormalize(light.direction+v);
                float power=exp2(3+8*saturate(_GlossSmoothness));
                return light.color*(light.distanceAttenuation*light.shadowAttenuation)*pow(saturate(dot(n,h)),power)*saturate(dot(n,light.direction));
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal), v=GetWorldSpaceNormalizeViewDir(i.world);
                float facing=saturate(dot(n,v));
                float3 transmitted=refract(-v,n,rcp(max(1,_IOR)));
                // JC | 2026-11-05 | 평행 유리판의 입사/출사 변위를 두께로 근사합니다. 굴절률 1이면 변위가 0입니다.
                float3 displacement=_Thickness*(transmitted/max(.1,abs(dot(transmitted,n)))+v/max(.1,facing));
                float2 uv=GetNormalizedScreenSpaceUV(i.pos);
                float4 shifted=TransformWorldToHClip(i.world+displacement*_RefractionStrength);
                float4 screen=ComputeScreenPos(shifted);
                float2 refractedUV=screen.xy/max(.0001,screen.w);
                // JC | 2026-11-17 | 화면 경계뿐 아니라 실제 유리판 경계에서 변위를 연속적으로 0으로 줄입니다.
                float paneEdge=min(min(i.pane.x,i.pane.y),min(1-i.pane.x,1-i.pane.y));
                float paneFade=smoothstep(0,max(.005,_RefractionEdgeFade),paneEdge)*saturate(i.pane.z);
                float edgeFade=saturate(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y))*40)*paneFade;
                refractedUV=lerp(uv,refractedUV,edgeFade);
                // JC | 2026-11-05 | 화면 밖 및 유리 앞의 기둥/캐릭터가 굴절에 끌려 들어오는 것을 제한합니다.
                float sceneDepth=LinearEyeDepth(SampleSceneDepth(saturate(refractedUV)),_ZBufferParams);
                float glassDepth=-TransformWorldToView(i.world).z;
                // JC | 2026-11-17 | 창틀 깊이에서 원본 UV로 갑자기 전환하지 않고 여유 깊이에 따라 부드럽게 복귀합니다.
                float depthFade=smoothstep(.015,.20,sceneDepth-glassDepth);
                refractedUV=lerp(uv,saturate(refractedUV),depthFade);
                half3 background=_JcBossGlassColorReady>.5 ? SAMPLE_TEXTURE2D_X(_JcBossGlassColor,sampler_JcBossGlassColor,UnityStereoTransformScreenSpaceTex(refractedUV)).rgb : SampleSceneColor(refractedUV);
                half rim=(1-facing)*(1-facing);
                float opticalPath=_Thickness/max(.25,abs(dot(transmitted,n)));
                half density=max(0,_Absorption)*lerp(_FaceAlpha,_EdgeAlpha,rim)*opticalPath/.06;
                half3 transmission=exp(-density*(1-saturate(_Tint.rgb)));
                half f0=(_IOR-1)/(_IOR+1);f0*=f0;
                half fresnel=(f0+(1-f0)*pow(1-facing,5))*_ReflectionStrength;
                half3 reflection=GlossyEnvironmentReflection(reflect(-v,n),i.world,_ReflectionRoughness,1,uv);
                // JC | 2026-11-17 | 표면 반사는 유지하면서 배경 투과색을 틴트색으로 가립니다. Blend One Zero에서도 1이면 배경이 비치지 않습니다.
                half3 glassColor=lerp(background*transmission,saturate(_Tint.rgb),saturate(_Opacity));
                half3 gloss=GlassGloss(GetMainLight(),n,v);
                InputData inputData=(InputData)0;
                inputData.positionWS=i.world;inputData.normalizedScreenSpaceUV=uv;
                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_FORWARD_PLUS
                    UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                        gloss+=GlassGloss(GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1)),n,v);
                    #endif
                    uint lightCount=GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(lightCount)
                        gloss+=GlassGloss(GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1)),n,v);
                    LIGHT_LOOP_END
                #endif
                // JC | 2026-11-17 | 광원이 비추지 않아도 읽히는 두 개의 부드러운 띠입니다. IBL/실광 반사 값은 보존합니다.
                float angle=radians(_HighlightAngle);
                float band=dot(i.pane.xy-.5,float2(cos(angle),sin(angle)))+(v.x+v.z)*.10;
                float width=max(.01,_HighlightWidth), feather=max(.002,width*_HighlightSoftness);
                float highlight=1-smoothstep(width-feather,width,abs(band+.22));
                highlight+=(1-smoothstep(width*.45-feather*.4,width*.45,abs(band-.23)))*.45;
                highlight*=smoothstep(0,.025,paneEdge)*saturate(i.pane.z);
                return half4(lerp(glassColor,reflection,saturate(fresnel))+gloss*_GlossTint.rgb*_GlossStrength+
                    _HighlightTint.rgb*(_HighlightStrength*highlight),1);
            }
            ENDHLSL
        }
    }
}
