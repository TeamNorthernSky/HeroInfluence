Shader "Hidden/FogOfWarDecay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _DecayAmount ("Decay Amount", Float) = 0.01
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _DecayAmount;

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                // 방문했던 시야 텍스처 값을 감소시킴 (0 이하로 떨어지지 않게 제한)
                float val = max(0.0, col.r - _DecayAmount);
                return fixed4(val, val, val, val);
            }
            ENDCG
        }
    }
}
