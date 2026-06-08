using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 교환소 모달 컨트롤러. 자원 ↔ 자원 교환.
/// 흐름: ① 지불 재화 선택(화살표 순환) → ② 정도 선택(지불량, 묶음 단위 증감) → ③ 수령 재화 행의 교환 버튼 클릭.
/// 환산 규칙은 <see cref="ExchangeData"/> 참조(기준 묶음 단위, 강화 보너스는 묶음당).
/// 정도 표기는 '지불 자원량'(100, 200 …). ▲▼ 1회당 묶음 단위(골드 100 / 그 외 5)씩 증감.
/// 보유량이 1묶음 미만이면 정도에 묶음 단위 값을 붉은색으로 유지 + 교환 버튼 비활성.
/// </summary>
[DisallowMultipleComponent]
public class ExchangeModalController : MonoBehaviour
{
    [Serializable]
    public class TargetRowUI
    {
        public Image icon;
        public TextMeshProUGUI nameText;     // 수령 재화 이름(선택)
        public TextMeshProUGUI receiveText;  // 수령량(예: "+12")
        public Button exchangeButton;
    }

    [Header("모달 본체")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button btnClose;

    [Header("지불 재화 선택")]
    [SerializeField] private Button btnSourcePrev;
    [SerializeField] private Button btnSourceNext;
    [SerializeField] private Image sourceIcon;
    [SerializeField] private TextMeshProUGUI sourceNameText;
    [SerializeField] private TextMeshProUGUI sourceHeldText; // 보유량

    [Header("교환 정도(지불량)")]
    [SerializeField] private Button btnAmountUp;
    [SerializeField] private Button btnAmountDown;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Color amountNormalColor = Color.black;
    [SerializeField] private Color amountInsufficientColor = new Color(0.85f, 0.1f, 0.1f);

    [Header("수령 재화 행 (지불 재화 제외 3종)")]
    [SerializeField] private TargetRowUI[] targetRows = new TargetRowUI[3];

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
    private int amountPaid; // 지불량(묶음 단위의 배수). 보유 부족 시 묶음 단위 값 유지.

    private EconomyManager subscribedEco;
    private HQStateManager subscribedHQ;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnSourcePrev != null) btnSourcePrev.onClick.AddListener(() => CycleSource(-1));
        if (btnSourceNext != null) btnSourceNext.onClick.AddListener(() => CycleSource(+1));
        if (btnAmountUp != null) btnAmountUp.onClick.AddListener(() => StepAmount(+1));
        if (btnAmountDown != null) btnAmountDown.onClick.AddListener(() => StepAmount(-1));

        for (int i = 0; i < targetRows.Length; i++)
        {
            int captured = i;
            if (targetRows[i] != null && targetRows[i].exchangeButton != null)
                targetRows[i].exchangeButton.onClick.AddListener(() => OnExchange(captured));
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        selectedSourceIndex = 0;
        amountPaid = ExchangeData.GetBatchUnit(CycleOrder[0]);
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
            subscribedHQ.OnStateChanged += Refresh; // 교환소 강화 시 효율(보너스) 갱신
        }
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedEco != null) { subscribedEco.OnResourceChanged -= OnResourceChanged; subscribedEco = null; }
        if (subscribedHQ != null) { subscribedHQ.OnStateChanged -= Refresh; subscribedHQ = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
    }

    // ─── 지불 재화 선택 ─────────────────────────────────────
    private void CycleSource(int dir)
    {
        selectedSourceIndex = ((selectedSourceIndex + dir) % CycleOrder.Length + CycleOrder.Length) % CycleOrder.Length;
        amountPaid = ExchangeData.GetBatchUnit(CycleOrder[selectedSourceIndex]); // 1묶음으로 리셋
        Refresh();
    }

    // ─── 정도(지불량) 증감 — 묶음 단위씩 ──────────────────────
    private void StepAmount(int dir)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        ResourceType src = CycleOrder[selectedSourceIndex];
        int unit = ExchangeData.GetBatchUnit(src);
        int held = gm.Economy.Get(src);
        int maxBatches = held / unit;
        if (maxBatches < 1) { amountPaid = unit; Refresh(); return; } // 부족 — 1묶음 표기 유지
        int batches = Mathf.Clamp(amountPaid / unit + dir, 1, maxBatches);
        amountPaid = batches * unit;
        Refresh();
    }

    // ─── 교환 실행 ─────────────────────────────────────────
    private void OnExchange(int rowIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        ResourceType src = CycleOrder[selectedSourceIndex];
        if (!TryGetTarget(rowIndex, out ResourceType target)) return;

        int unit = ExchangeData.GetBatchUnit(src);
        int level = gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Exchange) : 0;
        if (level < 1) return;
        if (gm.Economy.Get(src) < unit) return; // 1묶음 미만 보유 — 교환 불가

        int batches = Mathf.Max(1, amountPaid / unit);
        int perBatch = ExchangeData.GetReceivePerBatch(src, target, level);
        if (perBatch <= 0) return;
        int received = batches * perBatch;
        int cost = batches * unit;

        if (!gm.Economy.Has(src, cost)) return;
        if (!gm.Economy.Spend(src, cost))
        {
            Debug.LogError($"[Exchange] Spend 실패: {src} {cost}");
            return;
        }
        gm.Economy.Add(target, received);
        // 보유량 감소 반영 — 정도 재클램프 후 갱신
        StepAmount(0);
    }

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

        // 정도(지불량) 클램프
        if (affordable)
            amountPaid = Mathf.Clamp(amountPaid, unit, maxBatches * unit);
        else
            amountPaid = unit; // 부족 시 묶음 단위 값 유지(붉은색 표기)
        int batches = Mathf.Max(1, amountPaid / unit);

        // 지불 재화 영역
        if (sourceIcon != null) { sourceIcon.sprite = GetIcon(src); sourceIcon.enabled = sourceIcon.sprite != null; }
        if (sourceNameText != null) sourceNameText.text = GetKoreanName(src);
        if (sourceHeldText != null) sourceHeldText.text = $"보유 : {held:N0}";

        // 정도 표기(지불 자원량) — 부족하면 붉은색
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
            if (!TryGetTarget(r, out ResourceType target))
            {
                if (row.exchangeButton != null) row.exchangeButton.interactable = false;
                continue;
            }
            int perBatch = ExchangeData.GetReceivePerBatch(src, target, level);
            int received = batches * perBatch;

            if (row.icon != null) { row.icon.sprite = GetIcon(target); row.icon.enabled = row.icon.sprite != null; }
            if (row.nameText != null) row.nameText.text = GetKoreanName(target);
            if (row.receiveText != null) row.receiveText.text = $"{received:N0}";
            if (row.exchangeButton != null)
                row.exchangeButton.interactable = unlocked && affordable && received > 0;
        }

        // 안내
        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "교환소가 활성화되지 않았습니다.";
            else if (!affordable) msg = $"교환에 필요한 최소 {unit:N0}{GetKoreanName(src)}이(가) 부족합니다.";
            stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }

    /// <summary>행 인덱스 → 수령 재화(지불 재화 다음부터 순환, 지불 재화 제외).</summary>
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
