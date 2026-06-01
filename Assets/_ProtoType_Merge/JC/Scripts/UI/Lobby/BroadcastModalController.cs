using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홍보 모달 컨트롤러. 단일 Modal_Broadcast 안에서 영웅 정보+진행 횟수+비용+슬라이더+확정/취소.
/// 영웅 선택 sub-modal은 별도 신설(Modal_BroadcastHeroSelect, HeroListController 재활용).
/// </summary>
[DisallowMultipleComponent]
public class BroadcastModalController : MonoBehaviour
{
    [Header("Modal_Broadcast 본체")]
    [SerializeField] private GameObject modalBroadcastRoot;
    [SerializeField] private Button btnClose;

    [Header("영웅 영역")]
    [Tooltip("HeroProfile 자체 GO에 부착된 Button (없으면 클릭 영역으로 만들 수 있음). 클릭 시 영웅 선택 모달 열기")]
    [SerializeField] private Button btnHeroSlot;
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette; // 미선택 시 활성
    [SerializeField] private TextMeshProUGUI currentIPText; // Txt_CurrentIP
    [SerializeField] private GameObject selectPromptGo;     // Txt_SelectPrompt — 미선택 시 활성

    [Header("Info — Value TMP 결합")]
    [SerializeField] private TextMeshProUGUI cost1ValueText;
    [SerializeField] private TextMeshProUGUI availableValueText;
    [SerializeField] private TextMeshProUGUI countValueText;
    [SerializeField] private TextMeshProUGUI totalCostValueText;

