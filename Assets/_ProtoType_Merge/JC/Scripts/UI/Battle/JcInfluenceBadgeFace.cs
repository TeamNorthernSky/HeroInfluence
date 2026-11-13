using UnityEngine;
using UnityEngine.UI;

// JC | 2026-11-12 | 기본 UI 재질을 사용하는 둥근 배지 면입니다. 스윕도 같은 메시 내부에서 계산해 마스크 밖으로 새지 않습니다.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class JcInfluenceBadgeFace : MaskableGraphic
{
    Color surface, border;
    float sweep = -1;
    // JC | 2026-11-13 | 실시간 색상 조절 중 실제 색이 바뀔 때만 메시를 다시 만듭니다.
    public void SetStyle(Color fill, Color edge) { if (surface == fill && border == edge) return; surface = fill; border = edge; SetVerticesDirty(); }
    public void SetSweep(float value) { if (sweep == value) return; sweep = value; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect area = GetPixelAdjustedRect();
        Draw(vh, area, 6, border, false);
        Draw(vh, new Rect(area.x + 1.5f, area.y + 1.5f, area.width - 3, area.height - 3), 4.5f, surface, true);
    }
    void Draw(VertexHelper vh, Rect r, float radius, Color tint, bool sheen)
    {
        if (r.width <= 0 || r.height <= 0) return;
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
        const int rows = 16, columns = 40;
        int first = vh.currentVertCount;
        for (int y = 0; y <= rows; y++)
        {
            float fy = (float)y / rows;
            float py = r.yMin + r.height * fy;
            float dy = Mathf.Max(0, radius - Mathf.Min(py - r.yMin, r.yMax - py));
            float inset = radius - Mathf.Sqrt(Mathf.Max(0, radius * radius - dy * dy));
            for (int x = 0; x <= columns; x++)
            {
                float fx = (float)x / columns;
                float px = Mathf.Lerp(r.xMin + inset, r.xMax - inset, fx);
                Color c = tint;
                if (sheen)
                {
                    c *= Mathf.Lerp(.87f, 1.10f, fy); c.a = tint.a;
                    if (sweep >= 0)
                    {
                        float band = Mathf.Abs(fx + fy * .18f - Mathf.Lerp(-.3f, 1.5f, sweep));
                        float light = Mathf.SmoothStep(.42f, 0, Mathf.Clamp01(band / .16f));
                        c = Color.Lerp(c, new Color(1,.94f,.72f,1), light);
                    }
                }
                vh.AddVert(new Vector3(px, py), c * color, Vector2.zero);
            }
        }
        for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
        {
            int i = first + y * (columns + 1) + x;
            vh.AddTriangle(i, i + columns + 1, i + 1);
            vh.AddTriangle(i + 1, i + columns + 1, i + columns + 2);
        }
    }
}
