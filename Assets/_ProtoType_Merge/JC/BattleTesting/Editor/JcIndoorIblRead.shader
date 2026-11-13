// JC | 2026-11-12 | 읽기 비활성 HDR 큐브맵을 편집기에서만 선형 HDR로 읽습니다. 런타임에는 사용하지 않습니다.
Shader "Hidden/JC/Indoor IBL Read"
{
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            samplerCUBE _Source;
            int _Face;
            float4 frag(v2f_img i):SV_Target
            {
                float2 p=i.uv*2-1; float3 d;
                if(_Face==0)d=float3(1,-p.y,-p.x);
                else if(_Face==1)d=float3(-1,-p.y,p.x);
                else if(_Face==2)d=float3(p.x,1,p.y);
                else if(_Face==3)d=float3(p.x,-1,-p.y);
                else if(_Face==4)d=float3(p.x,-p.y,1);
                else d=float3(-p.x,-p.y,-1);
                return float4(texCUBElod(_Source,float4(normalize(d),0)).rgb,1);
            }
            ENDHLSL
        }
    }
}
