using UnityEngine;
using UnityEngine.UI;

/// <summary>홍보 +/- 버튼의 파란 베벨 룩을 사용한 해상도 독립 슬라이더 노브.</summary>
public sealed class PublicitySliderHandleGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        Layer(vh, r, 7, new Color(.03f,.24f,.46f), new Color(.02f,.15f,.36f));
        r = Inset(r, 1.5f); Layer(vh,r,6,new Color(.1f,.8f,1),new Color(.02f,.32f,.85f));
        r = Inset(r, 3); r.yMin += 2; Layer(vh,r,4,new Color(.04f,.61f,1),new Color(.03f,.32f,.96f));
        Rect shine = new Rect(r.xMin+1,r.yMax-5,Mathf.Min(8,r.width-2),3);
        Layer(vh,shine,1.5f,new Color(.7f,1,1,.95f),new Color(.3f,.85f,1,.6f));
    }
    private static Rect Inset(Rect r,float n) => Rect.MinMaxRect(r.xMin+n,r.yMin+n,r.xMax-n,r.yMax-n);
    private void Layer(VertexHelper vh,Rect r,float radius,Color top,Color bottom)
    {
        radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
        int center=vh.currentVertCount;
        vh.AddVert(r.center,Color.Lerp(bottom,top,.5f)*color,Vector2.zero);
        const int segments=8; int count=4*(segments+1);
        for(int corner=0;corner<4;corner++)for(int i=0;i<=segments;i++) {
            float angle=(corner*90+i*90f/segments)*Mathf.Deg2Rad;
            Vector2 c=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
            Vector2 v=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            vh.AddVert(v,Color.Lerp(bottom,top,Mathf.InverseLerp(r.yMin,r.yMax,v.y))*color,Vector2.zero);
        }
        for(int i=0;i<count;i++)vh.AddTriangle(center,center+1+i,center+1+(i+1)%count);
    }
}
