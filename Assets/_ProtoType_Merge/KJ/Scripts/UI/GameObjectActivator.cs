using System.Collections.Generic;
using UnityEngine;

/// <summary>하위 트리를 먼저 조사한 뒤 예외를 제외한 비활성 오브젝트를 켭니다.</summary>
[DisallowMultipleComponent]
public class GameObjectActivator : MonoBehaviour
{
    [Tooltip("이 오브젝트와 모든 자식은 활성화 대상에서 제외하고 현재 상태를 유지합니다.")]
    public GameObject[] excludedObjects = System.Array.Empty<GameObject>();

    private void Start()
    {
        var excluded = new HashSet<GameObject>(excludedObjects ?? System.Array.Empty<GameObject>());
        var pending = new List<GameObject>();
        CollectInactiveObjects(transform, excluded, pending);

        foreach (GameObject target in pending)
        {
            if (target != null && !target.activeSelf)
                target.SetActive(true);
        }
    }

    private static void CollectInactiveObjects(Transform current, HashSet<GameObject> excluded,
        List<GameObject> pending)
    {
        if (excluded.Contains(current.gameObject)) return;

        if (!current.gameObject.activeSelf)
            pending.Add(current.gameObject);

        foreach (Transform child in current)
            CollectInactiveObjects(child, excluded, pending);
    }
}
