using UnityEngine;

public static class PersistentUnitRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        if (PersistentUnitRepository.Instance != null)
            return;

        var go = new GameObject("[PersistentUnitRepository]");
        go.AddComponent<PersistentUnitRepository>();
    }
}
