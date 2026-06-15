using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 교환소 모달 컨트롤러. (기획서 협회(교환소) V3.0)
/// 흐름: ① 지불 재화 선택(화살표 순환) → ② 지불량 선택(▲▼ 묶음 단위) →
///       ③ 수령 재화 행 체크박스로 1개 선택 → ④ 하단 "진행" 버튼으로 교환 / "취소"로 초기화.
/// 무효 수령 재화(예: 타자원→자금)는 숨기지 않고 반투명 오버레이로 비활성 표시.
/// 환산 규칙은 <see cref="ExchangeData"/>. 정도 표기는 '지불 자원량'(100, 200 …).
/// </summary>
[DisallowMultipleComponent]
public class ExchangeModalController : MonoBehaviour
{
    [Serializable]
    public class TargetRowUI
    {
        public GameObject root;
        public Image icon;
        public TextMeshProUGUI nameText;     // 선택
        public TextMeshProUGUI receiveText;  // 수령량
        public Button checkbox;              // 좌클릭으로 이 수령 재화 선택(라디오)
        public GameObject checkMark;         // 선택됨 표시
        public GameObject disabledOverlay;   // 무효(교환 불가) 시 반투명 흑회색 오버레이
    }

    [Header("모달 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;

    [Header("지불 재화 선택")]
    [SerializeField] private Button btnSourcePrev;
    [SerializeField] private Button btnSourceNext;
    [SerializeField] private Image sourceIcon;
    [SerializeField] private TextMeshProUGUI sourceNameText;
    [SerializeField] private TextMeshProUGUI sourceHeldText;

    [Header("교환 정도(지불량)")]
    [SerializeField] private Button btnAmountUp;
    [SerializeField] private Button btnAmountDown;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Color amountNormalColor = Color.white;
    [SerializeField] private Color amountInsufficientColor = new Color(0.85f, 0.1f, 0.1f);

    [Header("수령 재화 행 (지불 재화 제외 3종)")]
    [SerializeField] private TargetRowUI[] targetRows = new TargetRowUI[3];

    [Header("진행 / 취소")]
    [SerializeField] private Button btnConfirm; // 진행
    [SerializeField] private Button btnCancel;  // 취소
    [SerializeField] private GameObject confirmDisabledOverlay;

    [Header("안내")]
    [SerializeField] private TextMeshProUGUI stateInfoText;

    [Header("재화 아이콘 (선택)")]
    [SerializeField] private Sprite moneyIcon;
    [SerializeField] private Sprite chipIcon;
    [SerializeField] private Sprite crystalIcon;
    [SerializeField] private Sprite supplyIcon;

    // 지불 재화 순환 순서: 골드 → 메달 → 수정 → 자재
    private static readonly ResourceType[] CycleOrder =
        { ResourceType.Money, ResourceType.Chip, ResourceType.Crystal, ResourceType.Supply };

    private int selectedSourceIndex;
    private int amountPaid;          // 지불량(묶음 단위 배수)
    private int selectedTargetRow = -1; // 선택된 수령 행(-1 미선택)

    private EconomyManager subscribedEco;
    private HQStateManager subscribedHQ;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnSourcePrev != null) btnSourcePrev.onClick.AddListener(() => CycleSource(-1));
        if (btnSourceNext != null) btnSourceNext.onClick.AddListener(() => CycleSource(+1));
        if (btnAmountUp != null) btnAmountUp.onClick.AddListener(() => StepAmount(+1));
        if (btnAmountDown != null) btnAmountDown.onClick.AddListener(() => StepAmount(-1));
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnCancel != null) btnCancel.onClick.AddListener(OnCancel);

        for (int i = 0; i < targetRows.Length; i++)
        {
            int captured = i;
            if (targetRows[i] != null && targetRows[i].checkbox != null)
                targetRows[i].checkbox.onClick.AddListener(() => OnSelectTarget(captured));
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        ResetSelection();
        Refresh();
    }

