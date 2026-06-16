using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260615] 탐사씬 "건설 버튼"(BTN_Explor_IsCanBuild) 컨트롤러. (탐사 기획 ④-1)
/// - 이번 턴에 시설 해금/업그레이드를 아직 안 했으면 밝은 버튼(canBuild) → 클릭 시 본부(HQLobbyScene) 진입.
/// - 시설 해금/업그레이드 횟수를 소모(HQStateManager.UpgradedThisTurn)하면 어두운 버튼(cannotBuild)로 교체 → 진입 불가.
/// HQStateManager.OnStateChanged + 매턴 OnTurnAdvanced(UpgradedThisTurn 리셋)에 연동.
/// </summary>
[DisallowMultipleComponent]
public class IsCanBuildController : MonoBehaviour
{
    [Tooltip("밝은 상태 버튼(진입 가능). BTN_Explor_IsCanBuild")]
    [SerializeField] private GameObject canBuildButton;
    [Tooltip("어두운 상태 버튼(진입 불가). BTN_Explor_IsCanBuild_alt")]
    [SerializeField] private GameObject cannotBuildButton;
    [Tooltip("밝은 버튼의 클릭 타겟(보통 canBuildButton의 Button)")]
    [SerializeField] private Button canBuildClickTarget;

    private HQStateManager subscribed;

    private void OnEnable()
    {
        if (canBuildClickTarget != null) canBuildClickTarget.onClick.AddListener(OnClickBuild);
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        if (canBuildClickTarget != null) canBuildClickTarget.onClick.RemoveListener(OnClickBuild);
        if (subscribed != null) { subscribed.OnStateChanged -= Refresh; subscribed = null; }
    }

    private void Update()
    {
        if (subscribed == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.HQ != null && subscribed == null)
        {
            subscribed = gm.HQ;
            subscribed.OnStateChanged += Refresh;
            Refresh();
        }
    }

    private bool CanBuild()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.HQ != null && !gm.HQ.UpgradedThisTurn;
    }

    private void Refresh()
    {
        bool can = CanBuild();
        if (canBuildButton != null) canBuildButton.SetActive(can);
        if (cannotBuildButton != null) cannotBuildButton.SetActive(!can);
    }

    private void OnClickBuild()
    {
        if (!CanBuild()) return;
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadLobby();
    }
}
