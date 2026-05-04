using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal static class ForgeBrush
    {
        public enum BrushShape { Circle, Square }

        // 원형 스탬프. 이미지(y-up) 좌표의 center에 radius로 찍음.
        public static bool StampDisk(byte[] mask, int imgW, int imgH, Vector2 center, float radius, bool paint, out RectInt dirty)
        {
            int cx = Mathf.RoundToInt(center.x);
            int cy = Mathf.RoundToInt(center.y);
            int r = Mathf.Max(1, Mathf.RoundToInt(radius));
            int x0 = Mathf.Max(0, cx - r);
            int y0 = Mathf.Max(0, cy - r);
            int x1 = Mathf.Min(imgW - 1, cx + r);
            int y1 = Mathf.Min(imgH - 1, cy + r);
            if (x1 < x0 || y1 < y0) { dirty = new RectInt(0, 0, 0, 0); return false; }

            byte val = paint ? (byte)255 : (byte)0;
            int r2 = r * r;
            bool changed = false;
            for (int y = y0; y <= y1; y++)
            {
                int dy = y - cy; int dy2 = dy * dy;
                int row = y * imgW;
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dy2 <= r2)
                    {
                        int idx = row + x;
                        if (mask[idx] != val) { mask[idx] = val; changed = true; }
                    }
                }
            }
            dirty = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            return changed;
        }

        // 정사각형 스탬프. halfSide는 반변(원형 브러시의 radius에 대응).
        public static bool StampSquare(byte[] mask, int imgW, int imgH, Vector2 center, float halfSide, bool paint, out RectInt dirty)
        {
            int cx = Mathf.RoundToInt(center.x);
            int cy = Mathf.RoundToInt(center.y);
            int h = Mathf.Max(1, Mathf.RoundToInt(halfSide));
            int x0 = Mathf.Max(0, cx - h);
            int y0 = Mathf.Max(0, cy - h);
            int x1 = Mathf.Min(imgW - 1, cx + h);
            int y1 = Mathf.Min(imgH - 1, cy + h);
            if (x1 < x0 || y1 < y0) { dirty = new RectInt(0, 0, 0, 0); return false; }

            byte val = paint ? (byte)255 : (byte)0;
            bool changed = false;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * imgW;
                for (int x = x0; x <= x1; x++)
                {
                    int idx = row + x;
                    if (mask[idx] != val) { mask[idx] = val; changed = true; }
                }
            }
            dirty = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            return changed;
        }

        // shape 디스패처
        public static bool Stamp(byte[] mask, int imgW, int imgH, Vector2 center, float radius, bool paint, BrushShape shape, out RectInt dirty)
        {
            return shape == BrushShape.Square
                ? StampSquare(mask, imgW, imgH, center, radius, paint, out dirty)
                : StampDisk(mask, imgW, imgH, center, radius, paint, out dirty);
        }

        // 두 점 사이를 shape 스탬프로 이어 그림. 반환 dirty는 통합 영역.
        public static bool Stroke(byte[] mask, int imgW, int imgH, Vector2 a, Vector2 b, float radius, bool paint, BrushShape shape, out RectInt dirty)
        {
            float step = Mathf.Max(1f, radius * 0.5f);
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / step));
            bool changed = false;
            int xmin = int.MaxValue, ymin = int.MaxValue, xmax = int.MinValue, ymax = int.MinValue;

            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, steps == 0 ? 0f : (float)i / steps);
                if (Stamp(mask, imgW, imgH, p, radius, paint, shape, out _))
                    changed = true;

                int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
                int r = Mathf.Max(1, Mathf.RoundToInt(radius));
                int bx0 = Mathf.Max(0, cx - r);
                int by0 = Mathf.Max(0, cy - r);
                int bx1 = Mathf.Min(imgW - 1, cx + r);
                int by1 = Mathf.Min(imgH - 1, cy + r);
                if (bx0 <= bx1 && by0 <= by1)
                {
                    if (bx0 < xmin) xmin = bx0;
                    if (by0 < ymin) ymin = by0;
                    if (bx1 > xmax) xmax = bx1;
                    if (by1 > ymax) ymax = by1;
                }
            }

            if (xmax < xmin || ymax < ymin) { dirty = new RectInt(0, 0, 0, 0); return false; }
            dirty = new RectInt(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1);
            return changed;
        }

        // 기존 시그니처 유지 (원형 전용 레거시 호출자 호환).
        public static bool StrokeLine(byte[] mask, int imgW, int imgH, Vector2 a, Vector2 b, float radius, bool paint, out RectInt dirty)
        {
            return Stroke(mask, imgW, imgH, a, b, radius, paint, BrushShape.Circle, out dirty);
        }
    }
}