    [Header("진행 슬라이더 + ±·MAX")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private Button btnMax;

    [Header("진행·취소")]
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnCancel;
    [SerializeField] private GameObject disabledOverlay;
    [SerializeField] private TextMeshProUGUI stateInfoText;

    [Header("영웅 선택 sub-modal")]
    [SerializeField] private GameObject heroSelectModalRoot;
    [SerializeField] private HeroListController heroSelectListController;
    [SerializeField] private Button btnHeroSelectClose;

    private int selectedUnitIndex = -1;
    private int progressCount;
    private BroadcastManager subscribedBC;
    private EconomyManager subscribedEco;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null) btnCancel.onClick.AddListener(CloseModal);
        if (btnHeroSlot != null) btnHeroSlot.onClick.AddListener(OpenHeroSelect);
        if (btnHeroSelectClose != null) btnHeroSelectClose.onClick.AddListener(CloseHeroSelect);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnPrev != null) btnPrev.onClick.AddListener(() => SetCount(progressCount - 1));
        if (btnNext != null) btnNext.onClick.AddListener(() => SetCount(progressCount + 1));
        if (btnMax != null) btnMax.onClick.AddListener(SetCountToMax);
        if (progressSlider != null) progressSlider.onValueChanged.AddListener(OnSliderChanged);

        if (heroSelectListController != null) heroSelectListController.SetSelectionMode(true);
    }

    private void OnEnable() { TrySubscribe(); progressCount = 0; Refresh(); }
    private void OnDisable() { Unsubscribe(); }
    private void Update()
    {
        if (subscribedBC == null || subscribedEco == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subscribedBC == null && gm.Broadcast != null)
        {
            subscribedBC = gm.Broadcast;
            subscribedBC.OnStateChanged += Refresh;
            if (heroSelectListController != null)
                heroSelectListController.UnitSelected += OnHeroSelected;
        }
        if (subscribedEco == null && gm.Economy != null)
        {
            subscribedEco = gm.Economy;
            subscribedEco.OnResourceChanged += OnResourceChanged;
        }
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedBC != null)
        {
            subscribedBC.OnStateChanged -= Refresh;
            if (heroSelectListController != null)
                heroSelectListController.UnitSelected -= OnHeroSelected;
            subscribedBC = null;
        }
        if (subscribedEco != null)
        {
            subscribedEco.OnResourceChanged -= OnResourceChanged;
            subscribedEco = null;
        }
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    public void CloseModal()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(false);
        if (modalBroadcastRoot != null) modalBroadcastRoot.SetActive(false);
    }

    // ─── 영웅 선택 ──────────────────────────────────────────
    private void OpenHeroSelect()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(true);
        if (heroSelectListController != null) heroSelectListController.Rebuild();
    }

    private void CloseHeroSelect()
    {
        if (heroSelectModalRoot != null) heroSelectModalRoot.SetActive(false);
    }

    private void OnHeroSelected(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        CloseHeroSelect();
        progressCount = 0;
        Refresh();
    }

    // ─── 진행 횟수 ──────────────────────────────────────────
    private void OnSliderChanged(float v) => SetCount(Mathf.RoundToInt(v));

    private void SetCount(int newCount)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Broadcast == null) return;
        int max = gm.Broadcast.CurrentPool;
        progressCount = Mathf.Clamp(newCount, 0, Mathf.Max(0, max));
        Refresh();
    }

    private void SetCountToMax()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Broadcast == null) return;
        progressCount = Mathf.Max(0, gm.Broadcast.CurrentPool);
        Refresh();
    }

    // ─── 진행 확정 ──────────────────────────────────────────
    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Broadcast == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0) return;
        if (progressCount <= 0) return;
        int total = gm.Broadcast.GetProgressCost() * progressCount;
        if (!gm.Broadcast.CanProgress(progressCount)) return;
        if (!gm.Economy.Has(ResourceType.Money, total)) return;
        if (!gm.Economy.Spend(ResourceType.Money, total))
        {
            Debug.LogError($"[Broadcast] Spend 실패: Money {total}");
            return;
        }
        if (!gm.Broadcast.TryProgress(selectedUnitIndex, progressCount))
        {
            Debug.LogError("[Broadcast] TryProgress 실패");
            return;
        }
        progressCount = 0;
        Refresh();
    }

    // ─── Refresh ────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Broadcast == null || gm.Economy == null) return;

        bool hasSelection = TryResolveSelected(out var unit, out var template);
        int pool = gm.Broadcast.CurrentPool;
        int costPer = gm.Broadcast.GetProgressCost();
        int total = costPer * progressCount;
        bool unlocked = gm.Broadcast.IsUnlocked();

        // 영웅 영역
        if (heroSilhouette != null) heroSilhouette.SetActive(!hasSelection);
        if (heroProfileImage != null) heroProfileImage.enabled = hasSelection;
        if (currentIPText != null)
            currentIPText.text = hasSelection ? $"I.P : {gm.Broadcast.GetIP(unit.UnitIndex)}" : "I.P : —";
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasSelection);

        // Info Value 4종
        if (cost1ValueText != null) cost1ValueText.text = $"{costPer:N0}";
        if (availableValueText != null) availableValueText.text = $"{pool}";
        if (countValueText != null) countValueText.text = $"{progressCount}";
        if (totalCostValueText != null) totalCostValueText.text = $"{total:N0}";

        // Slider — 최소 0 고정 (사용자가 명시적으로 늘려야 진행)
        if (progressSlider != null)
        {
            progressSlider.wholeNumbers = true;
            progressSlider.minValue = 0;
            progressSlider.maxValue = Mathf.Max(1, pool);
            progressSlider.SetValueWithoutNotify(progressCount);
        }

        // Confirm 가능 여부
        bool poolOk = pool >= progressCount && progressCount > 0;
        bool moneyOk = gm.Economy.Has(ResourceType.Money, total);
        bool canConfirm = unlocked && hasSelection && poolOk && moneyOk;
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;
        if (disabledOverlay != null) disabledOverlay.SetActive(!canConfirm);

        // 안내
        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "홍보 기능이 활성화되지 않았습니다.";
            else if (!hasSelection) msg = "영웅을 선택해 주세요.";
            else if (pool <= 0) msg = "이번 주 진행 가능 횟수를 모두 사용했습니다.";
            else if (!moneyOk) msg = "자원이 부족합니다.";
            stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }

    private bool TryResolveSelected(out UnitPersistentData unit, out UnitData template)
    {
        unit = null;
        template = null;
        if (selectedUnitIndex < 0) return false;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;
        if (!repo.TryGetUnit(selectedUnitIndex, out unit) || unit == null) return false;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
            catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out template);
        return true;
    }
}
