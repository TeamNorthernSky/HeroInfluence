using UnityEngine;

namespace Orora.ImageObjectForge
{
    // 이미지 좌표계 규약: y=0이 하단(텍스처 표준). 마우스 입력 매핑 시에만 Y 뒤집음.
    internal class ForgeViewport
    {
        public float Zoom = 1f;
        public Vector2 Pan = Vector2.zero; // canvasRect 기준, 표시 사각형 좌상단의 (x, y) top-down 오프셋

        const float MinZoom = 0.05f;
        const float MaxZoom = 32f;

        public Rect GetImageDisplayRect(Rect canvasRect, int imgW, int imgH)
        {
            return new Rect(canvasRect.x + Pan.x, canvasRect.y + Pan.y, imgW * Zoom, imgH * Zoom);
        }

        // 스크린 → 이미지(y-up)
        public Vector2 ScreenToImage(Rect canvasRect, Vector2 screen, int imgH)
        {
            float localX = (screen.x - canvasRect.x - Pan.x) / Zoom;
            float localYTop = (screen.y - canvasRect.y - Pan.y) / Zoom;
            return new Vector2(localX, imgH - localYTop);
        }

        // 이미지(y-up) → 스크린
        public Vector2 ImageToScreen(Rect canvasRect, Vector2 img, int imgH)
        {
            float localYTop = imgH - img.y;
            return new Vector2(canvasRect.x + Pan.x + img.x * Zoom,
                               canvasRect.y + Pan.y + localYTop * Zoom);
        }

        public void ZoomAt(Rect canvasRect, Vector2 screenPivot, float wheelDelta, int imgH)
        {
            // wheelDelta: 음수=확대, 양수=축소 (Unity 관례)
            Vector2 imgPivot = ScreenToImage(canvasRect, screenPivot, imgH);
            float factor = Mathf.Pow(1.1f, -wheelDelta);
            float newZoom = Mathf.Clamp(Zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(newZoom, Zoom)) return;
            Zoom = newZoom;
            // imgPivot이 여전히 screenPivot에 놓이도록 Pan 재계산
            float localYTopPivot = imgH - imgPivot.y;
            Pan = new Vector2(screenPivot.x - canvasRect.x - imgPivot.x * Zoom,
                              screenPivot.y - canvasRect.y - localYTopPivot * Zoom);
        }

        public void Fit(Rect canvasRect, int imgW, int imgH)
        {
            if (imgW <= 0 || imgH <= 0 || canvasRect.width <= 0 || canvasRect.height <= 0) return;
            float zx = canvasRect.width / imgW;
            float zy = canvasRect.height / imgH;
            Zoom = Mathf.Clamp(Mathf.Min(zx, zy) * 0.95f, MinZoom, MaxZoom);
            Pan = new Vector2((canvasRect.width - imgW * Zoom) * 0.5f,
                              (canvasRect.height - imgH * Zoom) * 0.5f);
        }

        public void PanBy(Vector2 screenDelta)
        {
            Pan += screenDelta;
        }
    }
}
