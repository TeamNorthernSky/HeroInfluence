// JC | 2026-11-09 | 느낌표의 기존 투명 정렬을 유지하고 월드 메시 깊이만 무시합니다. 공용 카메라 깊이는 수정하지 않습니다.
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
        [HideInInspector] _MeshMap ("Mesh alpha", 2D) = "white" {}
        [HideInInspector] _MeshTint ("Mesh tint", Color) = (1,1,1,1)
        [HideInInspector] _MeshCull ("Mesh cull", Float) = 2
        [HideInInspector] _MeshCutoff ("Mesh cutoff", Float) = .001
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
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
            #include "UnitySprites.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _JcTriggerMeshDepth;
            float _JcTriggerDepthActive, _JcTriggerDepthUvFlip;
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
            fixed4 MarkerFrag(MarkerOutput i):SV_Target
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
                if(sceneEye+max(.002,eye*.00002)<eye)
                {
                    float4 clipPos=mul(_JcTriggerDepthVP,float4(i.world,1));
                    float2 uv=clipPos.xy/clipPos.w*.5+.5;
                    if(_JcTriggerDepthUvFlip>.5)uv.y=1-uv.y;
                    float meshEye=tex2D(_JcTriggerMeshDepth,uv).r;
                    // JC | 2026-11-09 | 메시가 실제 최근접 가림 표면일 때만 통과합니다. 다른 깊이 기록 표시는 유지합니다.
                    clip(_JcTriggerDepthActive>.5 && abs(meshEye-sceneEye)<max(.004,sceneEye*.00003) ? 1 : -1);
                }
                fixed4 c=SampleSpriteTexture(i.uv)*i.color; c.rgb*=c.a; return c;
            }
            ENDCG
        }
        Pass
        {
            Name "WorldMeshDepth"
            Tags { "LightMode"="JcWorldMeshDepth" }
            Cull [_MeshCull] ZWrite Off ZTest Always
            Blend One One BlendOp Min
            HLSLPROGRAM
            #pragma vertex MeshVert
            #pragma fragment MeshFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MeshMap); SAMPLER(sampler_MeshMap);
            float4 _MeshMap_ST, _MeshTint, _JcMeshDepthRoles;
            float _MeshCutoff;
            float4x4 _JcTriggerDepthVP, _JcTriggerDepthView;
            struct Input { float3 position:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 position:SV_POSITION; float2 uv:TEXCOORD0; float eye:TEXCOORD1; };
            Output MeshVert(Input v) { Output o; float4 w=float4(TransformObjectToWorld(v.position),1); o.position=mul(_JcTriggerDepthVP,w); o.eye=-mul(_JcTriggerDepthView,w).z; o.uv=v.uv*_MeshMap_ST.xy+_MeshMap_ST.zw; return o; }
            // JC | 2026-11-09 | 큰 빈 깊이와 lerp하면 작은 깊이가 상쇄되어 발 경계의 비교 정밀도가 떨어지므로 분기로 기록합니다.
            float2 MeshFrag(Output v):SV_Target { clip(SAMPLE_TEXTURE2D(_MeshMap,sampler_MeshMap,v.uv).a*_MeshTint.a-_MeshCutoff); return float2(_JcMeshDepthRoles.x>.5?v.eye:65504, _JcMeshDepthRoles.y>.5?v.eye:65504); }
            ENDHLSL
        }
    }
}
