using UnityEngine;

public static class PersistentEnemyRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        if (PersistentEnemyRepository.Instance != null)
            return;

        var go = new GameObject("[PersistentEnemyRepository]");
        go.AddComponent<PersistentEnemyRepository>();
    }
}
