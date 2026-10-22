using UnityEngine;
using UnityEngine.UI;

/// <summary>원과 독립된 메시 및 Transform으로 좌우 이동하는 화살표입니다.</summary>
public sealed class JcBattleHoverArrow : MaskableGraphic
{
    private Vector2 origin, pixel = Vector2.one;
    private float phase;

    public void Configure(Vector2 tip, Vector2 screenPixel)
    {
        origin = tip;
        pixel = screenPixel;
        rectTransform.localScale = new Vector3(-pixel.x * 1.6f, pixel.y * 1.6f, 1f);
        Move(phase);
    }

    public void Move(float progress)
    {
        phase = progress;
        rectTransform.anchoredPosition = origin + new Vector2(pixel.x * (Bounce(phase) * 6f - 44.8f), 0);
    }

    public static float Blend(float phase)
    {
        float p = Mathf.Repeat(phase, 1f);
        return p < .25f ? p / .25f : 1f - (p - .25f) / .75f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        DrawArrow(vh, Vector2.zero, Vector2.one);
    }

    public static float Bounce(float phase)
    {
        float p = Mathf.Repeat(phase, 1f);
        return p < .25f ? Mathf.SmoothStep(0, 1, p / .25f) : 1f - Mathf.SmoothStep(0, 1, (p - .25f) / .75f);
    }

    private static void DrawArrow(VertexHelper vh, Vector2 origin, Vector2 pixel)
    {
        var dark = new Color(.27f, .015f, .025f, 1);
        var edge = new Color(.52f, .025f, .04f, 1);
        var red = new Color(1, .055f, .07f, 1);
        var light = new Color(1, .4f, .34f, 1);
        var lower = new Color(.68f, .015f, .035f, 1);
        Triangle(vh, origin + pixel * new Vector2(1,-2), pixel, new Vector2(-28,0),new Vector2(0,17),new Vector2(0,-17),dark,dark,dark);
        Triangle(vh, origin, pixel,new Vector2(-28,0),new Vector2(0,17),new Vector2(0,-17),edge,dark,dark);
        Triangle(vh, origin, pixel,new Vector2(-24,0),new Vector2(-3,12),new Vector2(-3,-12),red,light,lower);
        Triangle(vh, origin, pixel,new Vector2(-24,0),new Vector2(-3,12),new Vector2(-9,0),light,light,red);
        Triangle(vh, origin, pixel,new Vector2(-24,0),new Vector2(-9,0),new Vector2(-3,-12),red,red,lower);
        Triangle(vh, origin, pixel,new Vector2(-9,0),new Vector2(-3,12),new Vector2(-3,-12),red,edge,lower);
    }

    private static void Triangle(VertexHelper vh, Vector2 origin, Vector2 pixel, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
    {
        int start = vh.currentVertCount;
        vh.AddVert(origin + Vector2.Scale(a,pixel),ca,Vector2.zero);
        vh.AddVert(origin + Vector2.Scale(b,pixel),cb,Vector2.zero);
        vh.AddVert(origin + Vector2.Scale(c,pixel),cc,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);
    }

}
