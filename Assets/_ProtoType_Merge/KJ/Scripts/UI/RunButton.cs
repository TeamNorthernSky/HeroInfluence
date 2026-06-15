using UnityEngine;
using UnityEngine.UI;

public class RunButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private BattleFlowManager battleFlowManager;

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

        if (battleFlowManager == null)
        {
            battleFlowManager = FindFirstObjectByType<BattleFlowManager>();
        }
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnRunButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnRunButtonClicked);
        }
    }

    private void OnRunButtonClicked()
    {
        if (battleFlowManager == null)
        {
            Debug.LogWarning("[RunButton] BattleFlowManager를 찾을 수 없습니다.");
            return;
        }

        battleFlowManager.RequestFlee();
    }
}
