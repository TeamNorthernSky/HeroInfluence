using System.Collections.Generic;
using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal static class ForgePolygon
    {
        // even-odd 스캔라인 채우기. 이미지(y-up) 좌표의 다각형을 마스크에 추가(add=true) 또는 제거(add=false).
        // 반환: 실제 수정된 영역의 bbox. 변화 없으면 width=height=0.
        public static RectInt FillToMask(byte[] mask, int imgW, int imgH, IList<Vector2> pts, bool add)
        {
            if (pts == null || pts.Count < 3) return new RectInt(0, 0, 0, 0);

            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            int y0 = Mathf.Max(0, Mathf.CeilToInt(minY - 0.5f));
            int y1 = Mathf.Min(imgH - 1, Mathf.FloorToInt(maxY - 0.5f));
            if (y1 < y0) return new RectInt(0, 0, 0, 0);

            byte val = add ? (byte)255 : (byte)0;
            var xs = new List<float>(pts.Count);
            int n = pts.Count;

            int actualMinX = int.MaxValue, actualMaxX = int.MinValue;
            int actualMinY = int.MaxValue, actualMaxY = int.MinValue;

            for (int y = y0; y <= y1; y++)
            {
                xs.Clear();
                float yf = y + 0.5f;
                for (int i = 0; i < n; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % n];
                    if ((a.y <= yf && b.y > yf) || (b.y <= yf && a.y > yf))
                    {
                        float t = (yf - a.y) / (b.y - a.y);
                        xs.Add(a.x + t * (b.x - a.x));
                    }
                }
                xs.Sort();
                int row = y * imgW;
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    int sx = Mathf.Max(0, Mathf.CeilToInt(xs[i] - 0.5f));
                    int ex = Mathf.Min(imgW - 1, Mathf.FloorToInt(xs[i + 1] - 0.5f));
                    if (sx > ex) continue;
                    for (int x = sx; x <= ex; x++)
                        mask[row + x] = val;
                    if (sx < actualMinX) actualMinX = sx;
                    if (ex > actualMaxX) actualMaxX = ex;
                    if (y < actualMinY) actualMinY = y;
                    if (y > actualMaxY) actualMaxY = y;
                }
            }
            if (actualMinX > actualMaxX) return new RectInt(0, 0, 0, 0);
            return new RectInt(actualMinX, actualMinY, actualMaxX - actualMinX + 1, actualMaxY - actualMinY + 1);
        }
    }
}
