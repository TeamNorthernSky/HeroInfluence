using UnityEngine;
using UnityEngine.UI;

/// <summary>원과 독립된 메시 및 Transform으로 좌우 이동하는 화살표입니다.</summary>
public sealed class JcBattleHoverArrow : MaskableGraphic
{
    // JC | 2026-11-12 | 직전 크기에서 전체 20%, 높이는 추가 20% 축소합니다. 화살촉 기준과 왕복 거리는 유지합니다.
    private const float VisualScale = 1.6f * 1.8f * .8f;
    private JcBattleHoverArrowOutline outline;
    private Vector2 origin, pixel = Vector2.one;
    private float phase;
    // JC | 2026-11-13 | 입체 무그림자 스타일과 회복 파랑을 같은 메시/왕복 경로에서 표시합니다.
    private JcBattleArrowStyle style;
    private bool healing;
    private float sizeMultiplier = 1f, travel = 6f;
    private float outlineWidth = 3f, cornerRadius;
    public void SetAppearance(JcBattleArrowStyle value, bool isHealing, float scale, float movement, float thickness = 3f, float rounding = 0f)
    {
        bool dirty = style != value || healing != isHealing || outlineWidth != thickness || cornerRadius != rounding;
        outlineWidth = thickness; cornerRadius = rounding;
        if (outline != null) outline.Configure(thickness, rounding);
        style = value; healing = isHealing; sizeMultiplier = scale; travel = movement;
        if (outline != null) outline.enabled = enabled && style != JcBattleArrowStyle.Legacy;
        if (dirty) SetVerticesDirty();
    }

    // JC | 2026-11-12 | 스타일 전환 시에만 메시를 다시 만듭니다. 크기와 이동 좌표는 바꾸지 않습니다.
    public void SetLegacyStyle(bool value)
    {
        SetAppearance(value ? JcBattleArrowStyle.Legacy : JcBattleArrowStyle.Flat, false, 1f, 6f);
    }

    public void Configure(Vector2 tip, Vector2 screenPixel)
    {
        origin = tip;
        pixel = screenPixel;
        rectTransform.localScale = new Vector3(-pixel.x * VisualScale * sizeMultiplier, pixel.y * VisualScale * .8f * sizeMultiplier, 1f);
        Move(phase);
    }

    public void Move(float progress)
    {
        phase = progress;
        rectTransform.anchoredPosition = origin + new Vector2(pixel.x * (Bounce(phase) * travel - 28f * VisualScale * sizeMultiplier), 0);
    }

    public static float Blend(float phase)
    {
        float p = Mathf.Repeat(phase, 1f);
        return p < .25f ? p / .25f : 1f - (p - .25f) / .75f;
    }

    // JC | 2026-11-12 | 배지와 같은 색을 테두리 렌더러에만 전달하여 본체 색과 메시 재생성을 분리합니다.
    public void SetOutlineColor(Color tint)
    {
        if (style == JcBattleArrowStyle.Legacy) return;
        if (outline == null)
        {
            var go = new GameObject("JC_HoverArrowOutline", typeof(RectTransform), typeof(CanvasRenderer), typeof(JcBattleHoverArrowOutline));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            outline = go.GetComponent<JcBattleHoverArrowOutline>();
            outline.raycastTarget = false;
        }
        outline.Configure(outlineWidth, cornerRadius);
        outline.enabled = enabled;
        outline.canvasRenderer.SetColor(tint);
    }

    protected override void OnDisable()
    {
        if (outline != null) outline.enabled = false;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // JC | 2026-11-13 | 본체와 점멸 테두리가 같은 둥근 외곽을 사용하여 입체 면 돌출을 방지합니다.
        if (style == JcBattleArrowStyle.Legacy) DrawLegacyArrow(vh, Vector2.zero, Vector2.one, true, healing);
        else if (style == JcBattleArrowStyle.BeveledFlashing && cornerRadius <= 0) DrawLegacyArrow(vh, Vector2.zero, Vector2.one, false, healing);
        else JcBattleArrowContour.Fill(vh, cornerRadius, style == JcBattleArrowStyle.BeveledFlashing, healing);
    }

    public static float Bounce(float phase)
    {
        float p = Mathf.Repeat(phase, 1f);
        return p < .25f ? Mathf.SmoothStep(0, 1, p / .25f) : 1f - Mathf.SmoothStep(0, 1, (p - .25f) / .75f);
    }

