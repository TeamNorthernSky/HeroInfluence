using UnityEngine;

public static class MapProgressRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        if (MapProgressRepository.Instance != null)
            return;

        var go = new GameObject("[MapProgressRepository]");
        go.AddComponent<MapProgressRepository>();
    }
}
