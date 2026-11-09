// JC | 2026-11-09 | 해당 이벤트 메시만 가림에서 제외하고 외부 건물/이벤트의 차폐는 유지합니다.
Shader "JC/Indicators/EventMarkerMeshVisibility"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("External Alpha Enabled", Float) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _JcMarkerOwner ("Event owner", Float) = 0
        [HideInInspector] _MeshMap ("Mesh alpha", 2D) = "white" {}
        [HideInInspector] _MeshTint ("Mesh tint", Color) = (1,1,1,1)
        [HideInInspector] _MeshCull ("Mesh cull", Float) = 2
        [HideInInspector] _MeshCutoff ("Mesh cutoff", Float) = .001
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        // JC | 2026-11-09 | 실제 느낌표와 호버 보호 마스크가 동일한 알파·소속·깊이 판정을 사용합니다.
        CGINCLUDE
        #include "UnitySprites.cginc"
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        sampler2D _JcTriggerMeshDepth, _JcTriggerMeshOwners, _JcTriggerOtherMeshDepth;
        float _JcTriggerDepthActive, _JcTriggerDepthUvFlip, _JcMarkerOwner;
        float4x4 _JcTriggerDepthVP, _JcTriggerDepthView;
        struct MarkerOutput { float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float4 screen:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
        MarkerOutput MarkerVert(appdata_t v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            v2f sprite = SpriteVert(v);
            MarkerOutput o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos=sprite.vertex; o.color=sprite.color; o.uv=sprite.texcoord;
            o.world=mul(unity_ObjectToWorld,UnityFlipSprite(v.vertex,_Flip)).xyz;
            o.screen=ComputeScreenPos(o.pos); return o;
        }
        float2 PrivateUv(float3 world)
        {
            float4 projected=mul(_JcTriggerDepthVP,float4(world,1));
            float2 uv=projected.xy/projected.w*.5+.5;
            if(_JcTriggerDepthUvFlip>.5)uv.y=1-uv.y;
            return uv;
        }
        void CheckMarkerVisibility(MarkerOutput i)
        {
            float raw=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.screen.xy/i.screen.w);
            float sceneEye=unity_OrthoParams.w>.5 ? lerp(_ProjectionParams.y,_ProjectionParams.z,
                #if defined(UNITY_REVERSED_Z)
                1-raw
                #else
                raw
                #endif
                ) : LinearEyeDepth(raw);
            float eye=-mul(UNITY_MATRIX_V,float4(i.world,1)).z;
            float bias=max(.002,eye*.00002);
            if(sceneEye+bias<eye)
            {
                float2 uv=PrivateUv(i.world);
                float meshEye=tex2D(_JcTriggerMeshDepth,uv).r;
                float owner=tex2D(_JcTriggerMeshOwners,uv).r;
                float otherEye=tex2D(_JcTriggerOtherMeshDepth,uv).r;
                // 자기 메시가 최근접인 경우에만 예외입니다. 그 뒤의 다른 건물이 아이콘 앞에 있으면 여전히 가립니다.
                bool ownSurface=_JcMarkerOwner>0 && abs(owner-_JcMarkerOwner)<.25;
                clip(_JcTriggerDepthActive>.5 && ownSurface &&
                    abs(meshEye-sceneEye)<max(.004,sceneEye*.00003) && otherEye+bias>=eye ? 1 : -1);
            }
        }
        fixed4 MarkerFrag(MarkerOutput i):SV_Target
        {
            CheckMarkerVisibility(i);
            fixed4 c=SampleSpriteTexture(i.uv)*i.color; c.rgb*=c.a; return c;
        }
        MarkerOutput CoverageVert(appdata_t v)
        {
            MarkerOutput o=MarkerVert(v);
            o.pos=mul(_JcTriggerDepthVP,float4(o.world,1)); return o;
        }
        fixed4 CoverageFrag(MarkerOutput i):SV_Target
        {
            CheckMarkerVisibility(i);
            fixed a=SampleSpriteTexture(i.uv).a*i.color.a;
            return fixed4(a,a,a,a);
        }
        // JC | 2026-11-09 | 기존 채움 보호(RG 깊이)를 보존하고, 별도 패스에서 메시 소속/다른 소속 깊이를 얻습니다.
        sampler2D _MeshMap;
        float4 _MeshMap_ST, _MeshTint, _JcMeshDepthRoles;
        float _MeshCutoff, _JcMeshOwner;
        struct MeshInput { float3 position:POSITION; float2 uv:TEXCOORD0; };
        struct MeshOutput { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float eye:TEXCOORD1; float3 world:TEXCOORD2; };
        MeshOutput MeshVert(MeshInput v)
        {
            MeshOutput o; float4 w=mul(unity_ObjectToWorld,float4(v.position,1));
            o.position=mul(_JcTriggerDepthVP,w); o.eye=-mul(_JcTriggerDepthView,w).z;
            o.uv=v.uv*_MeshMap_ST.xy+_MeshMap_ST.zw; o.world=w.xyz; return o;
        }
        void MeshAlpha(MeshOutput v) { clip(tex2D(_MeshMap,v.uv).a*_MeshTint.a-_MeshCutoff); }
        float2 MeshFrag(MeshOutput v):SV_Target
        {
            MeshAlpha(v);
            return float2(_JcMeshDepthRoles.x>.5?v.eye:65504, _JcMeshDepthRoles.y>.5?v.eye:65504);
        }
        float OwnerFrag(MeshOutput v):SV_Target
        {
            MeshAlpha(v); clip(_JcMeshDepthRoles.x-.5);
            float nearest=tex2D(_JcTriggerMeshDepth,PrivateUv(v.world)).r;
            clip(max(.00002,v.eye*.000002)-abs(nearest-v.eye));
            return _JcMeshOwner;
        }
        float OtherFrag(MeshOutput v):SV_Target
        {
            MeshAlpha(v); clip(_JcMeshDepthRoles.x-.5);
            float owner=tex2D(_JcTriggerMeshOwners,PrivateUv(v.world)).r;
            clip(abs(owner-_JcMeshOwner)-.25);
            return v.eye;
        }
        ENDCG
        Pass
        {
            Name "EventMarker"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex MarkerVert
            #pragma fragment MarkerFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            ENDCG
        }
        Pass
        {
            Name "WorldMeshDepth"
            Tags { "LightMode"="JcWorldMeshDepth" }
            Cull [_MeshCull] ZWrite Off ZTest Always
            Blend One One BlendOp Min
            CGPROGRAM
            #pragma vertex MeshVert
            #pragma fragment MeshFrag
            ENDCG
        }
        Pass
        {
            Name "WorldMeshOwner"
            Tags { "LightMode"="JcWorldMeshOwner" }
            Cull [_MeshCull] ZWrite Off ZTest Always
            Blend One One BlendOp Min
            CGPROGRAM
            #pragma vertex MeshVert
            #pragma fragment OwnerFrag
            ENDCG
        }
        Pass
        {
            Name "OtherOwnerDepth"
            Tags { "LightMode"="JcOtherOwnerDepth" }
            Cull [_MeshCull] ZWrite Off ZTest Always
            Blend One One BlendOp Min
            CGPROGRAM
            #pragma vertex MeshVert
            #pragma fragment OtherFrag
            ENDCG
        }
        Pass
        {
            Name "MarkerCoverage"
            Tags { "LightMode"="JcMarkerCoverage" }
            Cull Off ZWrite Off ZTest Always
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex CoverageVert
            #pragma fragment CoverageFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            ENDCG
        }
    }
}
