using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HQUpgradeFlowController : MonoBehaviour
{
    [Header("Modal_HQ_Progress 결합")]
    [SerializeField] private GameObject modalProgressRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI upgradeInfoText;
    [SerializeField] private TextMeshProUGUI hqLevelText;
    [SerializeField] private TextMeshProUGUI stateInfoText;
    [SerializeField] private TextMeshProUGUI costMoneyText;
    [SerializeField] private TextMeshProUGUI costChipText;
    [SerializeField] private TextMeshProUGUI costCrystalText;
    [SerializeField] private TextMeshProUGUI costSupplyText;
    [SerializeField] private Button btnConfirm;
    [SerializeField] private GameObject disabledOverlay;
    [SerializeField] private Button btnCancel;
    [SerializeField] private Button btnClose;

    [Header("Modal_UpgradeResult 결합")]
    [SerializeField] private GameObject modalResultRoot;
    [SerializeField] private TextMeshProUGUI resultTitleText;
    [SerializeField] private TextMeshProUGUI resultLevelText;
    [SerializeField] private TextMeshProUGUI resultBodyText;
    [SerializeField] private Button btnResultOk;

    [Header("부서별 모달 콘텐츠 (추후 CSV 가능)")]
    [SerializeField] private List<DepartmentModalContent> departmentContents = new List<DepartmentModalContent>();

    [Serializable]
    public class DepartmentModalContent
    {
        public HQDepartment department;
        public string progressTitle;
        [Tooltip("해금 이후(level>=1) 진입 시 사용할 Title. 빈값이면 progressTitle 폴백")]
        public string progressTitleUpgrade;
        public bool showHqLevel = true;
        [TextArea] public string upgradeInfoStaticText; // 빈값이면 부서별 동적 텍스트 (현재: 본부만)
        [TextArea]
        [Tooltip("해금 이후(level>=1) 진입 시 사용. 빈값이면 upgradeInfoStaticText 폴백")]
        public string upgradeInfoStaticTextUpgrade;
        public string resultTitle;
        public bool showResultLevel = true;
        [TextArea] public string resultBodyStaticText;  // 빈값이면 부서별 동적 텍스트 (현재: 본부만)
        [TextArea]
        [Tooltip("업그레이드(before>=1) 시 사용. 빈값이면 resultBodyStaticText 폴백")]
        public string resultBodyStaticTextUpgrade;
        [Tooltip("'현재 본부의 업그레이드 상태가 최고 단계입니다.' 등 안내. 본부=true, 활성화 흐름=false")]
        public bool useMaxLevelStateInfo = true;
    }

    private static readonly Color costAffordColor = Color.white;
    private static readonly Color costShortColor = new Color(0.85f, 0.15f, 0.15f, 1f);

    [SerializeField] private HQUpgradeLayoutView layoutView;
    private HQDepartment currentDept;
    private DepartmentModalContent currentContent;
    private IReadOnlyDictionary<ResourceType, int> currentCost;
    private EconomyManager subscribedEco;
    private HQStateManager subscribedHQ;

    private void Awake()
    {
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnCancel != null) btnCancel.onClick.AddListener(CloseProgress);
        if (btnClose != null) btnClose.onClick.AddListener(CloseProgress);
        if (btnResultOk != null) btnResultOk.onClick.AddListener(CloseResult);
    }

    private void OnEnable() { TrySubscribe(); }
    private void OnDisable() { Unsubscribe(); }
    private void Update()
    {
        if (subscribedEco == null || subscribedHQ == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subscribedEco == null && gm.Economy != null)
        {
            subscribedEco = gm.Economy;
            subscribedEco.OnResourceChanged += HandleResourceChanged;
        }
        if (subscribedHQ == null && gm.HQ != null)
        {
            subscribedHQ = gm.HQ;
            subscribedHQ.OnStateChanged += HandleHQChanged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedEco != null)
        {
            subscribedEco.OnResourceChanged -= HandleResourceChanged;
            subscribedEco = null;
        }
        if (subscribedHQ != null)
        {
            subscribedHQ.OnStateChanged -= HandleHQChanged;
            subscribedHQ = null;
        }
    }

    // [KJ 260811] 턴 제한 폐지 → Progress를 닫지 않고 연타하므로, 상태/자원이 바뀌면 표시(비용·레벨·문구)까지
    //  다시 계산한다. 업그레이드 직후 TryUpgrade가 OnStateChanged를 발화 → 여기서 다음 단계로 자동 갱신된다.
    private void HandleResourceChanged(ResourceType _, int __)
    {
        if (modalProgressRoot == null || !modalProgressRoot.activeSelf) return;
        RefreshProgress();
        ApplyProgressButtonState();
    }

    private void HandleHQChanged()
    {
        if (modalProgressRoot == null || !modalProgressRoot.activeSelf) return;
        RefreshProgress();
        ApplyProgressButtonState();
    }

    private DepartmentModalContent FindContent(HQDepartment d)
    {
        for (int i = 0; i < departmentContents.Count; i++)
        {
            if (departmentContents[i] != null && departmentContents[i].department == d) return departmentContents[i];
        }
        return null;
    }

    public DepartmentModalContent GetContent(HQDepartment d) => FindContent(d);

    public void ShowProgress(HQDepartment dept)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;

        currentDept = dept;
        currentContent = FindContent(dept);

        // Modal_HQ는 닫지 않음 — Progress가 같은 캔버스 형제로 위에 떠 오버레이.
        // 닫기 후 사용자가 즉시 다른 부서를 선택할 수 있음.
        if (modalProgressRoot != null) modalProgressRoot.SetActive(true);

        RefreshProgress();
        ApplyProgressButtonState();
    }

    /// <summary>[KJ 260811] 표시 갱신 — 현재 레벨을 다시 읽어 비용까지 재계산한다.
    /// 턴당 1회 제한 폐지로 Progress를 닫지 않고 연속 업그레이드하므로, OnStateChanged/OnResourceChanged
    /// 때마다 이 메서드로 "다음 단계" 값으로 갱신해야 한다. 재계산하지 않으면 이전 단계 비용이 남아
    /// 잘못된 가격으로 차감된다. currentDept/currentContent는 ShowProgress가 세팅한 값을 재사용한다
    /// (모달이 열려 있는 동안 대상 부서는 바뀌지 않는다).</summary>
    private void RefreshProgress()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;

        int currentLevel = gm.HQ.GetLevel(currentDept);
        currentCost = gm.HQ.GetUpgradeCost(currentDept, currentLevel);
        if (layoutView != null)
        {
            layoutView.RefreshDisplay(currentDept, currentLevel, gm.HQ, gm.Economy, currentCost);
            return;
        }

        // Title — 해금 전(level=0)은 progressTitle, 해금 후(level>=1)는 progressTitleUpgrade (빈값이면 progressTitle 폴백)
        if (titleText != null)
        {
            string title = currentContent?.progressTitle ?? string.Empty;
            if (currentContent != null && currentLevel >= 1 && !string.IsNullOrEmpty(currentContent.progressTitleUpgrade))
                title = currentContent.progressTitleUpgrade;
            titleText.text = title;
        }

        // HQLevel (showHqLevel=false면 공란)
        bool showHqLevel = currentContent != null && currentContent.showHqLevel;
        if (hqLevelText != null)
        {
            hqLevelText.gameObject.SetActive(showHqLevel);
            if (showHqLevel) hqLevelText.text = BuildHQLevelText(gm.HQ, currentDept, currentLevel);
        }

        // UpgradeInfo — 해금 후(level>=1)는 upgradeInfoStaticTextUpgrade 우선, 폴백은 기존 흐름
        if (upgradeInfoText != null)
        {
            bool maxed = currentLevel >= gm.HQ.GetMaxLevel(currentDept);
            bool unlocked = currentLevel >= 1;
            string text;
            if (maxed) text = string.Empty;
            else if (unlocked && currentContent != null && !string.IsNullOrEmpty(currentContent.upgradeInfoStaticTextUpgrade))
                text = currentContent.upgradeInfoStaticTextUpgrade;
            else if (currentContent != null && !string.IsNullOrEmpty(currentContent.upgradeInfoStaticText))
                text = currentContent.upgradeInfoStaticText;
            else
                text = BuildDynamicUpgradeInfoText(gm.HQ, currentDept, currentLevel);
            upgradeInfoText.text = text;
            upgradeInfoText.gameObject.SetActive(!maxed);
        }

        // Costs
        if (costMoneyText != null) costMoneyText.text = currentCost[ResourceType.Money].ToString("N0");
        if (costChipText != null) costChipText.text = currentCost[ResourceType.Chip].ToString("N0");
        if (costCrystalText != null) costCrystalText.text = currentCost[ResourceType.Crystal].ToString("N0");
        if (costSupplyText != null) costSupplyText.text = currentCost[ResourceType.Supply].ToString("N0");

    }

    private void ApplyProgressButtonState()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        //bool turnUsed = gm.HQ != null && gm.HQ.UpgradedThisTurn;
        bool maxed = gm.HQ != null && gm.HQ.GetLevel(currentDept) >= gm.HQ.GetMaxLevel(currentDept);
        bool canAfford = CheckAfford(gm.Economy);
        bool prereqMet = gm.HQ == null || gm.HQ.ArePrerequisitesMet(currentDept);

        bool enabled = /*!turnUsed &&*/ canAfford && !maxed && prereqMet;

        if (btnConfirm != null) btnConfirm.interactable = enabled;
        if (disabledOverlay != null) disabledOverlay.SetActive(!enabled);

        ApplyCostColors(gm.Economy);

        if (stateInfoText != null)
        {
            string msg = null;
            // 최고 단계는 턴 사용 여부와 무관하게 항상 최우선 안내
            if (maxed && currentContent != null && currentContent.useMaxLevelStateInfo)
                msg = "업그레이드 상태가 최고 단계입니다.";
            else if (!prereqMet)
                msg = gm.HQ != null ? gm.HQ.GetUnmetReasonText(currentDept) : "선행 조건이 필요합니다.";
            //else if (turnUsed)
            //    msg = "이번 턴에 이미 건설, 또는 업그레이드를 진행했습니다.";
            else if (!canAfford)
                msg = "자원이 부족합니다.";
            stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }

    private bool CheckAfford(EconomyManager eco)
    {
        if (eco == null || currentCost == null) return false;
        foreach (var kv in currentCost)
        {
            if (!eco.Has(kv.Key, kv.Value)) return false;
        }
        return true;
    }

    private void ApplyCostColors(EconomyManager eco)
    {
        if (currentCost == null || layoutView != null) return;
        SetCostColor(costMoneyText, eco, ResourceType.Money);
        SetCostColor(costChipText, eco, ResourceType.Chip);
        SetCostColor(costCrystalText, eco, ResourceType.Crystal);
        SetCostColor(costSupplyText, eco, ResourceType.Supply);
    }

    private void SetCostColor(TextMeshProUGUI tmp, EconomyManager eco, ResourceType type)
    {
        if (tmp == null || currentCost == null) return;
        if (!currentCost.TryGetValue(type, out int cost)) return;
        bool afford = eco != null && eco.Has(type, cost);
        tmp.color = afford ? costAffordColor : costShortColor;
    }

    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null || gm.Economy == null) return;
        if (!CheckAfford(gm.Economy)) return;
        // [JC 260618] CanUpgrade 사전 가드 — 차감 전에 업그레이드 가능 여부 확정(턴 사용/선행조건/최고단계 레이스 차단).
        if (!gm.HQ.CanUpgrade(currentDept))
        {
            Debug.LogError("[HQUpgrade] CanUpgrade=false — 차감 중단");
            return;
        }

        // [KJ 260811] 차감 중 Spend가 OnResourceChanged를 동기 발화 → HandleResourceChanged → RefreshProgress가
        //  currentCost를 새 딕셔너리로 교체한다. 순회 중 컬렉션이 바뀌면 InvalidOperationException이 나므로
        //  차감·롤백은 이 스냅샷만 사용한다. 필드 currentCost는 건드리지 않는다.
        var costSnapshot = new List<KeyValuePair<ResourceType, int>>(currentCost);

        // [JC 260618] 차감 성공분을 기억해, 이후 단계 실패 시 전체 롤백(자원 유실 방지). Training/Lab 롤백 패턴과 정합.
        var spent = new List<KeyValuePair<ResourceType, int>>();
        foreach (var kv in costSnapshot)
        {
            if (kv.Value <= 0) continue;
            if (!gm.Economy.Spend(kv.Key, kv.Value))
            {
                Debug.LogError($"[HQUpgrade] Spend 실패: {kv.Key} {kv.Value} — 롤백");
                foreach (var s in spent) gm.Economy.Add(s.Key, s.Value);
                return;
            }
            spent.Add(kv);
        }

        if (!gm.HQ.TryUpgrade(currentDept, out int beforeLevel, out int afterLevel))
        {
            Debug.LogError("[HQUpgrade] TryUpgrade 실패 — 차감 롤백");
            foreach (var s in spent) gm.Economy.Add(s.Key, s.Value);
            return;
        }

        // 턴당 업그레이드 1회 제한 해제로 인해 패널 계속 띄워둠
        //CloseProgress();
        ShowResult(beforeLevel, afterLevel);
    }

    private void CloseProgress()
    {
        if (modalProgressRoot != null) modalProgressRoot.SetActive(false);
    }

    private void ShowResult(int before, int after)
    {
        // Title
        if (resultTitleText != null)
            resultTitleText.text = currentContent?.resultTitle ?? string.Empty;

        // Level
        bool showLevel = currentContent != null && currentContent.showResultLevel;
        if (resultLevelText != null)
        {
            resultLevelText.gameObject.SetActive(showLevel);
            if (showLevel) resultLevelText.text = $"{before}단계 → {after}단계";
        }

        // Body — before==0(해금) vs before>=1(업그레이드) 분기. 폴백: upgrade→static→dynamic
        if (resultBodyText != null)
        {
            string body;
            bool isUpgrade = before >= 1;
            if (isUpgrade && currentContent != null && !string.IsNullOrEmpty(currentContent.resultBodyStaticTextUpgrade))
                body = currentContent.resultBodyStaticTextUpgrade;
            else if (currentContent != null && !string.IsNullOrEmpty(currentContent.resultBodyStaticText))
                body = currentContent.resultBodyStaticText;
            else
                body = BuildDynamicResultBody(after);
            resultBodyText.text = body;
        }

        if (layoutView != null)
        {
            layoutView.ShowResult(currentDept, before, after, GameManager.Instance.HQ);
            if (resultLevelText != null) resultLevelText.gameObject.SetActive(false);
        }
        if (modalResultRoot != null) modalResultRoot.SetActive(true);
    }

    private string BuildDynamicResultBody(int afterLevel)
    {
        var gm = GameManager.Instance;
        int incomeAfter = gm != null && gm.HQ != null ? gm.HQ.GetTurnIncomeAt(currentDept, afterLevel) : 0;
        return $"다음 턴부터 자금 획득량이 {incomeAfter:N0}으로 증가합니다.";
    }

    private static string BuildDynamicUpgradeInfoText(HQStateManager hq, HQDepartment dept, int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        int incomeBefore = hq.GetTurnIncomeAt(dept, currentLevel);
        int incomeAfter = hq.GetTurnIncomeAt(dept, nextLevel);
        return $"본부를 {currentLevel}단계에서 {nextLevel}단계로 업그레이드 합니다. 턴 시작시 획득 자금이 {incomeBefore:N0}에서 {incomeAfter:N0}으로 증가합니다.";
    }

    private static string BuildHQLevelText(HQStateManager hq, HQDepartment dept, int currentLevel)
    {
        bool isMax = currentLevel >= hq.GetMaxLevel(dept);
        return isMax
            ? $"{currentLevel}단계(최고 단계)"
            : $"{currentLevel}단계 → {currentLevel + 1}단계";
    }

    private void CloseResult()
    {
        if (modalResultRoot != null) modalResultRoot.SetActive(false);
    }
}
