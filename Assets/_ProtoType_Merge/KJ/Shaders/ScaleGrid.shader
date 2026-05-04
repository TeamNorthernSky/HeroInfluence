Shader "Custom/ScaleGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.2, 0.2, 0.2, 1)
        _BackgroundColor ("Background Color", Color) = (1, 1, 1, 1)
        _LineThickness ("Line Thickness", Range(0.001, 1)) = 0.02
        XLength("XLength (칸 수)", Float) = 2
        ZLength("ZLength (칸 수)", Float) = 3
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

            struct vertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct vertexOutput
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float2 scale : TEXCOORD1; // 선 두께 보정을 위한 스케일 전달용
            };

            float4 _GridColor;
            float4 _BackgroundColor;
            float _LineThickness;
            float XLength;
            float ZLength;

            vertexOutput vert (vertexInput v)
            {
                vertexOutput o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 마스터 오브젝트의 Transform Scale 값 추출 (선의 일정한 두께 유지를 위해 사용)
                float scaleX = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float scaleZ = length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z));
                // 0으로 나누어지는 것(에러)을 방지
                o.scale = float2(max(scaleX, 0.001), max(scaleZ, 0.001));
                
                // 사용자가 지정한 고정된 칸 수 (XLength, ZLength) 만큼 구역을 분할합니다.
                o.uv = v.uv * float2(XLength, ZLength);
                
                return o;
            }

            fixed4 frag (vertexOutput i) : SV_Target
            {
                // 각 칸의 UV를 0~1로 순환하게 만듦
                float2 grid = frac(i.uv);
                
                // 오브젝트의 스케일이 커지면 (예: 10배 확대), 선까지 비정상적으로 10배 길쭉해지는 문제가 생깁니다.
                // 지정된 _LineThickness에 맞춰서 늘어난 스케일만큼 몫을 나누어, 항상 일정한 선 두께가 유지되도록 보정합니다.
                float2 uvThickness = _LineThickness * float2(XLength / i.scale.x, ZLength / i.scale.y);
                float2 halfThick = uvThickness * 0.5;
                
                // 테두리 근처일 경우 선을 그립니다.
                float2 isLine = step(grid, halfThick) + step(1.0 - halfThick, grid);
                
                float lineIntensity = saturate(isLine.x + isLine.y);
                
                // Lerp를 통해 투명도까지 부드럽게 혼합합니다.
                return lerp(_BackgroundColor, _GridColor, lineIntensity);
            }
            ENDCG
        }
    }
}
