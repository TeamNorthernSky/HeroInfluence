using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Screen-space field remaining between exploration HUD groups.</summary>
public static class ExplorationBubbleFieldArea
{
    private static readonly List<RectTransform> targets = new List<RectTransform>();
    private static readonly List<Rect> excluded = new List<Rect>();
    private static readonly Vector3[] corners = new Vector3[4];
    private static float nextSearch;
    private static int sceneHandle;
    private static int checkedFrame = -1;
    private static Camera checkedCamera;
    private static Rect field;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        targets.Clear();
        excluded.Clear();
        nextSearch = 0f;
        checkedFrame = -1;
        checkedCamera = null;
        sceneHandle = 0;
    }

    public static bool Contains(Camera camera, Vector3 worldPosition)
    {
        if (camera == null) return false;
        if (checkedFrame != Time.frameCount || checkedCamera != camera)
        {
            UpdateRegions(camera);
            checkedFrame = Time.frameCount;
            checkedCamera = camera;
        }
        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f || !field.Contains(screen)) return false;
        foreach (Rect rect in excluded) if (rect.Contains(screen)) return false;
        return true;
    }

    private static void UpdateRegions(Camera camera)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (sceneHandle != scene.handle || Time.unscaledTime >= nextSearch)
        {
            sceneHandle = scene.handle;
            nextSearch = Time.unscaledTime + .5f;
            targets.Clear();
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled || canvas.renderMode == RenderMode.WorldSpace ||
                    (canvas.gameObject.scene != scene && canvas.gameObject.scene.name != "DontDestroyOnLoad")) continue;
                foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
                    if (IsHudGroup(rect.name.Replace("(Clone)", ""))) targets.Add(rect);
            }
        }

        field = camera.pixelRect;
        excluded.Clear();
        foreach (RectTransform target in targets)
        {
            if (target == null || !target.gameObject.activeInHierarchy || !TryGetVisibleBounds(target, out Rect bounds)) continue;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            float padding = 8f * (canvas != null ? canvas.rootCanvas.scaleFactor : 1f);
            string name = target.name.Replace("(Clone)", "");
            // The upper/lower HUD bars and party column bound the main field.
            if (name == "PartyPanel") field.xMin = Mathf.Max(field.xMin, bounds.xMax + padding);
            else if (name == "TopBar" || name == "TopBar 1" || name == "PNL_Explor_TopBar")
                field.yMax = Mathf.Min(field.yMax, bounds.yMin - padding);
            else if (name == "MoveGauge" || name == "Move Gauge")
                field.yMin = Mathf.Max(field.yMin, bounds.yMax + padding);
            else
                excluded.Add(Rect.MinMaxRect(bounds.xMin - padding, bounds.yMin - padding,
                    bounds.xMax + padding, bounds.yMax + padding));
        }
    }

    private static bool IsHudGroup(string name)
        => name == "PartyPanel" || name == "TopBar" || name == "TopBar 1" || name == "PNL_Explor_TopBar" ||
           name == "MoveGauge" || name == "Move Gauge" || name == "MiniMap" || name == "Minimap" ||
           name == "PNL_Explor_Minmap" || name == "TurnPanel";

    private static bool TryGetVisibleBounds(RectTransform target, out Rect bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.isActiveAndEnabled || graphic.color.a <= .001f || !IsVisible(graphic.transform)) continue;
            Canvas canvas = graphic.canvas;
            if (canvas == null || !canvas.isActiveAndEnabled || !canvas.rootCanvas.isActiveAndEnabled) continue;
            Camera eventCamera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            graphic.rectTransform.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(eventCamera, corner);
                if (!found) { bounds = new Rect(point, Vector2.zero); found = true; }
                else bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, point.x), Mathf.Min(bounds.yMin, point.y),
                    Mathf.Max(bounds.xMax, point.x), Mathf.Max(bounds.yMax, point.y));
            }
        }
        return found;
    }

    private static bool IsVisible(Transform transform)
    {
        foreach (CanvasGroup group in transform.GetComponentsInParent<CanvasGroup>())
        {
            if (group.alpha <= .001f) return false;
            if (group.ignoreParentGroups) break;
        }
        return true;
    }
}
