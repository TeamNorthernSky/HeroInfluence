// [본게임 공유 자산 / 2026-10-13] 테스트 폴더 전용 셰이더가 아닙니다.
// 본게임 Unit_VillanWheel_40005 -> ASB_Work/Effect/Hoop/HoopVfxPreset.asset ->
// JC_YuliaEnergy.mat / JC_YuliaSmoke.mat이 이 셰이더를 참조합니다. 삭제·GUID 변경 시 본게임 연출도 깨집니다.
// JC_DespairWheelFrame.mat도 같은 본게임 프리셋이 공유합니다(프레임 재질은 별도 URP 셰이더 사용).
// 현재 위치와 참조를 유지하며, 1차 완성 후 리팩토링 검토 때 본게임·테스트 양쪽 의존성을 함께 확인합니다.
Shader "JC/BattleTesting/YuliaEnergy"
{
    Properties
    {
        [HDR] _Color ("Energy", Color) = (1,0.2,2,1)
        _Mode ("Line 0 / Shell 1 / Particle 2", Float) = 0
        _SrcBlend ("Src", Float) = 5
        _DstBlend ("Dst", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color; float _Mode; float _SrcBlend; float _DstBlend;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o; o.world=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; o.color=dot(v.color.rgb,v.color.rgb)<.0001?float4(1,1,1,1):v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float alpha;
                if (_Mode>.5 && _Mode<1.5)
                {
                    float rim=pow(1-saturate(abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world)))),2.2);
                    float wave=sin(i.world.y*13+sin(i.world.x*8+_Time.y*1.5)*2+_Time.y*3);
                    float veins=pow(saturate(1-abs(wave)),14);
                    alpha=saturate(rim*.85+veins*.2+.025);
                }
                else if (_Mode>1.5)
                {
                    float2 p=i.uv*2-1; float r=dot(p,p);
                    float cloud=.7+.3*sin(i.uv.x*14+sin(i.uv.y*17)+_Time.y);
                    alpha=pow(saturate(1-r),2)*cloud;
                }
                else alpha=pow(saturate(1-abs(i.uv.y*2-1)),1.4);
                return half4(_Color.rgb*i.color.rgb,alpha*_Color.a*i.color.a);
            }
            ENDHLSL
        }
    }
}
