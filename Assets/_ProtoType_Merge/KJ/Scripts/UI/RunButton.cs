using UnityEngine;
using UnityEngine.UI;

public class RunButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private BattleFlowManager battleFlowManager;
    [Tooltip("기존 공용 패널 스타일을 사용한 도주 확인 모달입니다. 확인에서만 도주하며 취소하면 전투를 계속합니다. 씬에 저장됩니다.")]
    [SerializeField] private GameObject confirmationPopup;
    [SerializeField, Tooltip("튜토리얼 도주 확인 뒤 표시할 제한 안내 모달입니다. 화면 클릭 또는 확인으로 닫으며 전투 잠금은 닫힌 뒤 해제합니다.")]
    private GameObject tutorialRestrictionPopup;
    private bool showingRestriction, dismissRestrictionPending;
    private int restrictionOpenedFrame;
    private bool IsTutorialBattle => gameObject.scene.name == "TutorialBattleScene" ||
        (CombatContext.Instance != null && CombatContext.Instance.IsTutorial);
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
        (IsTutorialBattle || battleFlowManager.IsPlayerActionAllowed(battleFlowManager.CurrentUnit, PendingActionType.Flee, null));

    public void ConfirmRun()
    {
        if (!awaitingConfirmation || confirmationPopup == null || !confirmationPopup.activeInHierarchy ||
            ModalManager.Top != confirmationPopup) return;
        if (IsTutorialBattle)
        {
            if (tutorialRestrictionPopup == null) { Debug.LogError("튜토리얼 도주 제한 모달 연결이 없습니다.", this); CancelRun(); return; }
            awaitingConfirmation = false;
            showingRestriction = true;
            restrictionOpenedFrame = Time.frameCount;
            confirmationPopup.SetActive(false);
            tutorialRestrictionPopup.SetActive(true);
            return;
        }
        CancelRun();
        if (CanRequestRun()) battleFlowManager.RequestFlee();
    }

    public void CancelRun()
    {
        awaitingConfirmation = false;
        showingRestriction = dismissRestrictionPending = false;
        if (tutorialRestrictionPopup != null) tutorialRestrictionPopup.SetActive(false);
        if (confirmationPopup != null) confirmationPopup.SetActive(false);
        confirmationLock?.Dispose();
        confirmationLock = null;
    }

    public void DismissTutorialRestriction()
    {
        if (showingRestriction && Time.frameCount > restrictionOpenedFrame) dismissRestrictionPending = true;
    }
    private void LateUpdate()
    {
        // 확인 클릭을 전투 대상 선택으로 넘기지 않고 버튼에서 손을 뗀 뒤 잠금을 해제한다.
        if (dismissRestrictionPending && !Input.GetMouseButton(0)) CancelRun();
    }
    private void Update()
    {
        if (showingRestriction)
        {
            if (tutorialRestrictionPopup == null || !tutorialRestrictionPopup.activeInHierarchy) { CancelRun(); return; }
            if (ModalManager.Top == tutorialRestrictionPopup && Input.GetMouseButtonDown(0)) DismissTutorialRestriction();
        }
        // ESC 등 공용 모달 닫기 경로에서도 전투 잠금을 남기지 않는다.
        if (awaitingConfirmation && (confirmationPopup == null || !confirmationPopup.activeInHierarchy)) CancelRun();
    }
}
