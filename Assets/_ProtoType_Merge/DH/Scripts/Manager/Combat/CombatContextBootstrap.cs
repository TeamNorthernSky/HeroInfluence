using UnityEngine;

public static class CombatContextBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCombatContextExists()
    {
        CombatContext.EnsureInstance();
    }
}
