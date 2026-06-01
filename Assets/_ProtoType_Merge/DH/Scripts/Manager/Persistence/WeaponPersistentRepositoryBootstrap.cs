using UnityEngine;

public static class WeaponPersistentRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        if (WeaponPersistentRepository.Instance != null)
            return;

        var go = new GameObject("[WeaponPersistentRepository]");
        go.AddComponent<WeaponPersistentRepository>();
    }
}
