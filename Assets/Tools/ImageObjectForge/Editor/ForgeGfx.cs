using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal static class ForgeGfx
    {
        static Texture2D _white;
        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _white.SetPixel(0, 0, Color.white);
                    _white.Apply();
                    _white.hideFlags = HideFlags.HideAndDontSave;
                }
                return _white;
            }
        }

        public static void DrawLine(Vector2 a, Vector2 b, Color color, float thickness = 1.5f)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.0001f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var savedColor = GUI.color;
            var savedMatrix = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), White);
            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }

        public static void DrawRectOutline(Rect r, Color color, float thickness = 1f)
        {
            DrawLine(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), color, thickness);
            DrawLine(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), color, thickness);
            DrawLine(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), color, thickness);
            DrawLine(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), color, thickness);
        }

        public static void DrawCircle(Vector2 center, float radius, Color color, float thickness = 1.5f, int segments = 32)
        {
            if (radius < 0.5f) return;
            Vector2 prev = center + new Vector2(radius, 0);
            for (int i = 1; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 cur = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                DrawLine(prev, cur, color, thickness);
                prev = cur;
            }
        }

        public static void DrawEllipseOutline(Rect bbox, Color color, float thickness = 1.5f, int segments = 48)
        {
            float rx = bbox.width * 0.5f;
            float ry = bbox.height * 0.5f;
            if (rx < 0.5f || ry < 0.5f) return;
            float cx = bbox.center.x;
            float cy = bbox.center.y;
            Vector2 prev = new Vector2(cx + rx, cy);
            for (int i = 1; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 cur = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
                DrawLine(prev, cur, color, thickness);
                prev = cur;
            }
        }

        public static void DrawPolyline(Vector2[] pts, Color color, float thickness = 1.5f, bool close = false)
        {
            if (pts == null || pts.Length < 2) return;
            for (int i = 0; i < pts.Length - 1; i++)
                DrawLine(pts[i], pts[i + 1], color, thickness);
            if (close)
                DrawLine(pts[pts.Length - 1], pts[0], color, thickness);
        }

        public static void FilledRect(Rect r, Color color)
        {
            var saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, White);
            GUI.color = saved;
        }

        // -------- 타일 체커보드 텍스처 (1회 생성) --------
        static Texture2D _checkerTex;
        const int CheckerCell = 16;
        const int CheckerTile = CheckerCell * 8; // 128px

        public static Texture2D CheckerboardTex
        {
            get
            {
                if (_checkerTex == null)
                {
                    _checkerTex = new Texture2D(CheckerTile, CheckerTile, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Repeat,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    var c1 = new Color32(89, 89, 89, 255);
                    var c2 = new Color32(64, 64, 64, 255);
                    var px = new Color32[CheckerTile * CheckerTile];
                    for (int y = 0; y < CheckerTile; y++)
                    {
                        int cy = y / CheckerCell;
                        for (int x = 0; x < CheckerTile; x++)
                        {
                            int cx = x / CheckerCell;
                            px[y * CheckerTile + x] = ((cx + cy) & 1) == 0 ? c1 : c2;
                        }
                    }
                    _checkerTex.SetPixels32(px);
                    _checkerTex.Apply(false, true);
                }
                return _checkerTex;
            }
        }

        public static void DrawCheckerboard(Rect rect)
        {
            float tilesX = rect.width / CheckerTile;
            float tilesY = rect.height / CheckerTile;
            GUI.DrawTextureWithTexCoords(rect, CheckerboardTex, new Rect(0, 0, tilesX, tilesY));
        }

        // -------- 링 커서 텍스처 (1회 생성) --------
        static Texture2D _ringTex;
        const int RingSize = 128;

        public static Texture2D RingTex
        {
            get
            {
                if (_ringTex == null)
                {
                    _ringTex = new Texture2D(RingSize, RingSize, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    float half = RingSize * 0.5f;
                    float outer = half - 1f;
                    var px = new Color32[RingSize * RingSize];
                    for (int y = 0; y < RingSize; y++)
                    {
                        for (int x = 0; x < RingSize; x++)
                        {
                            float dx = x - half + 0.5f, dy = y - half + 0.5f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float ring = Mathf.Clamp01(1f - Mathf.Abs(d - outer) / 1.5f);
                            px[y * RingSize + x] = new Color32(255, 255, 255, (byte)(ring * 255));
                        }
                    }
                    _ringTex.SetPixels32(px);
                    _ringTex.Apply(false, true);
                }
                return _ringTex;
            }
        }

        public static void DrawRing(Vector2 center, float radius, Color color)
        {
            if (radius < 0.5f) return;
            float size = radius * 2f + 4f;
            var saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), RingTex, ScaleMode.StretchToFill, true);
            GUI.color = saved;
        }

        // 시드 마커 — 작은 사각 + 십자
        public static void DrawSeedMarker(Vector2 center, Color color, float size = 9f)
        {
            float h = size * 0.5f;
            // 외곽 사각
            DrawRectOutline(new Rect(center.x - h, center.y - h, size, size), color, 1f);
            // 십자 (외곽보다 살짝 길게)
            float ext = h + 2f;
            DrawLine(new Vector2(center.x - ext, center.y), new Vector2(center.x + ext, center.y), color, 1.2f);
            DrawLine(new Vector2(center.x, center.y - ext), new Vector2(center.x, center.y + ext), color, 1.2f);
        }
    }
}
