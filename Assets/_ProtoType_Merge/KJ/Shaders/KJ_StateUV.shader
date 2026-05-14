Shader "KJ/StateUV"
{
    // ──────────────────────────────────────
    // 상태(State)에 따라 버텍스 컬러가 (1,1,1)인 메시의 UV를 교체합니다.
    //
    // _State: 0 = Normal  → UV (0.10, 0.10)
    //         1 = Ally    → UV (0.50, 0.10)
    //         2 = Enemy   → UV (0.60, 0.10)
    //
    // 버텍스 컬러가 흰색이 아닌 정점은 원본 메시 UV를 그대로 사용합니다.
    // ──────────────────────────────────────
    Properties
    {
        [MainTexture] _BaseMap       ("Base Texture", 2D)   = "white" {}
        [MainColor]   _BaseColor     ("Base Color",   Color) = (1,1,1,1)

        _State                ("State (0=Normal 1=Ally 2=Enemy)", Float) = 0

        // 상태별 UV 좌표 (인스펙터에서 직접 조정 가능)
        _NormalUV             ("Normal UV",  Vector) = (0.1, 0.1, 0, 0)
        _AllyUV               ("Ally UV",    Vector) = (0.5, 0.1, 0, 0)
        _EnemyUV              ("Enemy UV",   Vector) = (0.6, 0.1, 0, 0)

        // 흰색 판별 임계값 (R, G, B 모두 이 값 이상이면 '흰색 정점')
        _WhiteThreshold       ("White Threshold", Range(0.8, 1.0)) = 0.99
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP Core 인클루드
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ──────────────────────
            // 상수 버퍼
            // ──────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _State;
                float4 _NormalUV;
                float4 _AllyUV;
                float4 _EnemyUV;
                float  _WhiteThreshold;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // ──────────────────────
            // 입출력 구조체
            // ──────────────────────
            struct Attributes
            {
                float4 positionOS  : POSITION;
                float2 uv          : TEXCOORD0;
                float4 vertexColor : COLOR;    // 버텍스 컬러
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 vertexColor : COLOR;
            };

            // ──────────────────────
            // 버텍스 셰이더
            // ──────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.vertexColor = IN.vertexColor;

                // 버텍스 컬러가 (1,1,1) 이상인 정점인지 판별
                bool isWhite = (IN.vertexColor.r >= _WhiteThreshold) &&
                               (IN.vertexColor.g >= _WhiteThreshold) &&
                               (IN.vertexColor.b >= _WhiteThreshold);

                if (isWhite)
                {
                    // 상태에 따라 고정 UV 좌표 선택
                    float2 stateUV;
                    int stateInt = (int)round(_State);
                    if      (stateInt == 1) stateUV = _AllyUV.xy;
                    else if (stateInt == 2) stateUV = _EnemyUV.xy;
                    else                   stateUV = _NormalUV.xy;

                    OUT.uv = stateUV;
                }
                else
                {
                    // 흰색이 아닌 정점은 원본 UV 그대로 사용
                    OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                }

                return OUT;
            }

            // ──────────────────────
            // 프래그먼트 셰이더
            // ──────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return texColor * _BaseColor;
            }

            ENDHLSL
        }

        // 그림자 캐스팅 패스 (URP 표준)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _State;
                float4 _NormalUV;
                float4 _AllyUV;
                float4 _EnemyUV;
                float  _WhiteThreshold;
            CBUFFER_END

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings   { float4 positionHCS : SV_POSITION; };

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
