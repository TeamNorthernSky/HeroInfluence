using UnityEngine;
using UnityEngine.UI;

/// <summary>협회 진입과 턴 종료는 탐사 HUD의 공통 입력 조건을 사용합니다. 턴당 방문 횟수 제한은 없습니다.</summary>
[DisallowMultipleComponent]
public class IsCanBuildController : MonoBehaviour
{
    [Tooltip("밝은 버튼의 클릭 타겟(보통 canBuildButton의 Button)")]
    [SerializeField] private Button canBuildClickTarget;

    private ExplorationHUDController explorationHud;

    private void OnEnable()
    {
        if (canBuildClickTarget != null) canBuildClickTarget.onClick.AddListener(OnClickBuild);
        Refresh();
    }

    private void OnDisable()
    {
        if (canBuildClickTarget != null) canBuildClickTarget.onClick.RemoveListener(OnClickBuild);
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        if (canBuildClickTarget != null) canBuildClickTarget.interactable = CanBuild();
    }

    private bool CanBuild()
    {
        if (explorationHud == null) explorationHud = FindFirstObjectByType<ExplorationHUDController>();
        return explorationHud != null && explorationHud.CanUseExplorationActions();
    }

    private void OnClickBuild()
    {
        if (!CanBuild()) return;
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadLobby();
    }
}