    private void OnDisable() => Unsubscribe();

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
            subscribedEco.OnResourceChanged += OnResourceChanged;
        }
        if (subscribedHQ == null && gm.HQ != null)
        {
            subscribedHQ = gm.HQ;
            subscribedHQ.OnStateChanged += Refresh;
        }
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedEco != null) { subscribedEco.OnResourceChanged -= OnResourceChanged; subscribedEco = null; }
        if (subscribedHQ != null) { subscribedHQ.OnStateChanged -= Refresh; subscribedHQ = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal() { if (modalRoot != null) modalRoot.SetActive(false); }

    private void ResetSelection()
    {
        selectedSourceIndex = 0;
        amountPaid = ExchangeData.GetBatchUnit(CycleOrder[0]);
        selectedTargetRow = -1;
    }

    // ─── 지불 재화 선택 ─────────────────────────────────────
    private void CycleSource(int dir)
    {
        selectedSourceIndex = ((selectedSourceIndex + dir) % CycleOrder.Length + CycleOrder.Length) % CycleOrder.Length;
        amountPaid = ExchangeData.GetBatchUnit(CycleOrder[selectedSourceIndex]); // 1묶음 리셋
        selectedTargetRow = -1; // 지불 변경 시 수령 선택 해제
        Refresh();
    }

    private void StepAmount(int dir)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        ResourceType src = CycleOrder[selectedSourceIndex];
        int unit = ExchangeData.GetBatchUnit(src);
        int held = gm.Economy.Get(src);
        int maxBatches = held / unit;
        if (maxBatches < 1) { amountPaid = unit; Refresh(); return; }
        int batches = Mathf.Clamp(amountPaid / unit + dir, 1, maxBatches);
        amountPaid = batches * unit;
        Refresh();
    }

    // ─── 수령 재화 선택(체크박스) ──────────────────────────
    private void OnSelectTarget(int rowIndex)
    {
        if (!TryGetTarget(rowIndex, out _)) return; // 무효 행은 선택 불가
        selectedTargetRow = (selectedTargetRow == rowIndex) ? -1 : rowIndex; // 토글
        Refresh();
    }

    // ─── 진행 / 취소 ───────────────────────────────────────
    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        if (selectedTargetRow < 0) return;
        ResourceType src = CycleOrder[selectedSourceIndex];
        if (!TryGetTarget(selectedTargetRow, out ResourceType target)) return;

        int unit = ExchangeData.GetBatchUnit(src);
        int level = gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Exchange) : 0;
        if (level < 1) return;
        if (gm.Economy.Get(src) < unit) return;

        int batches = Mathf.Max(1, amountPaid / unit);
        int perBatch = ExchangeData.GetReceivePerBatch(src, target, level);
        if (perBatch <= 0) return;
        if (gm.Economy.IsAtMax(target)) return;
        int received = batches * perBatch;
        int cost = batches * unit;

        if (!gm.Economy.Has(src, cost)) return;
        if (!gm.Economy.Spend(src, cost)) { Debug.LogError($"[Exchange] Spend 실패: {src} {cost}"); return; }
        gm.Economy.Add(target, received);
        StepAmount(0); // 보유 감소 반영 + 재클램프
    }

    // 취소: 모달 닫기(다른 협회 팝업의 취소와 동작 통일). 다음에 열 때 OnEnable에서 초기화됨.
    private void OnCancel() => CloseModal();

    // ─── Refresh ───────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;

        ResourceType src = CycleOrder[selectedSourceIndex];
        int unit = ExchangeData.GetBatchUnit(src);
        int held = gm.Economy.Get(src);
        int level = gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Exchange) : 0;
        bool unlocked = level >= 1;
        int maxBatches = held / unit;
        bool affordable = maxBatches >= 1;

        if (affordable) amountPaid = Mathf.Clamp(amountPaid, unit, maxBatches * unit);
        else amountPaid = unit;
        int batches = Mathf.Max(1, amountPaid / unit);

        // 지불 재화
        if (sourceIcon != null) { sourceIcon.sprite = GetIcon(src); sourceIcon.enabled = sourceIcon.sprite != null; }
        if (sourceNameText != null) sourceNameText.text = GetKoreanName(src);
        if (sourceHeldText != null) sourceHeldText.text = $"보유 : {held:N0}";

        if (amountText != null)
        {
            amountText.text = $"{amountPaid:N0}";
            amountText.color = affordable ? amountNormalColor : amountInsufficientColor;
        }
        if (btnAmountUp != null) btnAmountUp.interactable = unlocked && affordable && batches < maxBatches;
        if (btnAmountDown != null) btnAmountDown.interactable = unlocked && affordable && batches > 1;

        // 수령 재화 3행
        for (int r = 0; r < targetRows.Length; r++)
        {
            var row = targetRows[r];
            if (row == null) continue;
            bool valid = TryGetTarget(r, out ResourceType target);
            if (row.root != null) row.root.SetActive(true); // 무효 행도 표시(비활성 오버레이로)

            // 아이콘/이름은 무효 행도 항상 갱신(무효 행이 이전 자원 아이콘을 남기는 버그 방지)
            if (row.icon != null) { row.icon.sprite = GetIcon(target); row.icon.enabled = row.icon.sprite != null; }
            if (row.nameText != null) row.nameText.text = GetKoreanName(target);

            int received = 0;
            if (valid)
            {
                int perBatch = ExchangeData.GetReceivePerBatch(src, target, level);
                received = batches * perBatch;
            }
            if (row.receiveText != null) row.receiveText.text = valid ? $"{received:N0}" : "-";
            if (row.disabledOverlay != null) row.disabledOverlay.SetActive(!valid || !unlocked);
            if (row.checkbox != null) row.checkbox.interactable = valid && unlocked;
            if (row.checkMark != null) row.checkMark.SetActive(valid && selectedTargetRow == r);
        }
        if (selectedTargetRow >= 0 && !TryGetTarget(selectedTargetRow, out _)) selectedTargetRow = -1;

        // 진행 버튼 게이트
        bool canConfirm = false;
        if (unlocked && affordable && selectedTargetRow >= 0 && TryGetTarget(selectedTargetRow, out ResourceType selTarget))
        {
            int perBatch = ExchangeData.GetReceivePerBatch(src, selTarget, level);
            canConfirm = perBatch > 0 && (batches * perBatch) > 0 && !gm.Economy.IsAtMax(selTarget);
        }
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;
        if (confirmDisabledOverlay != null) confirmDisabledOverlay.SetActive(!canConfirm);

        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "교환소가 활성화되지 않았습니다.";
            else if (!affordable) msg = $"교환에 필요한 최소 {unit:N0} {GetKoreanName(src)}이(가) 부족합니다.";
            else if (selectedTargetRow < 0) msg = "교환 받을 자원을 선택하세요.";
            stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }

    private bool TryGetTarget(int rowIndex, out ResourceType target)
    {
        target = ResourceType.Money;
        if (rowIndex < 0 || rowIndex >= CycleOrder.Length - 1) return false;
        target = CycleOrder[(selectedSourceIndex + 1 + rowIndex) % CycleOrder.Length];
        return ExchangeData.CanExchange(CycleOrder[selectedSourceIndex], target);
    }

    private Sprite GetIcon(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money: return moneyIcon;
            case ResourceType.Chip: return chipIcon;
            case ResourceType.Crystal: return crystalIcon;
            case ResourceType.Supply: return supplyIcon;
        }
        return null;
    }

    private static string GetKoreanName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money: return "골드";
            case ResourceType.Chip: return "메달";
            case ResourceType.Crystal: return "수정";
            case ResourceType.Supply: return "자재";
        }
        return type.ToString();
    }
}
