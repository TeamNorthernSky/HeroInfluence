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
            button.onClick.AddListener(OnRunButtonClicked);

        if (battleFlowManager != null)
        {
            battleFlowManager.OnTurnStarted += OnTurnStarted;
            battleFlowManager.OnBattleEnded += OnBattleEnded;
        }

        //button.interactable = false;
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnRunButtonClicked);

        if (battleFlowManager != null)
        {
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
            battleFlowManager.OnBattleEnded -= OnBattleEnded;
        }
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (button != null) button.interactable = unit != null && unit.IsPlayer;
    }

    private void OnBattleEnded(BattleResult result)
    {
        if (button != null) button.interactable = false;
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
