using UnityEngine;

/// <summary>Owns a speech bubble under this exploration character.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PartyUnitState))]
public sealed class ExplorationUnitBubble : MonoBehaviour
{
    private ExplorationActionPointBubble bubble;

    public ExplorationActionPointBubble Bubble => bubble;
    public int UnitIndex => GetComponent<PartyUnitState>().UnitIndex;

    /// <summary>Tests the main camera's frustum and rendering layers, not other cameras or scene-view visibility.</summary>
    public bool IsVisibleTo(Camera camera)
    {
        if (camera == null || !camera.isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer.forceRenderingOff || renderer is ParticleSystemRenderer || renderer is TrailRenderer ||
                renderer is LineRenderer || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
            if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) return true;
        }
        return false;
    }

    public bool Prepare(ExplorationActionPointBubble prefab)
    {
        if (bubble != null) return true;
        if (prefab == null) return false;
        bubble = Instantiate(prefab, transform, false);
        bubble.name = "MovementNoticeBubble";
        bubble.transform.localScale = Vector3.one;
        bubble.AttachToCharacter();
        bubble.Hide();
        return true;
    }

    public bool Show(Camera camera, string message = null)
    {
        if (bubble == null || camera == null || !gameObject.activeInHierarchy) return false;
        bubble.Present(transform, camera, message);
        return true;
    }

    public void Hide()
    {
        if (bubble != null) bubble.Hide();
    }

    private void OnDisable() => Hide();
}
