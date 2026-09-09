using System.Collections.Generic;
using UnityEngine;

public class PartyOcclusionFadeController : MonoBehaviour
{
    private static readonly HashSet<PartyOcclusionFadeController> ActiveControllers = new HashSet<PartyOcclusionFadeController>();

    // 반투명 대상이 없는 프레임에는 추가 그림자 제외 렌더링을 생략한다.
    public static bool TryHasFadedObjects(UnityEngine.SceneManagement.Scene scene, out bool hasFaded)
    {
        bool found = false;
        hasFaded = false;
        foreach (var controller in ActiveControllers)
        {
            if (controller == null || !controller.isActiveAndEnabled || controller.gameObject.scene != scene) continue;
            found = true;
            hasFaded |= controller.fadedObjects.Count > 0;
        }
        return found;
    }
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
    private readonly Dictionary<DecorativeObjectPlacement, float> clearSince = new Dictionary<DecorativeObjectPlacement, float>();
    private float nextCheckTime;
    private readonly JcBuildingMeshOcclusion buildingMeshOcclusion = new JcBuildingMeshOcclusion();

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

        var tuning = JcBuildingSilhouetteController.TryGetSettings(cameraToUse, out var currentTuning)
            ? currentTuning : JcBuildingSilhouetteSettings.Default;

        IReadOnlyList<DecorativeObjectPlacement> objects = decorativeObjectRegistry.DecorativeObjects;
        for (int i = 0; i < objects.Count; i++)
        {
            DecorativeObjectPlacement decorativeObject = objects[i];
            if (decorativeObject == null || !decorativeObject.isActiveAndEnabled)
                continue;

            if (tuning.preciseBuildingOcclusion && JcBuildingMeshOcclusion.IsBuilding(decorativeObject))
            {
                if (!buildingMeshOcclusion.IsOccluded(decorativeObject, from, party.transform.position, tuning)) continue;
            }
            else
            {
                // 건물 외 장식 및 정밀 판정 해제 시에는 DH의 기존 판정을 그대로 유지한다.
                if (!decorativeObject.TryGetRenderBounds(out Bounds bounds, boundsPadding)) continue;
                if (!bounds.IntersectRay(ray, out float distance) || distance > segmentLength) continue;
            }

            currentOccluders.Add(decorativeObject);
            clearSince.Remove(decorativeObject);
            decorativeObject.SetOcclusionFadeAlpha(occludedAlpha, transparentOverrideMaterial);
        }

        RestoreNoLongerOccluding(party.IsMoving, tuning, Time.unscaledTime);

        fadedObjects.Clear();
        foreach (DecorativeObjectPlacement decorativeObject in currentOccluders)
            fadedObjects.Add(decorativeObject);
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
                if (hold) clearSince.Remove(decorativeObject);
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
        clearSince.Clear();
    }

    private void HandleDecorativeObjectUnregistered(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject != null)
            decorativeObject.RestoreOcclusionFade();

        fadedObjects.Remove(decorativeObject);
        clearSince.Remove(decorativeObject);
        currentOccluders.Remove(decorativeObject);
        if (decorativeObject != null) buildingMeshOcclusion.Remove(decorativeObject);
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
