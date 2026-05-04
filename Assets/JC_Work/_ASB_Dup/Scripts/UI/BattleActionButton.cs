using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 액션 버튼 프리팹에 부착. 인스펙터에서 PendingActionType을 지정하면 클릭 시 BattleInputRouter로 전달.
/// 라우터 참조가 비어 있으면 씬에서 FindFirstObjectByType으로 탐색(프로토타입 편의).
/// </summary>
[RequireComponent(typeof(Button))]
public class BattleActionButton : MonoBehaviour
{
    [SerializeField] private PendingActionType actionType = PendingActionType.BasicAttack;
    [SerializeField] private Button button;
    [SerializeField] private BattleInputRouter router;

    public PendingActionType ActionType => actionType;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        BattleInputRouter target = router != null ? router : FindFirstObjectByType<BattleInputRouter>();
        if (target == null)
        {
            Debug.LogWarning("[BattleActionButton] BattleInputRouter를 찾을 수 없습니다.");
            return;
        }
        target.PressAction(actionType);
    }
}
