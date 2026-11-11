using System;
using System.Collections.Generic;
using UnityEngine;

public class DecorativeObjectRegistry : MonoBehaviour
{
    private readonly List<DecorativeObjectPlacement> decorativeObjects = new List<DecorativeObjectPlacement>();

    public IReadOnlyList<DecorativeObjectPlacement> DecorativeObjects => decorativeObjects;

    public event Action<DecorativeObjectPlacement> DecorativeObjectRegistered;
    public event Action<DecorativeObjectPlacement> DecorativeObjectUnregistered;
    // JC | 2026-11-10 | 기존 대상별 알림을 재사용하면서 빈 재구축·등록부 교체도 수집측에 알립니다.
    public event Action<DecorativeObjectRegistry> Rebuilt;
    public static event Action<DecorativeObjectRegistry> LifecycleChanged;

    private void Awake()
    {
        RefreshSceneDecorativeObjects();
    }

    // JC | 2026-11-10 | 매초 씬 탐색 대신 등록부 수명이 바뀔 때 구독을 재연결합니다.
    private void OnEnable() => LifecycleChanged?.Invoke(this);
    private void OnDisable() => LifecycleChanged?.Invoke(this);
    private void OnDestroy() => LifecycleChanged?.Invoke(this);

    public void Register(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject == null || decorativeObjects.Contains(decorativeObject))
            return;

        decorativeObjects.Add(decorativeObject);
        DecorativeObjectRegistered?.Invoke(decorativeObject);
    }

    public void Unregister(DecorativeObjectPlacement decorativeObject)
    {
        if (decorativeObject == null)
            return;

        if (!decorativeObjects.Remove(decorativeObject))
            return;

        DecorativeObjectUnregistered?.Invoke(decorativeObject);
    }

    [ContextMenu("Refresh Scene Decorative Objects")]
    public void RefreshSceneDecorativeObjects()
    {
        decorativeObjects.Clear();

        DecorativeObjectPlacement[] sceneObjects = FindObjectsByType<DecorativeObjectPlacement>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            DecorativeObjectPlacement decorativeObject = sceneObjects[i];
            if (decorativeObject == null)
                continue;

            Register(decorativeObject);
        }
        // JC | 2026-11-10 | 등록/해제 이벤트의 기존 의미와 횟수를 바꾸지 않는 별도 완료 알림입니다.
        Rebuilt?.Invoke(this);
    }
}