    private static void DrawArrow(VertexHelper vh, Vector2 origin, Vector2 pixel, bool healing)
    {
        // JC | 2026-11-12 | 오프셋 그림자와 입체 음영을 없애고 붉은 본체·점멸 테두리만 남깁니다.
        var red = healing ? new Color(.12f, .4f, 1f, 1f) : new Color(1, .055f, .07f, 1);
        Triangle(vh, origin, pixel,new Vector2(-24,0),new Vector2(-3,12),new Vector2(-3,-12),red,red,red);
    }

    // JC | 2026-11-12 | 기존 버전의 색상·삼각면 하이라이트·오프셋 그림자를 그대로 재사용합니다.
    private static void DrawLegacyArrow(VertexHelper vh, Vector2 origin, Vector2 pixel, bool shadow, bool healing)
    {
        var dark = healing ? new Color(.015f, .06f, .27f, 1) : new Color(.27f, .015f, .025f, 1);
        var edge = healing ? new Color(.025f, .15f, .52f, 1) : new Color(.52f, .025f, .04f, 1);
        var red = healing ? new Color(.12f, .4f, 1f, 1f) : new Color(1, .055f, .07f, 1);
        var light = healing ? new Color(.55f, .8f, 1f, 1) : new Color(1, .4f, .34f, 1);
        var lower = healing ? new Color(.035f, .18f, .68f, 1) : new Color(.68f, .015f, .035f, 1);
        if (shadow) Triangle(vh, origin + pixel * new Vector2(1,-2), pixel, new Vector2(-28,0),new Vector2(0,17),new Vector2(0,-17),dark,dark,dark);
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

// JC | 2026-11-12 | 그림자가 없는 중공 삼각 테두리입니다. 배지와 같은 위상으로 CanvasRenderer 색만 점멸합니다.
public sealed class JcBattleHoverArrowOutline : MaskableGraphic
{
    private float width = 3f, radius;
    public void Configure(float thickness, float rounding)
    {
        if (width == thickness && radius == rounding) return;
        width = thickness; radius = rounding; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (width <= 0) return;
        var outer = JcBattleArrowContour.Points(radius, 0);
        var inner = JcBattleArrowContour.Points(radius, width);
        for (int i=0;i<outer.Length;i++) { vh.AddVert(outer[i],Color.white,Vector2.zero); vh.AddVert(inner[i],Color.white,Vector2.zero); }
        for (int i=0;i<outer.Length;i++)
        {
            int next=((i+1)%outer.Length)*2;
            vh.AddTriangle(i*2,next,i*2+1); vh.AddTriangle(i*2+1,next,next+1);
        }
    }
}

// JC | 2026-11-13 | 고정 외곽 안에서만 윤곽 두께와 모서리를 조절합니다. 평면·입체·회복이 동일한 형상을 공유합니다.
internal static class JcBattleArrowContour
{
    static readonly Vector2[] Corners = { new Vector2(-28,0), new Vector2(0,17), new Vector2(0,-17) };
    static readonly Vector2 Center = new Vector2(-9.563f,0);
    internal static Vector2[] Points(float rounding, float inset)
    {
        float scale = Mathf.Max(.1f,1f-Mathf.Clamp(inset,0,6)/9.563f);
        var result=new Vector2[27];
        for(int i=0;i<3;i++)
        {
            Vector2 v=Center+(Corners[i]-Center)*scale;
            Vector2 prev=Center+(Corners[(i+2)%3]-Center)*scale, next=Center+(Corners[(i+1)%3]-Center)*scale;
            float cut=Mathf.Min(Mathf.Clamp(rounding,0,8)*scale,Mathf.Min((prev-v).magnitude,(next-v).magnitude)*.45f);
            Vector2 a=v+(prev-v).normalized*cut,b=v+(next-v).normalized*cut;
            for(int j=0;j<=8;j++) { float t=j/8f; result[i*9+j]=(1-t)*(1-t)*a+2*(1-t)*t*v+t*t*b; }
        }
        return result;
    }
    internal static void Fill(VertexHelper vh,float rounding,bool bevel,bool healing)
    {
        var points=Points(rounding,0);
        Color body=healing?new Color(.12f,.4f,1,1):new Color(1,.055f,.07f,1);
        Color light=healing?new Color(.55f,.8f,1,1):new Color(1,.4f,.34f,1);
        Color dark=healing?new Color(.035f,.18f,.68f,1):new Color(.68f,.015f,.035f,1);
        vh.AddVert(Center,body,Vector2.zero);
        foreach(var p in points) vh.AddVert(p,!bevel?body:Color.Lerp(dark,light,Mathf.InverseLerp(-17,17,p.y)),Vector2.zero);
        for(int i=0;i<points.Length;i++) vh.AddTriangle(0,i+1,(i+1)%points.Length+1);
    }
}
