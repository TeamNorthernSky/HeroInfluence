// [CHEAT_ONLY / 다른 기능 재사용 금지 — 2026-10-27 사용자 요청]
// 이 파일은 1구역 플레이테스트 진입 시간을 줄이는 치트 전용입니다.
// 치트 착지 후보 표시 전용입니다. 정식 이동범위 그리드·건물 윤곽선 재질에 사용하지 마세요.
// 의존 방향은 치트 → 기존 게임 API만 허용합니다. 일반 게임 코드는 이 폴더를 참조하지 마세요.
// 공통 기능이 필요하면 기존 정식 시스템을 사용하세요. 치트 구현을 공용 코드로 승격하거나 복사하지 마세요.

Shader "JC/Debug/Zone1WarpCells"
{
    Properties { _Hover ("선택 셀", Vector) = (0,0,0,0) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Hover;
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };
            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings o; o.world = TransformObjectToWorld(positionOS);
                o.positionCS = TransformWorldToHClip(o.world); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                bool hover = _Hover.w > .5 && all(abs(i.world.xz - _Hover.xy) < _Hover.zz);
                return hover ? half4(.8,1,.2,.8) : half4(.05,.95,.55,.34);
            }
            ENDHLSL
        }
    }
}
