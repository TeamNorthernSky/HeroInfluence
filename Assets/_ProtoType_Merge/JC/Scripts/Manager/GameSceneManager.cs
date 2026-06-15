using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 신설 260512 → 컴포넌트 격상 260514]
/// GameManager 자식 영속 컴포넌트. 씬 카탈로그(인스펙터 토글) + SceneManager 호출 래퍼 + 엔딩 API를 통합.
///
/// 책임:
/// - 씬 이름 카탈로그: 모든 주요 씬을 인스펙터 필드로 노출 (코드 하드코드 폐기)
/// - 탐사씬 토글: Default / Legacy 두 슬롯 + bool 토글로 즉시 전환
/// - LoadScene/LoadSceneAsync/SetActiveSceneByName: UnityEngine.SceneManagement.SceneManager 래퍼
/// - 엔딩 헬퍼: LoadVictoryEnding/LoadDefeatEnding (기획 확정 시 트리거 연결)
///
/// 호출 패턴: <c>GameSceneManager.Instance.LoadScene(...)</c> 또는 <c>GameSceneManager.Instance.ExplorationScene</c>.
/// </summary>
[DisallowMultipleComponent]
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("Scene Catalog")]
    [SerializeField] private string titleScene = "TitleScene";
    [SerializeField] private string lobbyScene = "HQLobbyScene";
    [SerializeField] private string gameLoadScene = "GameLoadScene";
    [SerializeField] private string victoryEndingScene = "Ending_Victory";
    [SerializeField] private string defeatEndingScene = "Ending_Defeat";

    [Header("Exploration Scene Toggle")]
    [Tooltip("ON 시 ExplorationSceneLegacy 사용(옛 DHScene). OFF(기본) 시 ExplorationSceneDefault(=DHScene_2).")]
    [SerializeField] private bool useLegacyExploration = false;
    [SerializeField] private string explorationSceneDefault = "DHScene_2";
    [SerializeField] private string explorationSceneLegacy = "DHScene";

    public string TitleScene => titleScene;
    public string LobbyScene => lobbyScene;
    public string GameLoadScene => gameLoadScene;
    public string VictoryEndingScene => victoryEndingScene;
    public string DefeatEndingScene => defeatEndingScene;
    public string ExplorationScene => useLegacyExploration ? explorationSceneLegacy : explorationSceneDefault;
    public bool UseLegacyExploration => useLegacyExploration;

    public string ActiveSceneName => SceneManager.GetActiveScene().name;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;
        SceneManager.LoadScene(sceneName, mode);
    }

    public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return null;
        return SceneManager.LoadSceneAsync(sceneName, mode);
    }

    public bool SetActiveSceneByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;
        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
            return false;
        return SceneManager.SetActiveScene(scene);
    }

    public void LoadExploration() => LoadScene(ExplorationScene);
    public void LoadLobby() => LoadScene(lobbyScene);
    public void LoadTitle() => LoadScene(titleScene);
    public void LoadGameLoad() => LoadScene(gameLoadScene);
    public void LoadVictoryEnding() => LoadScene(victoryEndingScene);
    public void LoadDefeatEnding() => LoadScene(defeatEndingScene);

    public void FadeToVictoryEnding(float fadeOut = 1f, float fadeIn = 0f)
    {
        SceneFadeController fade = SceneFadeController.Instance;
        if (fade != null) fade.FadeToScene(victoryEndingScene, fadeOut, fadeIn);
        else LoadScene(victoryEndingScene);
    }

    public void FadeToDefeatEnding(float fadeOut = 1f, float fadeIn = 0f)
    {
        SceneFadeController fade = SceneFadeController.Instance;
        if (fade != null) fade.FadeToScene(defeatEndingScene, fadeOut, fadeIn);
        else LoadScene(defeatEndingScene);
    }
}
