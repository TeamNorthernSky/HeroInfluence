using UnityEngine;
using UnityEngine.UI;

/// <summary>고정된 원형 메시의 색상만 바꾸고 독립된 화살표 Graphic을 제어합니다.</summary>
public sealed class JcBattleHoverRing : MaskableGraphic
{
    private float width = 10f;
    private JcBattleHoverArrow arrow;
    private readonly Vector3[] corners = new Vector3[4];
    private Image badgeCircle;
    public const float ArrowClearancePixels = 80f;

    public void AlignToBadge(Image circle) { badgeCircle = circle; SetVerticesDirty(); }

    public void Show(Color tint, float thickness, float progress = 0)
    {
        if (arrow == null)
        {
            var go = new GameObject("JC_HoverArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(JcBattleHoverArrow));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            arrow = go.GetComponent<JcBattleHoverArrow>();
            arrow.raycastTarget = false;
            SetVerticesDirty();
        }
        if (!Mathf.Approximately(width, thickness)) { width = thickness; SetVerticesDirty(); }
        enabled = true;
        // Graphic.color는 메시를 재생성하므로 렌더러 색상만 변경합니다.
        canvasRenderer.SetColor(tint);
        arrow.enabled = true;
        arrow.Move(progress);
    }

    protected override void OnDisable()
    {
        if (arrow != null) arrow.enabled = false;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        var center = rect.center;
        var inner = rect.size * 0.5f;
        if (badgeCircle != null)
        {
            // 실제 마스크 Image의 픽셀 보정 영역을 공유합니다. 슬롯별 사각형/피봇 차이를 누적하지 않습니다.
            var badgeRect = badgeCircle.GetPixelAdjustedRect();
            var badgeTransform = badgeCircle.rectTransform;
            center = rectTransform.InverseTransformPoint(badgeTransform.TransformPoint(badgeRect.center));
            var right = rectTransform.InverseTransformPoint(badgeTransform.TransformPoint(new Vector2(badgeRect.xMax, badgeRect.center.y)));
            var top = rectTransform.InverseTransformPoint(badgeTransform.TransformPoint(new Vector2(badgeRect.center.x, badgeRect.yMax)));
            inner = new Vector2(Mathf.Abs(right.x-center.x), Mathf.Abs(top.y-center.y));
        }
        // Canvas와 부모의 축소 배율에 관계없이 실제 화면 두께를 유지합니다.
        rectTransform.GetWorldCorners(corners);
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        var pixel = new Vector2(rect.width / Mathf.Max(.01f, Vector2.Distance(topLeft, topRight)),
            rect.height / Mathf.Max(.01f, Vector2.Distance(bottomLeft, topLeft)));
        // 흰 테두리의 안쪽 안티앨리어싱까지 붉은 불투명 코어로 덮습니다.
        inner -= pixel * 2f;
        Band(vh, center, inner, inner + pixel * width, Color.white, Color.white);
        // 레이아웃이 갱신될 때만 화살표 기준점을 계산합니다. 원의 형상에는 위상을 사용하지 않습니다.
        float referenceLeft = center.x - inner.x - pixel.x * 6f;
        if (arrow != null)
            arrow.Configure(new Vector2(referenceLeft - pixel.x * 27f, center.y), pixel);
    }

    private static void Band(VertexHelper vh, Vector2 center, Vector2 inner, Vector2 outer, Color innerColor, Color outerColor)
    {
        int start = vh.currentVertCount;
        const int segments = 96;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(center + Vector2.Scale(direction, inner), innerColor, Vector2.zero);
            vh.AddVert(center + Vector2.Scale(direction, outer), outerColor, Vector2.zero);
            if (i == 0) continue;
            int index = start + i * 2;
            vh.AddTriangle(index - 2, index - 1, index);
            vh.AddTriangle(index, index - 1, index + 1);
        }
    }
}
