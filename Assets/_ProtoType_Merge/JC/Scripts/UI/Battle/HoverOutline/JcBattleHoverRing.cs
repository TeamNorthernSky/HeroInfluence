using UnityEngine;
using UnityEngine.UI;

/// <summary>고정된 원형 메시의 색상만 바꾸고 독립된 화살표 Graphic을 제어합니다.</summary>
public sealed class JcBattleHoverRing : MaskableGraphic
{
    private float width = 10f;
    private JcBattleHoverArrow arrow;
    // JC | 2026-11-13 | 별도 Graphic으로 구분선을 그려 부모 원의 점멸 색을 상속하지 않습니다.
    private JcBattleHoverRingDivider divider;
    public override void SetVerticesDirty()
    {
        base.SetVerticesDirty();
        if (divider != null) divider.SetVerticesDirty();
    }
    private readonly Vector3[] corners = new Vector3[4];
    private Image badgeCircle;
    // JC | 2026-11-04 | 턴 배지 외곽을 기준으로 튜토리얼 안내와 강조 윤곽을 정렬합니다.
    private bool guideRing;
    // JC | 2026-11-13 | 형상은 조절값이 달라질 때만 갱신하고 점멸은 CanvasRenderer 색으로 처리합니다.
    private float innerDiameter = .98f, outerDiameter = 1.26f, arrowScale = 1f;
    private Vector2 centerOffset, arrowOffset;
    // JC | 2026-11-12 | 확대된 화살표와 추가 8px 이격까지 툴팁 배치의 회피 영역에 포함합니다.
    public const float ArrowClearancePixels = 128f;

    public void AlignToBadge(Image circle) { badgeCircle = circle; SetVerticesDirty(); }

    public void Show(Color tint, float thickness, float progress = 0, bool useGuideRing = false, bool useLegacyArrowStyle = false, JcBattleTurnSlotConfiguration settings = null, bool healing = false, bool showArrow = true)
    {
        if (guideRing != useGuideRing) { guideRing = useGuideRing; SetVerticesDirty(); }
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
        if (settings != null && (innerDiameter != settings.innerDiameter || outerDiameter != settings.outerDiameter ||
            centerOffset != settings.centerOffset || arrowOffset != settings.arrowOffset || arrowScale != settings.arrowScale))
        {
            innerDiameter = settings.innerDiameter; outerDiameter = settings.outerDiameter;
            centerOffset = settings.centerOffset; arrowOffset = settings.arrowOffset; arrowScale = settings.arrowScale;
            SetVerticesDirty();
        }
        enabled = true;
        // JC | 2026-11-13 | 생성은 최초 1회, 이후에는 변경된 조절값만 갱신합니다. 레이아웃 재생성 중 자식 메시를 갱신하지 않습니다.
        if (divider == null)
        {
            var go = new GameObject("JC_HoverRingInnerDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(JcBattleHoverRingDivider));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = rectTransform.pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            divider = go.GetComponent<JcBattleHoverRingDivider>();
            divider.raycastTarget = false;
        }
        divider.Show(this, settings?.innerSeparatorWidth ?? 1f,
            settings?.innerSeparatorColor ?? new Color(.07843138f, .12941177f, .23921569f, 1f), settings?.innerSeparatorOpacity ?? 1f);
        // Graphic.color는 메시를 재생성하므로 렌더러 색상만 변경합니다.
        canvasRenderer.SetColor(tint);
        // JC | 2026-11-13 | 확정 광역 대상의 원은 유지하되 화살표는 실제 호버 대상에만 표시합니다.
        // 호버가 옮겨져도 원은 계속 활성 상태일 수 있으므로 화살표 상태를 매번 갱신합니다.
        arrow.enabled = showArrow;
        // JC | 2026-11-12 | 배지 점멸은 유지하고 화살표의 표현 스타일만 전환합니다.
        if (settings != null) arrow.SetAppearance(settings.style, healing, settings.arrowScale, settings.arrowTravel, settings.arrowOutlineWidth, settings.arrowCornerRadius);
        else arrow.SetLegacyStyle(useLegacyArrowStyle);
        arrow.Move(progress);
        // JC | 2026-11-12 | 화살표 테두리와 턴 배지의 적백 점멸을 같은 색·위상으로 맞춥니다.
        arrow.SetOutlineColor(tint);
    }

    protected override void OnDisable()
    {
        if (arrow != null) arrow.enabled = false;
        if (divider != null) divider.enabled = false;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        GetRingGeometry(out var center, out var inner, out var outer, out var pixel);
        Band(vh, center, inner, outer, Color.white, Color.white);
        // 레이아웃이 갱신될 때만 화살표 기준점을 계산합니다. 원의 형상에는 위상을 사용하지 않습니다.
        float referenceLeft = center.x - inner.x - pixel.x * 6f;
        if (arrow != null)
            arrow.Configure(new Vector2(referenceLeft - pixel.x * 35f, center.y) + Vector2.Scale(arrowOffset, pixel), pixel);
    }

    // JC | 2026-11-13 | 기존 원과 구분선이 같은 계산 결과를 사용합니다. 원의 반지름·중심·픽셀 기준은 그대로입니다.
    internal void GetRingGeometry(out Vector2 center, out Vector2 inner, out Vector2 outer, out Vector2 pixel)
    {
        var rect = rectTransform.rect;
        center = rect.center;
        inner = rect.size * 0.5f;
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
        pixel = new Vector2(rect.width / Mathf.Max(.01f, Vector2.Distance(topLeft, topRight)),
            rect.height / Mathf.Max(.01f, Vector2.Distance(bottomLeft, topLeft)));
        // 흰 테두리의 안쪽 안티앨리어싱까지 붉은 불투명 코어로 덮습니다.
        var radius = inner;
        inner = radius * Mathf.Max(.1f, innerDiameter);
        outer = radius * Mathf.Max(innerDiameter + .01f, outerDiameter);
        // JC | 2026-11-13 | 튜토리얼도 실제 초상화 마스크의 중심·지름을 공유하여 슬롯별 정렬 기준 전환을 제거합니다.
        center += Vector2.Scale(centerOffset, pixel);
    }

    internal static void Band(VertexHelper vh, Vector2 center, Vector2 inner, Vector2 outer, Color innerColor, Color outerColor)
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
