using UnityEngine;

/// <summary>
/// Runtime-only selectable/hover marker for a hostage. Hostages intentionally do not
/// implement BattleCharactor, so they use this separate presentation component.
/// </summary>
[DisallowMultipleComponent]
public sealed class HostageTargetingVisual : MonoBehaviour
{
    private const int SegmentCount = 32;
    private const float Radius = 0.34f;

    private LineRenderer ring;
    private bool selectable;
    private bool hovered;

    public static HostageTargetingVisual Ensure(HostageBattleActor hostage)
    {
        if (hostage == null)
            return null;

        HostageTargetingVisual visual = hostage.GetComponent<HostageTargetingVisual>();
        return visual != null ? visual : hostage.gameObject.AddComponent<HostageTargetingVisual>();
    }

    public void SetSelectable(bool active)
    {
        selectable = active;
        ApplyState();
    }

    public void SetHovered(bool active)
    {
        hovered = active;
        ApplyState();
    }

    public void Clear()
    {
        selectable = false;
        hovered = false;
        ApplyState();
    }

    private void Awake()
    {
        CreateRing();
        ApplyState();
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDestroy()
    {
        if (ring != null && ring.sharedMaterial != null)
            Destroy(ring.sharedMaterial);
    }

    private void CreateRing()
    {
        GameObject ringObject = new GameObject("HostageTargetRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, 0.015f, 0f);

        ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = SegmentCount;
        ring.widthMultiplier = 0.025f;
        ring.alignment = LineAlignment.View;
        ring.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / SegmentCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius));
        }
    }

    private void ApplyState()
    {
        if (ring == null)
            return;

        bool active = selectable || hovered;
        ring.gameObject.SetActive(active);
        if (!active)
            return;

        Color color = hovered ? new Color(1f, 0.35f, 0.1f, 1f) : new Color(1f, 0.85f, 0.15f, 0.8f);
        ring.startColor = color;
        ring.endColor = color;
        ring.widthMultiplier = hovered ? 0.04f : 0.025f;
    }
}
