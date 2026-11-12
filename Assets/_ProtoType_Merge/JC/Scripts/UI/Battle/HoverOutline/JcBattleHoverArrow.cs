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
    private bool legacyStyle;

    // JC | 2026-11-12 | 스타일 전환 시에만 메시를 다시 만듭니다. 크기와 이동 좌표는 바꾸지 않습니다.
    public void SetLegacyStyle(bool value)
    {
        if (legacyStyle == value) return;
        legacyStyle = value;
        if (outline != null) outline.enabled = enabled && !legacyStyle;
        SetVerticesDirty();
    }

    public void Configure(Vector2 tip, Vector2 screenPixel)
    {
        origin = tip;
        pixel = screenPixel;
        rectTransform.localScale = new Vector3(-pixel.x * VisualScale, pixel.y * VisualScale * .8f, 1f);
        Move(phase);
    }

    public void Move(float progress)
    {
        phase = progress;
        rectTransform.anchoredPosition = origin + new Vector2(pixel.x * (Bounce(phase) * 6f - 28f * VisualScale), 0);
    }

    public static float Blend(float phase)
    {
        float p = Mathf.Repeat(phase, 1f);
        return p < .25f ? p / .25f : 1f - (p - .25f) / .75f;
    }

    // JC | 2026-11-12 | 배지와 같은 색을 테두리 렌더러에만 전달하여 본체 색과 메시 재생성을 분리합니다.
    public void SetOutlineColor(Color tint)
    {
        if (legacyStyle) return;
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
        if (legacyStyle) DrawLegacyArrow(vh, Vector2.zero, Vector2.one);
        else DrawArrow(vh, Vector2.zero, Vector2.one);
    }

    public static float Bounce(float phase)
    {
        float p = Mathf.Repeat(phase, 1f);
        return p < .25f ? Mathf.SmoothStep(0, 1, p / .25f) : 1f - Mathf.SmoothStep(0, 1, (p - .25f) / .75f);
    }

    private static void DrawArrow(VertexHelper vh, Vector2 origin, Vector2 pixel)
    {
        // JC | 2026-11-12 | 오프셋 그림자와 입체 음영을 없애고 붉은 본체·점멸 테두리만 남깁니다.
        var red = new Color(1, .055f, .07f, 1);
        Triangle(vh, origin, pixel,new Vector2(-24,0),new Vector2(-3,12),new Vector2(-3,-12),red,red,red);
    }

    // JC | 2026-11-12 | 기존 버전의 색상·삼각면 하이라이트·오프셋 그림자를 그대로 재사용합니다.
    private static void DrawLegacyArrow(VertexHelper vh, Vector2 origin, Vector2 pixel)
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

// JC | 2026-11-12 | 그림자가 없는 중공 삼각 테두리입니다. 배지와 같은 위상으로 CanvasRenderer 색만 점멸합니다.
public sealed class JcBattleHoverArrowOutline : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var outer = new[] { new Vector2(-28,0), new Vector2(0,17), new Vector2(0,-17) };
        var inner = new[] { new Vector2(-24,0), new Vector2(-3,12), new Vector2(-3,-12) };
        for (int i = 0; i < 3; i++)
        {
            vh.AddVert(outer[i], Color.white, Vector2.zero);
            vh.AddVert(inner[i], Color.white, Vector2.zero);
        }
        for (int i = 0; i < 3; i++)
        {
            int next = ((i + 1) % 3) * 2;
            vh.AddTriangle(i * 2, next, i * 2 + 1);
            vh.AddTriangle(i * 2 + 1, next, next + 1);
        }
    }
}
