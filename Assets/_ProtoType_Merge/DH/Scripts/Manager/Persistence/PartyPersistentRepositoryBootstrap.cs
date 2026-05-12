using UnityEngine;

public static class PartyPersistentRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        if (PartyPersistentRepository.Instance != null)
            return;

        var go = new GameObject("[PartyPersistentRepository]");
        go.AddComponent<PartyPersistentRepository>();
    }
}
