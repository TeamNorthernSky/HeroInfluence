using UnityEngine;

/// <summary>TmpBattleScene을 단독 실행해도 사용할 수 있는 진입·복귀 페이드.</summary>
public static class TmpBattleSceneFade
{
    public const string SceneName = "TmpBattleScene";
    private const float DefaultDuration = 0.35f;

    public static void Enter()
    {
        EnsureController().FadeIn(1f, holdDuration: 1f);
    }

    public static void Leave(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        // 등록되지 않은 씬 때문에 검정 화면에 갇히지 않도록 먼저 검사한다.
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[TmpBattleSceneFade] 전환할 씬이 빌드 설정에 없습니다: {sceneName}");
            return;
        }
        EnsureController().FadeToScene(sceneName,
            fadeOutDuration > 0f ? fadeOutDuration : DefaultDuration,
            fadeInDuration > 0f ? fadeInDuration : DefaultDuration);
    }

    private static SceneFadeController EnsureController()
    {
        if (SceneFadeController.Instance != null) return SceneFadeController.Instance;
        var root = new GameObject("TmpBattleSceneFade");
        Object.DontDestroyOnLoad(root);
        return root.AddComponent<SceneFadeController>();
    }
}
