using System.Collections.Generic;
using UnityEngine;

public class PartyOcclusionFadeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private DecorativeObjectRegistry decorativeObjectRegistry;

    [Header("Fade")]
    [SerializeField] private Material transparentOverrideMaterial;
    [SerializeField, Range(0.05f, 1f)] private float occludedAlpha = 0.45f;
    [SerializeField, Min(0f)] private float checkInterval = 0.1f;
    [SerializeField] private Vector3 partyFocusOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField, Min(0f)] private float boundsPadding;

    private readonly HashSet<DecorativeObjectPlacement> fadedObjects = new HashSet<DecorativeObjectPlacement>();
    private readonly HashSet<DecorativeObjectPlacement> currentOccluders = new HashSet<DecorativeObjectPlacement>();
    private float nextCheckTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeRegistry();
        nextCheckTime = 0f;
    }

    private void OnDisable()
    {
        UnsubscribeRegistry();
        RestoreAll();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextCheckTime)
            return;

        nextCheckTime = Time.unscaledTime + checkInterval;
        RefreshOcclusion();
    }

    private void RefreshOcclusion()
    {
        ResolveReferences();

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (cameraToUse == null || party == null || decorativeObjectRegistry == null)
        {
            RestoreAll();
            return;
        }

        Vector3 from = cameraToUse.transform.position;
        Vector3 to = party.transform.position + partyFocusOffset;
        Vector3 segment = to - from;
        float segmentLength = segment.magnitude;
        if (segmentLength <= Mathf.Epsilon)
        {
            RestoreAll();
            return;
        }

        Ray ray = new Ray(from, segment / segmentLength);
        currentOccluders.Clear();

        IReadOnlyList<DecorativeObjectPlacement> objects = decorativeObjectRegistry.DecorativeObjects;
        for (int i = 0; i < objects.Count; i++)
        {
            DecorativeObjectPlacement decorativeObject = objects[i];
            if (decorativeObject == null || !decorativeObject.isActiveAndEnabled)
                continue;

            if (!decorativeObject.TryGetRenderBounds(out Bounds bounds, boundsPadding))
                continue;

            if (!bounds.IntersectRay(ray, out float distance) || distance > segmentLength)
                continue;

            currentOccluders.Add(decorativeObject);
            decorativeObject.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
        }

        RestoreNoLongerOccluding();

        fadedObjects.Clear();
        foreach (DecorativeObjectPlacement decorativeObject in currentOccluders)
            fadedObjects.Add(decorativeObject);
    }

    private void RestoreNoLongerOccluding()
    {
        foreach (DecorativeObjectPlacement decorativeObject in fadedObjects)
        {
            if (decorativeObject == null || currentOccluders.Contains(decorativeObject))
                continue;

            decorativeObject.RestoreOcclusionFade();
        }
    }

    private void RestoreAll()
    {
        foreach (DecorativeObjectPlacement decorativeObject in fadedObjects)
        {
            if (decorativeObject == null)
                continue;

            decorativeObject.RestoreOcclusionFade();
        }

        fadedObjects.Clear();
        currentOccluders.Clear();
    }

    private void HandleDecorativeObjectUnregistered(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject != null)
            decorativeObject.RestoreOcclusionFade();

        fadedObjects.Remove(decorativeObject);
        currentOccluders.Remove(decorativeObject);
    }

    private void SubscribeRegistry()
    {
        if (decorativeObjectRegistry == null)
            return;

        decorativeObjectRegistry.DecorativeObjectUnregistered -= HandleDecorativeObjectUnregistered;
        decorativeObjectRegistry.DecorativeObjectUnregistered += HandleDecorativeObjectUnregistered;
    }

    private void UnsubscribeRegistry()
    {
        if (decorativeObjectRegistry == null)
            return;

        decorativeObjectRegistry.DecorativeObjectUnregistered -= HandleDecorativeObjectUnregistered;
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
        if (decorativeObjectRegistry == null)
            decorativeObjectRegistry = FindFirstObjectByType<DecorativeObjectRegistry>();
    }
}
