using UnityEngine;

public static class DHFogProgressControllerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureController()
    {
        if (DHFogProgressController.Instance != null)
            return;

        var go = new GameObject("[DHFogProgressController]");
        go.AddComponent<DHFogProgressController>();
    }
}
