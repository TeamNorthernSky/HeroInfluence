using UnityEngine;

/// <summary>Projects a world building into a UI focus target for the existing tutorial shader.</summary>
[DisallowMultipleComponent]
public sealed class TutorialBuildingFocusTarget : MonoBehaviour
{
    private Transform building;
    private Renderer[] renderers;
    private RectTransform focus;
    private RectTransform overlay;
    private readonly Vector3[] corners = new Vector3[8];

    public static RectTransform Resolve(Component owner, Transform target, RectTransform parent)
    {
        if (owner == null || target == null || parent == null) return null;
        var projection = owner.GetComponent<TutorialBuildingFocusTarget>();
        if (projection == null) projection = owner.gameObject.AddComponent<TutorialBuildingFocusTarget>();
        if (projection.building != target)
        {
            projection.building = target;
            projection.renderers = target.GetComponentsInChildren<Renderer>(true);
        }
        projection.overlay = parent;
        if (projection.focus == null)
        {
            var host = new GameObject("Association Tutorial Focus", typeof(RectTransform));
            host.layer = parent.gameObject.layer;
            projection.focus = (RectTransform)host.transform;
        }
        if (projection.focus.parent != parent) projection.focus.SetParent(parent, false);
        projection.Refresh();
        return projection.focus;
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (focus == null) return;
        var camera = Camera.main;
        if (building == null || !building.gameObject.activeInHierarchy || overlay == null || camera == null)
        {
            focus.gameObject.SetActive(false);
            return;
        }
        Bounds bounds = new Bounds(building.position, Vector3.one);
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        var canvas = overlay.GetComponentInParent<Canvas>();
        var uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera : null;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var point = camera.WorldToScreenPoint(corners[i]);
            if (point.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, point, uiCamera, out var local))
            {
                focus.gameObject.SetActive(false);
                return;
            }
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        focus.anchorMin = focus.anchorMax = focus.pivot = new Vector2(.5f, .5f);
        focus.localPosition = (min + max) * .5f;
        focus.sizeDelta = max - min;
        focus.gameObject.SetActive(true);
    }

    private void OnDisable() { if (focus != null) focus.gameObject.SetActive(false); }
    private void OnDestroy() { if (focus != null) Destroy(focus.gameObject); }
}
