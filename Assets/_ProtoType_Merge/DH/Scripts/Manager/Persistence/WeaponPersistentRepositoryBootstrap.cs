using UnityEngine;

public static class WeaponPersistentRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        EnsureInstance();
    }

    public static WeaponPersistentRepository EnsureInstance()
    {
        if (WeaponPersistentRepository.Instance != null)
            return WeaponPersistentRepository.Instance;

        var go = new GameObject("[WeaponPersistentRepository]");
        return go.AddComponent<WeaponPersistentRepository>();
    }
}
