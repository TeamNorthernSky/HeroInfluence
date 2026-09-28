using System;
using System.Collections.Generic;
using UnityEngine;

public class PartyOcclusionFadeTargetRegistry : MonoBehaviour
{
    private const string RootName = "[DH_PartyOcclusionFadeTargetRegistry]";

    private readonly List<PartyOcclusionFadeTarget> targets = new List<PartyOcclusionFadeTarget>();

    public IReadOnlyList<PartyOcclusionFadeTarget> Targets => targets;

    public event Action<PartyOcclusionFadeTarget> TargetRegistered;
    public event Action<PartyOcclusionFadeTarget> TargetUnregistered;

    public static PartyOcclusionFadeTargetRegistry EnsureInstance()
    {
        PartyOcclusionFadeTargetRegistry existing = FindFirstObjectByType<PartyOcclusionFadeTargetRegistry>();
        if (existing != null)
            return existing;

        GameObject root = GameObject.Find(RootName);
        if (root == null)
            root = new GameObject(RootName);

        return root.AddComponent<PartyOcclusionFadeTargetRegistry>();
    }

    private void Awake()
    {
        RefreshSceneTargets();
    }

    public void Register(PartyOcclusionFadeTarget target)
    {
        if (target == null || targets.Contains(target))
            return;

        targets.Add(target);
        TargetRegistered?.Invoke(target);
    }

    public void Unregister(PartyOcclusionFadeTarget target)
    {
        if (target == null)
            return;

        if (!targets.Remove(target))
            return;

        TargetUnregistered?.Invoke(target);
    }

    [ContextMenu("Refresh Scene Occlusion Fade Targets")]
    public void RefreshSceneTargets()
    {
        targets.Clear();

        PartyOcclusionFadeTarget[] sceneTargets =
            FindObjectsByType<PartyOcclusionFadeTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneTargets.Length; i++)
        {
            PartyOcclusionFadeTarget target = sceneTargets[i];
            if (target == null)
                continue;

            Register(target);
        }
    }
}
