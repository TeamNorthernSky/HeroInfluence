Shader "UI/UIGlowingGrid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GridColor ("Grid Glow Color (HDR)", Color) = (0.2, 0.8, 1.0, 1)
        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 0)
        
        _LineThickness ("Line Thickness", Range(0.001, 0.1)) = 0.05
        XLength("X 칸 수", Float) = 2
        ZLength("Y 칸 수", Float) = 3
        
        _PulseSpeed("Pulse Speed (일렁임 속도)", Float) = 3.0
        _PulseMin("Pulse Min (최소 밝기)", Range(0, 1)) = 0.3
        _PulseMax("Pulse Max (최대 밝기)", Range(1, 10)) = 2.0
        
        // UI Required Properties (마스크 등 캔버스 요소 호환성)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            float4 _GridColor;
            float4 _BackgroundColor;
            float _LineThickness;
            float XLength;
            float ZLength;
            float _PulseSpeed;
            float _PulseMin;
            float _PulseMax;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                
                // UI Canvas 환경에서는 Scale대신 RectTransform의 형태에 맞춰지므로 uv타일링만 칸 수에 맞게 제어합니다.
                OUT.texcoord = v.texcoord * float2(XLength, ZLength);
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 grid = frac(IN.texcoord);
                // 칸 수가 늘어나거나 UI 요소의 종횡비가 변할 때 선 두께를 보정
                float2 halfThick = _LineThickness * float2(XLength, ZLength) * 0.5;
                
                float2 isLine = step(grid, halfThick) + step(1.0 - halfThick, grid);
                float lineIntensity = saturate(isLine.x + isLine.y);
                
                // 시간에 따른 일렁임(Pulse) 파형 만들기 (sin(t)를 0~1사이로 조정)
                float sineWave = (sin(_Time.y * _PulseSpeed) + 1.0) * 0.5; 
                // Min/Max 값 사이를 오가게 설정
                float pulse = lerp(_PulseMin, _PulseMax, sineWave);
                
                // 그리드 원본 색상에 일렁임(Pulse) 값을 곱해서 반짝이게 만듭니다.
                float4 glowingGridColor = _GridColor;
                glowingGridColor.rgb *= pulse;
                
                // 배경색과 반짝이는 라인 색상을 혼합합니다.
                float4 color = lerp(_BackgroundColor, glowingGridColor, lineIntensity);
                
                // UI Canvas 그룹의 알파(투명도 제어) 등과 호환되도록 정점 컬러 반영
                color.a *= IN.color.a;

                return color;
            }
            ENDCG
        }
    }
}
