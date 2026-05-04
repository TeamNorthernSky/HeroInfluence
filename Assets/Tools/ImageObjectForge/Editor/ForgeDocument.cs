using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal class ForgeDocument
    {
        public Texture2D Source;
        public byte[] Mask;
        public Texture2D MaskOverlay;
        public string SourceAssetPath;
        public string SourceStem;

        public int Width => Source != null ? Source.width : 0;
        public int Height => Source != null ? Source.height : 0;
        public bool HasImage => Source != null && Mask != null;

        static readonly Color32 OverlayOn = new Color32(255, 60, 60, 140);
        static readonly Color32 OverlayOff = new Color32(0, 0, 0, 0);

        public void SetSource(Texture2D tex, string assetPath)
        {
            DisposeInternal();
            Source = tex;
            SourceAssetPath = assetPath;
            SourceStem = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            int n = tex.width * tex.height;
            Mask = new byte[n];
            MaskOverlay = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var clear = new Color32[n];
            MaskOverlay.SetPixels32(clear);
            MaskOverlay.Apply(false);
        }

        public void ClearMask()
        {
            if (Mask == null) return;
            System.Array.Clear(Mask, 0, Mask.Length);
            RebuildOverlayAll();
        }

        public void RebuildOverlayAll()
        {
            if (Mask == null || MaskOverlay == null) return;
            var cols = new Color32[Mask.Length];
            for (int i = 0; i < Mask.Length; i++)
                cols[i] = Mask[i] != 0 ? OverlayOn : OverlayOff;
            MaskOverlay.SetPixels32(cols);
            MaskOverlay.Apply(false);
        }

        public void RebuildOverlayRect(RectInt r)
        {
            if (Mask == null || MaskOverlay == null) return;
            ClipRect(ref r);
            if (r.width <= 0 || r.height <= 0) return;
            var cols = new Color32[r.width * r.height];
            int W = Width;
            for (int dy = 0; dy < r.height; dy++)
            {
                int maskRow = (r.y + dy) * W + r.x;
                int colRow = dy * r.width;
                for (int dx = 0; dx < r.width; dx++)
                    cols[colRow + dx] = Mask[maskRow + dx] != 0 ? OverlayOn : OverlayOff;
            }
            MaskOverlay.SetPixels32(r.x, r.y, r.width, r.height, cols);
            MaskOverlay.Apply(false);
        }

        public void ClipRect(ref RectInt r)
        {
            int x0 = Mathf.Max(0, r.x);
            int y0 = Mathf.Max(0, r.y);
            int x1 = Mathf.Min(Width, r.x + r.width);
            int y1 = Mathf.Min(Height, r.y + r.height);
            r = new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        public int CountMaskPixels()
        {
            if (Mask == null) return 0;
            int c = 0;
            for (int i = 0; i < Mask.Length; i++) if (Mask[i] != 0) c++;
            return c;
        }

        public void DisposeInternal()
        {
            if (Source != null) { Object.DestroyImmediate(Source); Source = null; }
            if (MaskOverlay != null) { Object.DestroyImmediate(MaskOverlay); MaskOverlay = null; }
            Mask = null;
            SourceAssetPath = null;
            SourceStem = null;
        }
    }
}
