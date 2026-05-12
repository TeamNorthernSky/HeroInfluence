using UnityEngine;

public static class CombatContextBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCombatContextExists()
    {
        if (CombatContext.Instance != null)
            return;

        GameObject root = new GameObject("[CombatContext]");
        root.AddComponent<CombatContext>();
    }
}
