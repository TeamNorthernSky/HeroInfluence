using UnityEngine;
using UnityEngine.UI;

public class RunButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private BattleFlowManager battleFlowManager;
    [Tooltip("기존 공용 패널 스타일을 사용한 도주 확인 모달입니다. 확인에서만 도주하며 취소하면 전투를 계속합니다. 씬에 저장됩니다.")]
    [SerializeField] private GameObject confirmationPopup;
    private System.IDisposable confirmationLock;
    private bool awaitingConfirmation;

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
        CancelRun();
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
        CancelRun();
        if (button != null) button.interactable = false;
    }

    private void OnRunButtonClicked()
    {
        if (battleFlowManager == null)
        {
            Debug.LogWarning("[RunButton] BattleFlowManager를 찾을 수 없습니다.");
            return;
        }

        if (awaitingConfirmation || confirmationPopup == null || !CanRequestRun()) return;
        awaitingConfirmation = true;
        confirmationLock = battleFlowManager.AcquireFlowLock(this, "도주 확인");
        confirmationPopup.SetActive(true);
    }

    private bool CanRequestRun() => battleFlowManager != null && !battleFlowManager.IsEndingBattle &&
        !battleFlowManager.IsFlowBlocked && !battleFlowManager.IsActionInProgress &&
        !battleFlowManager.IsTurnPresentationPending && battleFlowManager.CurrentUnit != null &&
        battleFlowManager.CurrentUnit.IsPlayer &&
        battleFlowManager.IsPlayerActionAllowed(battleFlowManager.CurrentUnit, PendingActionType.Flee, null);

    public void ConfirmRun()
    {
        if (!awaitingConfirmation || confirmationPopup == null || !confirmationPopup.activeInHierarchy ||
            ModalManager.Top != confirmationPopup) return;
        CancelRun();
        if (CanRequestRun()) battleFlowManager.RequestFlee();
    }

    public void CancelRun()
    {
        awaitingConfirmation = false;
        if (confirmationPopup != null) confirmationPopup.SetActive(false);
        confirmationLock?.Dispose();
        confirmationLock = null;
    }

    private void Update()
    {
        // ESC 등 공용 모달 닫기 경로에서도 전투 잠금을 남기지 않는다.
        if (awaitingConfirmation && (confirmationPopup == null || !confirmationPopup.activeInHierarchy)) CancelRun();
    }
}
