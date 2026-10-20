using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 패널 하단 텍스처를 위아래 대칭으로 사용하여 제목 바 없는 패널을 그린다.</summary>
public sealed class TutorialNoticeBackground : MaskableGraphic
{
    [SerializeField, Tooltip("하단 마감을 재사용할 기존 안내 패널 스프라이트입니다.")]
    private Sprite source;
    public override Texture mainTexture => source != null ? source.texture : base.mainTexture;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); if(source==null)return;
        Rect r=GetPixelAdjustedRect(); var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(source);
        float edge=Mathf.Min(14,Mathf.Min(r.width,r.height)*.25f);
        float[] x={r.xMin,r.xMin+edge,r.xMax-edge,r.xMax};
        float[] y={r.yMin,r.yMin+edge,r.center.y,r.yMax-edge,r.yMax};
        float[] u={uv.x,Mathf.Lerp(uv.x,uv.z,28f/source.rect.width),Mathf.Lerp(uv.x,uv.z,1-28f/source.rect.width),uv.z};
        float low=Mathf.Lerp(uv.y,uv.w,28f/source.rect.height),middle=Mathf.Lerp(uv.y,uv.w,.45f);
        float[] v={uv.y,low,middle,low,uv.y};
        for(int j=0;j<5;j++)for(int i=0;i<4;i++)vh.AddVert(new Vector2(x[i],y[j]),color,new Vector2(u[i],v[j]));
        for(int j=0;j<4;j++)for(int i=0;i<3;i++){int a=j*4+i;vh.AddTriangle(a,a+4,a+1);vh.AddTriangle(a+1,a+4,a+5);}
    }
}
