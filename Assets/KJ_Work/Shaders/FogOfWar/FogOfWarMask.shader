Shader "Hidden/FogOfWarMask"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        ZWrite Off ZTest Always Cull Off
        
        // 겹쳤을때 값이 무한정 더해지지 않도록 최대값 보존 블렌딩
        BlendOp Max
        Blend One One

        Pass
        {
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float _FogRemainBonus;

            fixed4 frag (v2f i) : SV_Target
            {
                // 중심(0.5, 0.5)에서의 거리 산출 (가장자리 1.0)
                float dist = distance(i.uv, float2(0.5, 0.5)) * 2.0;
                
                // 경계가 살짝 부드러운 원형 생성
                float alpha = saturate(1.0 - dist); 
                alpha = smoothstep(0.0, 0.5, alpha); 
                
                return fixed4(alpha + (alpha * _FogRemainBonus), 0, 0, 1);
            }
            ENDCG
        }
    }
}
