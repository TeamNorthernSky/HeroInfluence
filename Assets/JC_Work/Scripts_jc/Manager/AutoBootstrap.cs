using UnityEngine;

public static class AutoBootstrap
{
    private const string PrefabResourceKey = "GameManager";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Execute()
    {
        if (GameManager.Instance != null) return;

        var prefab = Resources.Load<GameObject>(PrefabResourceKey);
        if (prefab != null)
        {
            var go = Object.Instantiate(prefab);
            go.name = "GameManager";
            Object.DontDestroyOnLoad(go);
        }
        else
        {
            Debug.LogError("[AutoBootstrap] GameManager 프리팹 로드 실패 (Resources/GameManager 누락)");
        }
    }
}
