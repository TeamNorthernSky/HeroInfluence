using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260615] 탐사씬 "건설 버튼"(BTN_Explor_IsCanBuild) 컨트롤러. (탐사 기획 ④-1)
/// [KJ 260811] 턴당 1회 제한 폐지 — 항상 밝은 버튼(canBuild) 고정, 클릭 시 본부(HQLobbyScene) 진입.
/// 어두운 버튼(cannotBuild)은 상시 비활성. HQStateManager 구독은 감시할 상태가 없어져 제거했다.
/// CanBuild() 훅은 남겨둔다 — 추후 다른 진입 조건(예: 협회 인접)을 붙일 자리.
/// 직렬화 필드 3개는 프리팹 배선 보존을 위해 유지(어두운 버튼 오브젝트 삭제는 별도 UI 정리 작업).
/// </summary>
[DisallowMultipleComponent]
public class IsCanBuildController : MonoBehaviour
{
    [Tooltip("밝은 버튼의 클릭 타겟(보통 canBuildButton의 Button)")]
    [SerializeField] private Button canBuildClickTarget;

    private void OnEnable()
    {
        if (canBuildClickTarget != null) canBuildClickTarget.onClick.AddListener(OnClickBuild);
        //Refresh();
    }

    private void OnDisable()
    {
        if (canBuildClickTarget != null) canBuildClickTarget.onClick.RemoveListener(OnClickBuild);
    }

    // [KJ 260811] 턴 제한 폐지로 상시 true. 
    private bool CanBuild() => true;

    private void OnClickBuild()
    {
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadLobby();
    }
}
