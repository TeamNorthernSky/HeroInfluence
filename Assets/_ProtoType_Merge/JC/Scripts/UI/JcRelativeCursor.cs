using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>독립 Overlay Canvas에서 원본 텍스처를 재사용하는 소프트웨어 커서.</summary>
public sealed class JcRelativeCursor : IDisposable
{
    private GameObject root;
    private RawImage image;
    private RectTransform rect;
    private bool active;
    private bool scrolling;

    // 표시 픽셀 좌표를 렌더 좌표로 변환하고, 텍스처 전체가 보이도록 경계 안에 붙인다.
    public static Vector2 EdgePosition(Vector2 pointer, Vector2 displaySize, Vector2 renderSize, Vector2 cursorSize, Vector2 pivot)
    {
        Vector2 point = new Vector2(pointer.x / Mathf.Max(1, displaySize.x) * renderSize.x,
            pointer.y / Mathf.Max(1, displaySize.y) * renderSize.y);
        Vector2 size = Vector2.Min(cursorSize, renderSize);
        return new Vector2(Mathf.Clamp(point.x, size.x * pivot.x, renderSize.x - size.x * (1 - pivot.x)),
            Mathf.Clamp(point.y, size.y * pivot.y, renderSize.y - size.y * (1 - pivot.y)));
    }

    // 렌더 픽셀 좌표계에서 한 번만 배율 적용. GameView의 표시 배율은 Unity가 이후 적용한다.
    public static float RenderSize(float renderHeight, float referenceSize)
        => Mathf.Max(1, renderHeight) / 1080f * Mathf.Clamp(referenceSize, 1, 512);
    public static Vector2 Pivot(Vector2 hotspot, int width, int height)
        => new Vector2(Mathf.Clamp01(hotspot.x / Mathf.Max(1, width)),
            1 - Mathf.Clamp01(hotspot.y / Mathf.Max(1, height)));

    private void EnsureCanvas()
    {
        if (root != null) return;
        root = new GameObject("JC Runtime Cursor", typeof(Canvas));
        root.hideFlags = HideFlags.HideAndDontSave;
        if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        canvas.scaleFactor = 1;
        // GraphicRaycaster를 추가하지 않아 클릭 대상이 되지 않는다.
        var child = new GameObject("Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        child.transform.SetParent(root.transform, false);
        image = child.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.maskable = false;
        image.enabled = false;
        rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        Canvas.preWillRenderCanvases += BeforeRender;
    }

    public void Show(Texture2D texture, Vector2 hotspot, float referenceSize, bool isScrolling = false)
    {
        if (texture == null) { Release(); return; }
        EnsureCanvas();
        scrolling = isScrolling;
        if (image.texture != texture) image.texture = texture;
        Vector2 pivot = Pivot(hotspot, texture.width, texture.height);
        if (rect.pivot != pivot) rect.pivot = pivot;
        Vector2 size = Vector2.one * RenderSize(Screen.height, referenceSize);
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        if (!active)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            image.enabled = true;
            active = true;
        }
        BeforeRender();
    }

    private void BeforeRender()
    {
        if (!active || rect == null) return;
        bool inside = JcPointerInput.Inside;
        // 화면 밖에서는 스크롤 중에만 경계 표시를 유지하고 OS 커서는 계속 보이게 한다.
        if (!JcPointerInput.CanControl || (!inside && (!scrolling || JcPointerInput.ScrollDirection.sqrMagnitude < .01f)))
        { Release(); return; }
        if (Cursor.visible != !inside) Cursor.visible = !inside;
        Vector2 point = inside ? (Vector2)Input.mousePosition
            : EdgePosition(JcPointerInput.Position, JcPointerInput.Size,
                new Vector2(Screen.width, Screen.height), rect.sizeDelta, rect.pivot);
        if (rect.anchoredPosition != point) rect.anchoredPosition = point;
    }

    public void Release()
    {
        if (!active) return;
        active = false;
        if (image != null) image.enabled = false;
        Cursor.visible = true;
    }

    public void Dispose()
    {
        Release();
        Canvas.preWillRenderCanvases -= BeforeRender;
        if (root != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
        root = null;
        image = null;
        rect = null;
    }
}
