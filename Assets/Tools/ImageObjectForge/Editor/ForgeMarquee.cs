using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal static class ForgeMarquee
    {
        // 이미지(y-up) 좌표의 직사각형을 마스크에 추가(add=true) 또는 제거(add=false).
        // 반환: 실제 수정된 영역의 bbox. 변화 없으면 width=height=0.
        public static RectInt FillRect(byte[] mask, int imgW, int imgH, RectInt rectImg, bool add)
        {
            int x0 = Mathf.Max(0, rectImg.xMin);
            int y0 = Mathf.Max(0, rectImg.yMin);
            int x1 = Mathf.Min(imgW - 1, rectImg.xMax - 1);
            int y1 = Mathf.Min(imgH - 1, rectImg.yMax - 1);
            if (x0 > x1 || y0 > y1) return new RectInt(0, 0, 0, 0);

            byte val = add ? (byte)255 : (byte)0;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * imgW;
                for (int x = x0; x <= x1; x++)
                    mask[row + x] = val;
            }
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        // 이미지(y-up) 좌표의 직사각형 bbox에 내접하는 타원을 마스크에 추가/제거.
        // 반환: 실제 수정된 영역의 bbox.
        public static RectInt FillEllipse(byte[] mask, int imgW, int imgH, RectInt rectImg, bool add)
        {
            int x0 = Mathf.Max(0, rectImg.xMin);
            int y0 = Mathf.Max(0, rectImg.yMin);
            int x1 = Mathf.Min(imgW - 1, rectImg.xMax - 1);
            int y1 = Mathf.Min(imgH - 1, rectImg.yMax - 1);
            if (x0 > x1 || y0 > y1) return new RectInt(0, 0, 0, 0);

            float rx = rectImg.width * 0.5f;
            float ry = rectImg.height * 0.5f;
            if (rx < 0.5f || ry < 0.5f) return new RectInt(0, 0, 0, 0);
            float cx = rectImg.xMin + rx - 0.5f;   // 픽셀 중심 보정
            float cy = rectImg.yMin + ry - 0.5f;
            float rx2 = rx * rx, ry2 = ry * ry;

            byte val = add ? (byte)255 : (byte)0;
            int aMinX = int.MaxValue, aMaxX = int.MinValue;
            int aMinY = int.MaxValue, aMaxY = int.MinValue;

            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                float t = 1f - (dy * dy) / ry2;
                if (t < 0f) continue;
                float dxMax = Mathf.Sqrt(t * rx2);
                int sx = Mathf.Max(x0, Mathf.CeilToInt(cx - dxMax));
                int ex = Mathf.Min(x1, Mathf.FloorToInt(cx + dxMax));
                if (sx > ex) continue;
                int row = y * imgW;
                for (int x = sx; x <= ex; x++) mask[row + x] = val;
                if (sx < aMinX) aMinX = sx;
                if (ex > aMaxX) aMaxX = ex;
                if (y < aMinY) aMinY = y;
                if (y > aMaxY) aMaxY = y;
            }
            if (aMinX > aMaxX) return new RectInt(0, 0, 0, 0);
            return new RectInt(aMinX, aMinY, aMaxX - aMinX + 1, aMaxY - aMinY + 1);
        }

        // 두 이미지(y-up) 좌표 점에서 정규화된 RectInt 생성. Shift+드래그로 정사각형 제약 가능.
        public static RectInt RectFromPoints(Vector2 a, Vector2 b, bool square)
        {
            if (square)
            {
                float side = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
                b = new Vector2(a.x + Mathf.Sign(b.x - a.x == 0 ? 1 : b.x - a.x) * side,
                                a.y + Mathf.Sign(b.y - a.y == 0 ? 1 : b.y - a.y) * side);
            }
            int x0 = Mathf.RoundToInt(Mathf.Min(a.x, b.x));
            int y0 = Mathf.RoundToInt(Mathf.Min(a.y, b.y));
            int x1 = Mathf.RoundToInt(Mathf.Max(a.x, b.x));
            int y1 = Mathf.RoundToInt(Mathf.Max(a.y, b.y));
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
    }
}
