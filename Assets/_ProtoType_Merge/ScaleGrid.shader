Shader "Custom/ScaleGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.2, 0.2, 0.2, 1)
        _BackgroundColor ("Background Color", Color) = (1, 1, 1, 1)
        _LineThickness ("Line Thickness", Range(0.001, 1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _GridColor;
            float4 _BackgroundColor;
            float _LineThickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 마스터 오브젝트의 Transform Scale 값 추출 (X, Z축)
                float scaleX = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float scaleZ = length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z));
                
                // 오브젝트의 UV (0~1)에 Scale 값을 곱해서 Scale 수치만큼 UV가 반복되도록 설정합니다.
                o.uv = v.uv * float2(scaleX, scaleZ);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // frac을 통해 0~1로 순환하는 Grid UV 좌표 생성
                float2 grid = frac(i.uv);
                
                float halfThick = _LineThickness * 0.5;
                
                // 모서리(0과 1) 근처에 있을 때 선을 그림 (안티 앨리어싱 없는 선명한 형태)
                float2 isLine = step(grid, float2(halfThick, halfThick)) + step(float2(1.0 - halfThick, 1.0 - halfThick), grid);
                
                // X라인과 Z라인을 합쳐서 겹치는 부분을 최대 1로 고정
                float lineIntensity = saturate(isLine.x + isLine.y);
                
                // lerp 함수가 Alpha(투명도) 채널까지 자동으로 혼합해줍니다.
                // 배경 부분은 _BackgroundColor의 알파값을, 선 부분은 _GridColor의 알파값을 따라갑니다.
                return lerp(_BackgroundColor, _GridColor, lineIntensity);
            }
            ENDCG
        }
    }
}
