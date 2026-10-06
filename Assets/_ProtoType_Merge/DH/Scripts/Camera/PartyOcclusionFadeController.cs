using System.Collections.Generic;
using UnityEngine;

public class PartyOcclusionFadeController : MonoBehaviour
{
    private static readonly HashSet<PartyOcclusionFadeController> ActiveControllers = new HashSet<PartyOcclusionFadeController>();

    public static bool TryHasFadedObjects(UnityEngine.SceneManagement.Scene scene, out bool hasFaded)
    {
        bool found = false;
        hasFaded = false;
        foreach (PartyOcclusionFadeController controller in ActiveControllers)
        {
            if (controller == null || !controller.isActiveAndEnabled || controller.gameObject.scene != scene)
                continue;

            found = true;
            hasFaded |= controller.fadedObjects.Count > 0 || controller.fadedTargets.Count > 0;
        }

        return found;
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private DecorativeObjectRegistry decorativeObjectRegistry;
    [SerializeField] private PartyOcclusionFadeTargetRegistry occlusionFadeTargetRegistry;

    [Header("Fade")]
    [SerializeField] private Material transparentOverrideMaterial;
    [SerializeField, Range(0.05f, 1f)] private float occludedAlpha = 0.45f;
    [SerializeField, Min(0f)] private float checkInterval = 0.1f;
    [SerializeField] private Vector3 partyFocusOffset = new Vector3(0f, 0.8f, 0f);
    [Tooltip("가림 후보 검색용 Bounds의 전체 크기에 더할 월드 거리입니다. 0은 원래 크기이며, 실제 투명화 여부는 메시 표면과 파티 기준점까지의 선분 교차로 결정합니다. 씬·프리팹에 저장됩니다.")]
    [SerializeField, Min(0f)] private float boundsPadding;

    private readonly HashSet<DecorativeObjectPlacement> fadedObjects = new HashSet<DecorativeObjectPlacement>();
    private readonly HashSet<DecorativeObjectPlacement> currentOccluders = new HashSet<DecorativeObjectPlacement>();
    private readonly HashSet<PartyOcclusionFadeTarget> fadedTargets = new HashSet<PartyOcclusionFadeTarget>();
    private readonly HashSet<PartyOcclusionFadeTarget> currentTargetOccluders = new HashSet<PartyOcclusionFadeTarget>();
    private readonly JcBuildingMeshOcclusion meshOcclusion = new JcBuildingMeshOcclusion();
    private float nextCheckTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ActiveControllers.Add(this);
        ResolveReferences();
        SubscribeRegistry();
        nextCheckTime = 0f;
    }

    private void OnDisable()
    {
        ActiveControllers.Remove(this);
        UnsubscribeRegistry();
        RestoreAll();
        meshOcclusion.Clear();
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
        if (cameraToUse == null || party == null || (decorativeObjectRegistry == null && occlusionFadeTargetRegistry == null))
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

        currentOccluders.Clear();
        currentTargetOccluders.Clear();

        AddDecorativeOccluders(from, to);
        AddFadeTargetOccluders(from, to);

        RestoreNoLongerOccluding();
        RestoreTargetsNoLongerOccluding();

        fadedObjects.Clear();
        foreach (DecorativeObjectPlacement decorativeObject in currentOccluders)
            fadedObjects.Add(decorativeObject);

        fadedTargets.Clear();
        foreach (PartyOcclusionFadeTarget target in currentTargetOccluders)
            fadedTargets.Add(target);
    }

    private void AddDecorativeOccluders(Vector3 from, Vector3 to)
    {
        if (decorativeObjectRegistry == null)
            return;

        IReadOnlyList<DecorativeObjectPlacement> objects = decorativeObjectRegistry.DecorativeObjects;
        for (int i = 0; i < objects.Count; i++)
        {
            DecorativeObjectPlacement decorativeObject = objects[i];
            if (decorativeObject == null || !decorativeObject.isActiveAndEnabled)
                continue;

            if (!decorativeObject.TryGetRenderBounds(out Bounds bounds, boundsPadding))
                continue;
            if (!JcBuildingMeshOcclusion.IntersectsSegment(bounds, from, to))
                continue;
            if (!meshOcclusion.IsOccluded(decorativeObject, from, to))
                continue;

            currentOccluders.Add(decorativeObject);
            decorativeObject.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
        }
    }

