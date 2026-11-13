using UnityEngine;
using UnityEngine.UI;

// JC | 2026-11-13 | 초상화 배경색에 의한 두께 착시를 줄이는 고정 색 구분선입니다. 입력·원 점멸·화살표에 관여하지 않습니다.
public sealed class JcBattleHoverRingDivider : MaskableGraphic
{
    private JcBattleHoverRing owner;
    private float width;

    public void Show(JcBattleHoverRing ring, float pixels, Color tint, float opacity)
    {
        pixels = Mathf.Clamp(pixels, 0f, 6f);
        tint.a = Mathf.Clamp01(opacity);
        if (owner != ring || width != pixels) { owner = ring; width = pixels; SetVerticesDirty(); }
        color = tint;
        enabled = pixels > 0f && tint.a > 0f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!owner || width <= 0f) return;
        owner.GetRingGeometry(out var center, out var boundary, out _, out var pixel);
        // JC | 2026-11-13 | 붉은 원의 안쪽 경계에서 초상화 방향으로만 선을 추가해 원의 내·외경과 중심을 보존합니다.
        var inside = Vector2.Max(Vector2.zero, boundary - Vector2.Scale(pixel, Vector2.one * width));
        JcBattleHoverRing.Band(vh, center, inside, boundary, color, color);
    }
}
