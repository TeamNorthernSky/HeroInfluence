using UnityEngine;

public static class EnemyGroupPersistentRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        if (EnemyGroupPersistentRepository.Instance != null)
            return;

        var go = new GameObject("[EnemyGroupPersistentRepository]");
        go.AddComponent<EnemyGroupPersistentRepository>();
    }
}
