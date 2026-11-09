Shader "JC/Environment/Boss Window Glass"
{
    Properties
    {
        _Tint ("투과 색상", Color) = (0.63,0.94,1,1)
        _FaceAlpha ("정면 착색량", Range(0,1)) = .05
        _EdgeAlpha ("측면 착색량", Range(0,1)) = .14
        _Thickness ("유리 두께 (월드 단위)", Range(.005,.3)) = .06
        _IOR ("굴절률", Range(1,2)) = 1.45
        _RefractionStrength ("굴절 표현 강도", Range(0,3)) = 1
        _Absorption ("착색 강도", Range(0,8)) = 2
        _ReflectionStrength ("표면 반사 강도", Range(0,1)) = .1
        _ReflectionRoughness ("표면 반사 거칠기", Range(0,1)) = .25
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            // JC | 2026-11-05 | 보스와 해당 무대를 보는 SceneView는 각각 자기 카메라의 원본 해상도 투과색을 사용합니다.
            TEXTURE2D_X(_JcBossGlassColor); SAMPLER(sampler_JcBossGlassColor);
            float _JcBossGlassColorReady;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Thickness, _IOR, _RefractionStrength, _Absorption;
                half _FaceAlpha, _EdgeAlpha, _ReflectionStrength, _ReflectionRoughness;
            CBUFFER_END
            struct Attributes { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=TransformObjectToWorld(v.vertex.xyz);
                o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);
                return o;
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
                float edgeFade=saturate(min(min(uv.x,uv.y),min(1-uv.x,1-uv.y))*40);
                refractedUV=lerp(uv,refractedUV,edgeFade);
                // JC | 2026-11-05 | 화면 밖 및 유리 앞의 기둥/캐릭터가 굴절에 끌려 들어오는 것을 제한합니다.
                float sceneDepth=LinearEyeDepth(SampleSceneDepth(saturate(refractedUV)),_ZBufferParams);
                float glassDepth=-TransformWorldToView(i.world).z;
                if(any(refractedUV<0)||any(refractedUV>1)||sceneDepth<glassDepth+.001)refractedUV=uv;
                half3 background=_JcBossGlassColorReady>.5 ? SAMPLE_TEXTURE2D_X(_JcBossGlassColor,sampler_JcBossGlassColor,UnityStereoTransformScreenSpaceTex(refractedUV)).rgb : SampleSceneColor(refractedUV);
                half rim=(1-facing)*(1-facing);
                float opticalPath=_Thickness/max(.25,abs(dot(transmitted,n)));
                half density=max(0,_Absorption)*lerp(_FaceAlpha,_EdgeAlpha,rim)*opticalPath/.06;
                half3 transmission=exp(-density*(1-saturate(_Tint.rgb)));
                half f0=(_IOR-1)/(_IOR+1);f0*=f0;
                half fresnel=(f0+(1-f0)*pow(1-facing,5))*_ReflectionStrength;
                half3 reflection=GlossyEnvironmentReflection(reflect(-v,n),i.world,_ReflectionRoughness,1);
                return half4(lerp(background*transmission,reflection,saturate(fresnel)),1);
            }
            ENDHLSL
        }
    }
}
