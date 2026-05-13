using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 신설 260512] UnityEngine.SceneManagement.SceneManager 래퍼 (정적).
/// 기존 SceneLoader(자체 이력/레지스트리 운영)를 폐기하고 단순 래퍼로 격하.
/// 향후 페이드·로딩 화면 등 부가 정책은 SceneFadeController 등 별도 컴포넌트가 담당.
/// </summary>
public static class GameSceneManager
{
    // [JC 260513] 엔딩 씬 상수. 트리거 조건은 기획 확정 시 외부에서 LoadVictoryEnding/LoadDefeatEnding을 호출하면 됨.
    public const string VictoryEndingScene = "Ending_Victory";
    public const string DefeatEndingScene = "Ending_Defeat";

    public static string ActiveSceneName => SceneManager.GetActiveScene().name;

    public static void LoadVictoryEnding() => LoadScene(VictoryEndingScene);
    public static void LoadDefeatEnding() => LoadScene(DefeatEndingScene);

    public static void FadeToVictoryEnding(float fadeOut = 1f, float fadeIn = 0f)
    {
        SceneFadeController fade = SceneFadeController.Instance;
        if (fade != null) fade.FadeToScene(VictoryEndingScene, fadeOut, fadeIn);
        else LoadScene(VictoryEndingScene);
    }

    public static void FadeToDefeatEnding(float fadeOut = 1f, float fadeIn = 0f)
    {
        SceneFadeController fade = SceneFadeController.Instance;
        if (fade != null) fade.FadeToScene(DefeatEndingScene, fadeOut, fadeIn);
        else LoadScene(DefeatEndingScene);
    }

    public static void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        SceneManager.LoadScene(sceneName, mode);
    }

    public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return null;
        return SceneManager.LoadSceneAsync(sceneName, mode);
    }

    public static bool SetActiveSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded) return false;
        return SceneManager.SetActiveScene(scene);
    }
}