    private void AddFadeTargetOccluders(Vector3 from, Vector3 to)
    {
        if (occlusionFadeTargetRegistry == null)
            return;

        IReadOnlyList<PartyOcclusionFadeTarget> targets = occlusionFadeTargetRegistry.Targets;
        for (int i = 0; i < targets.Count; i++)
        {
            PartyOcclusionFadeTarget target = targets[i];
            if (target == null || !target.isActiveAndEnabled)
                continue;

            if (!target.TryGetRenderBounds(out Bounds bounds, boundsPadding))
                continue;
            if (!JcBuildingMeshOcclusion.IntersectsSegment(bounds, from, to))
                continue;
            if (!meshOcclusion.IsOccluded(target, from, to))
                continue;

            currentTargetOccluders.Add(target);
            target.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
        }
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

    private void RestoreTargetsNoLongerOccluding()
    {
        foreach (PartyOcclusionFadeTarget target in fadedTargets)
        {
            if (target == null || currentTargetOccluders.Contains(target))
                continue;

            target.RestoreOcclusionFade();
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

        foreach (PartyOcclusionFadeTarget target in fadedTargets)
        {
            if (target == null)
                continue;

            target.RestoreOcclusionFade();
        }

        fadedObjects.Clear();
        currentOccluders.Clear();
        fadedTargets.Clear();
        currentTargetOccluders.Clear();
    }

    private void HandleDecorativeObjectUnregistered(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject != null)
            decorativeObject.RestoreOcclusionFade();

        fadedObjects.Remove(decorativeObject);
        currentOccluders.Remove(decorativeObject);
        if (decorativeObject != null)
            meshOcclusion.Remove(decorativeObject);
    }

    private void HandleFadeTargetUnregistered(PartyOcclusionFadeTarget target)
    {
        if (target != null)
            target.RestoreOcclusionFade();

        fadedTargets.Remove(target);
        currentTargetOccluders.Remove(target);
        if (target != null)
            meshOcclusion.Remove(target);
    }

    private void SubscribeRegistry()
    {
        if (decorativeObjectRegistry != null)
        {
            decorativeObjectRegistry.DecorativeObjectUnregistered -= HandleDecorativeObjectUnregistered;
            decorativeObjectRegistry.DecorativeObjectUnregistered += HandleDecorativeObjectUnregistered;
        }

        if (occlusionFadeTargetRegistry != null)
        {
            occlusionFadeTargetRegistry.TargetUnregistered -= HandleFadeTargetUnregistered;
            occlusionFadeTargetRegistry.TargetUnregistered += HandleFadeTargetUnregistered;
        }
    }

    private void UnsubscribeRegistry()
    {
        if (decorativeObjectRegistry != null)
            decorativeObjectRegistry.DecorativeObjectUnregistered -= HandleDecorativeObjectUnregistered;

        if (occlusionFadeTargetRegistry != null)
            occlusionFadeTargetRegistry.TargetUnregistered -= HandleFadeTargetUnregistered;
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
        if (decorativeObjectRegistry == null)
            decorativeObjectRegistry = FindFirstObjectByType<DecorativeObjectRegistry>();
        if (occlusionFadeTargetRegistry == null)
        {
            occlusionFadeTargetRegistry = FindFirstObjectByType<PartyOcclusionFadeTargetRegistry>();
            if (occlusionFadeTargetRegistry != null && isActiveAndEnabled)
            {
                occlusionFadeTargetRegistry.TargetUnregistered -= HandleFadeTargetUnregistered;
                occlusionFadeTargetRegistry.TargetUnregistered += HandleFadeTargetUnregistered;
            }
        }
    }
}
