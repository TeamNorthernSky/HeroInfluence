using UnityEngine;
using UnityEngine.UI;

/// <summary>크기가 변해도 선 두께를 유지하는 둥근 안내 테두리.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TutorialPulseFrame : MaskableGraphic
{
    [SerializeField, Min(0f)] private float thickness = 3f;
    [SerializeField, Min(0f)] private float cornerRadius = 18f;
    [SerializeField] private bool filled;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);
        float width = Mathf.Min(thickness, Mathf.Min(rect.width, rect.height) * 0.5f);
        const int steps = 12;
        const int count = 4 * (steps + 1);
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(
                corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
            for (int step = 0; step <= steps; step++)
            {
                float angle = (90f - corner * 90f - step * 90f / steps) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * radius, color, Vector2.zero);
                mesh.AddVert(filled ? rect.center : center + direction * Mathf.Max(0f, radius - width), color, Vector2.zero);
            }
        }
        for (int i = 0; i < count; i++)
        {
            int a = i * 2;
            int b = ((i + 1) % count) * 2;
            mesh.AddTriangle(a, b, a + 1);
            mesh.AddTriangle(b, b + 1, a + 1);
        }
    }
}
