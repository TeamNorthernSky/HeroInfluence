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
    [SerializeField, Min(0f)] private float boundsPadding;

    private readonly HashSet<DecorativeObjectPlacement> fadedObjects = new HashSet<DecorativeObjectPlacement>();
    private readonly HashSet<DecorativeObjectPlacement> currentOccluders = new HashSet<DecorativeObjectPlacement>();
    private readonly Dictionary<DecorativeObjectPlacement, float> clearSince = new Dictionary<DecorativeObjectPlacement, float>();
    private readonly HashSet<PartyOcclusionFadeTarget> fadedTargets = new HashSet<PartyOcclusionFadeTarget>();
    private readonly HashSet<PartyOcclusionFadeTarget> currentTargetOccluders = new HashSet<PartyOcclusionFadeTarget>();
    private readonly Dictionary<PartyOcclusionFadeTarget, float> targetClearSince = new Dictionary<PartyOcclusionFadeTarget, float>();
    private readonly JcBuildingMeshOcclusion buildingMeshOcclusion = new JcBuildingMeshOcclusion();
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
        buildingMeshOcclusion.Clear();
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

        Ray ray = new Ray(from, segment / segmentLength);
        currentOccluders.Clear();
        currentTargetOccluders.Clear();

        JcBuildingSilhouetteSettings tuning = JcBuildingSilhouetteController.TryGetSettings(cameraToUse, out JcBuildingSilhouetteSettings currentTuning)
            ? currentTuning
            : JcBuildingSilhouetteSettings.Default;

        AddDecorativeOccluders(from, party.transform.position, ray, segmentLength, tuning);
        AddFadeTargetOccluders(ray, segmentLength);

        RestoreNoLongerOccluding(party.IsMoving, tuning, Time.unscaledTime);
        RestoreTargetsNoLongerOccluding(party.IsMoving, tuning, Time.unscaledTime);

        fadedObjects.Clear();
        foreach (DecorativeObjectPlacement decorativeObject in currentOccluders)
            fadedObjects.Add(decorativeObject);

        fadedTargets.Clear();
        foreach (PartyOcclusionFadeTarget target in currentTargetOccluders)
            fadedTargets.Add(target);
    }

    private void AddDecorativeOccluders(
        Vector3 from,
        Vector3 partyPosition,
        Ray ray,
        float segmentLength,
        JcBuildingSilhouetteSettings tuning)
    {
        if (decorativeObjectRegistry == null)
            return;

        IReadOnlyList<DecorativeObjectPlacement> objects = decorativeObjectRegistry.DecorativeObjects;
        for (int i = 0; i < objects.Count; i++)
        {
            DecorativeObjectPlacement decorativeObject = objects[i];
            if (decorativeObject == null || !decorativeObject.isActiveAndEnabled)
                continue;

            if (tuning.preciseBuildingOcclusion && JcBuildingMeshOcclusion.IsBuilding(decorativeObject))
            {
                if (!buildingMeshOcclusion.IsOccluded(decorativeObject, from, partyPosition, tuning))
                    continue;
            }
            else
            {
                if (!decorativeObject.TryGetRenderBounds(out Bounds bounds, boundsPadding))
                    continue;
                if (!bounds.IntersectRay(ray, out float distance) || distance > segmentLength)
                    continue;
            }

            currentOccluders.Add(decorativeObject);
            clearSince.Remove(decorativeObject);
            decorativeObject.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
        }
    }

    private void AddFadeTargetOccluders(Ray ray, float segmentLength)
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
            if (!bounds.IntersectRay(ray, out float distance) || distance > segmentLength)
                continue;

            currentTargetOccluders.Add(target);
            targetClearSince.Remove(target);
            target.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
        }
    }

    private void RestoreNoLongerOccluding(bool moving, JcBuildingSilhouetteSettings tuning, float now)
    {
        foreach (DecorativeObjectPlacement decorativeObject in fadedObjects)
        {
            if (decorativeObject == null || currentOccluders.Contains(decorativeObject))
                continue;

            if (decorativeObject.isActiveAndEnabled && JcBuildingMeshOcclusion.IsBuilding(decorativeObject))
            {
                bool hold = moving && tuning.holdBuildingFadeWhileMoving;
                if (hold)
                    clearSince.Remove(decorativeObject);
                else if (tuning.buildingRestoreDelay > 0f)
                {
                    if (!clearSince.TryGetValue(decorativeObject, out float since))
                    {
                        since = now;
                        clearSince.Add(decorativeObject, since);
                    }

                    hold = now - since < tuning.buildingRestoreDelay;
                }

                if (hold)
                {
                    currentOccluders.Add(decorativeObject);
                    decorativeObject.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
                    continue;
                }
            }

            clearSince.Remove(decorativeObject);
            decorativeObject.RestoreOcclusionFade();
        }
    }

    private void RestoreTargetsNoLongerOccluding(bool moving, JcBuildingSilhouetteSettings tuning, float now)
    {
        foreach (PartyOcclusionFadeTarget target in fadedTargets)
        {
            if (target == null || currentTargetOccluders.Contains(target))
                continue;

            bool hold = moving && tuning.holdBuildingFadeWhileMoving;
            if (hold)
            {
                targetClearSince.Remove(target);
            }
            else if (tuning.buildingRestoreDelay > 0f)
            {
                if (!targetClearSince.TryGetValue(target, out float since))
                {
                    since = now;
                    targetClearSince.Add(target, since);
                }

                hold = now - since < tuning.buildingRestoreDelay;
            }

            if (hold)
            {
                currentTargetOccluders.Add(target);
                target.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
                continue;
            }

            targetClearSince.Remove(target);
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
        clearSince.Clear();
        fadedTargets.Clear();
        currentTargetOccluders.Clear();
        targetClearSince.Clear();
    }

    private void HandleDecorativeObjectUnregistered(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject != null)
            decorativeObject.RestoreOcclusionFade();

        fadedObjects.Remove(decorativeObject);
        clearSince.Remove(decorativeObject);
        currentOccluders.Remove(decorativeObject);
        if (decorativeObject != null)
            buildingMeshOcclusion.Remove(decorativeObject);
    }

    private void HandleFadeTargetUnregistered(PartyOcclusionFadeTarget target)
    {
        if (target != null)
            target.RestoreOcclusionFade();

        fadedTargets.Remove(target);
        targetClearSince.Remove(target);
        currentTargetOccluders.Remove(target);
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
