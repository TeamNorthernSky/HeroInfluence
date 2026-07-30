using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홍보 모달 컨트롤러. 단일 Modal_Publicity 안에서 영웅 정보+진행 횟수+비용+슬라이더+확정/취소.
/// 영웅 선택 sub-modal은 별도 신설(Modal_PublicityHeroSelect, HeroListController 재활용).
/// </summary>
[DisallowMultipleComponent]
public class PublicityModalController : MonoBehaviour, IHeroSelectionOwner
{
    [Header("Modal_Publicity 본체")]
    [SerializeField] private GameObject modalPublicityRoot;
    [SerializeField] private Button btnClose;

    [Header("영웅 영역")]
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

    private int selectedUnitIndex = -1;
    private int progressCount;
    private PublicityManager subscribedBC;
    private EconomyManager subscribedEco;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        // [KJ 260729] 취소 = 영웅 선택 해제(HeroSlot 우클릭과 동일). 모달 닫기는 btnClose가 담당한다.
        if (btnCancel != null) btnCancel.onClick.AddListener(ClearHeroSelection);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(OnConfirm);
        if (btnPrev != null) btnPrev.onClick.AddListener(() => SetCount(progressCount - 1));
        if (btnNext != null) btnNext.onClick.AddListener(() => SetCount(progressCount + 1));
        if (btnMax != null) btnMax.onClick.AddListener(SetCountToMax);
        if (progressSlider != null) progressSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnEnable()
    {
        TrySubscribe();
        progressCount = 0;
        // [KJ 260728] 파티 패널을 선택 모드로 전환.
        var roster = LobbyUIRegistry.RosterView;
        if (roster != null) roster.BeginSelection(OnHeroSelected);
        else Debug.LogWarning("[PublicityModalController] LobbyUIRegistry.RosterView 없음 — 영웅 선택 불가.");
        Refresh();
    }
    private void OnDisable()
    {
        if (LobbyUIRegistry.RosterView != null) LobbyUIRegistry.RosterView.EndSelection();
        Unsubscribe();
    }
    private void Update()
    {
        if (subscribedBC == null || subscribedEco == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subscribedBC == null && gm.Publicity != null)
        {
            subscribedBC = gm.Publicity;
            subscribedBC.OnStateChanged += Refresh;
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
        if (modalPublicityRoot != null) modalPublicityRoot.SetActive(false);
    }

    // ─── 영웅 선택 ──────────────────────────────────────────
    private void OnHeroSelected(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        progressCount = 0;
        // [KJ 260728] 파티 패널 하이라이트 동기화. 서브모달 폐기로 CloseHeroSelect 호출 제거.
        if (LobbyUIRegistry.RosterView != null) LobbyUIRegistry.RosterView.SetSelected(unitIndex);
        Refresh();
    }

    /// <summary>[KJ 260728] HeroSlot 우클릭 → 선택 해제(IHeroSelectionOwner).</summary>
    public void ClearHeroSelection()
    {
        selectedUnitIndex = -1;
        if (LobbyUIRegistry.RosterView != null) LobbyUIRegistry.RosterView.SetSelected(-1);
        Refresh();
    }

    // ─── 진행 횟수 ──────────────────────────────────────────
    private void OnSliderChanged(float v) => SetCount(Mathf.RoundToInt(v));

    private void SetCount(int newCount)
    {
        progressCount = Mathf.Clamp(newCount, 0, EffectiveMaxCount());
        Refresh();
    }

    private void SetCountToMax()
    {
        progressCount = EffectiveMaxCount();
        Refresh();
    }

    /// <summary>진행 가능 최대 횟수 = min(풀 잔여, 선택 영웅의 IP 잔여 용량). 미선택 시 풀만.</summary>
    private int EffectiveMaxCount()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Publicity == null) return 0;
        int byPool = Mathf.Max(0, gm.Publicity.CurrentPool);
        if (selectedUnitIndex < 0) return byPool;
        return Mathf.Min(byPool, gm.Publicity.GetRemainingCapacity(selectedUnitIndex));
    }

    // ─── 진행 확정 ──────────────────────────────────────────
    private void OnConfirm()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Publicity == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0) return;
        if (progressCount <= 0) return;
        int total = gm.Publicity.GetProgressCost() * progressCount;
        if (!gm.Publicity.CanProgress(selectedUnitIndex, progressCount)) return;
        if (!gm.Economy.Has(ResourceType.Money, total)) return;
        if (!gm.Economy.Spend(ResourceType.Money, total))
        {
            Debug.LogError($"[Publicity] Spend 실패: Money {total}");
            return;
        }
        if (!gm.Publicity.TryProgress(selectedUnitIndex, progressCount))
        {
            Debug.LogError("[Publicity] TryProgress 실패 — 차감 롤백");
            gm.Economy.Add(ResourceType.Money, total); // [JC 260618] 자원 유실 방지 롤백
            return;
        }
        progressCount = 0;
        Refresh();
    }

    // ─── Refresh ────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Publicity == null || gm.Economy == null) return;

        bool hasSelection = TryResolveSelected(out var unit, out var template);
        int pool = gm.Publicity.CurrentPool;
        int costPer = gm.Publicity.GetProgressCost();
        int total = costPer * progressCount;
        bool unlocked = gm.Publicity.IsUnlocked();

        // 영웅 영역
        if (heroSilhouette != null) heroSilhouette.SetActive(false); // 빈 프로필은 기본(00) 이미지로 대체
        if (heroProfileImage != null)
        {
            heroProfileImage.gameObject.SetActive(true);
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = hasSelection
                ? HeroProfileCatalog.GetByUnitIndex(selectedUnitIndex)
                : HeroProfileCatalog.Default;
        }
        if (currentIPText != null)
            currentIPText.text = hasSelection ? $"I.P : {gm.Publicity.GetIP(unit.UnitIndex)}" : "I.P : —";
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasSelection);

        // Info Value 4종
        if (cost1ValueText != null) cost1ValueText.text = $"{costPer:N0}";
        if (availableValueText != null) availableValueText.text = $"{pool}";
        if (countValueText != null) countValueText.text = $"{progressCount}";
        if (totalCostValueText != null) totalCostValueText.text = $"{total:N0}";

        // 선택 영웅의 IP 잔여 용량(MaxIP까지) — 진행 횟수 상한 산정에 사용.
        int capacity = hasSelection ? gm.Publicity.GetRemainingCapacity(unit.UnitIndex) : pool;
        int maxCount = EffectiveMaxCount();

        // Slider — 최소 0 고정 (사용자가 명시적으로 늘려야 진행). 상한은 풀·용량 중 작은 값.
        if (progressSlider != null)
        {
            progressSlider.wholeNumbers = true;
            progressSlider.minValue = 0;
            progressSlider.maxValue = Mathf.Max(1, maxCount);
            progressSlider.SetValueWithoutNotify(progressCount);
        }

        // Confirm 가능 여부
        bool poolOk = pool >= progressCount && progressCount > 0;
        bool capacityOk = !hasSelection || progressCount <= capacity;
        bool moneyOk = gm.Economy.Has(ResourceType.Money, total);
        bool canConfirm = unlocked && hasSelection && poolOk && capacityOk && moneyOk;
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;
        if (disabledOverlay != null) disabledOverlay.SetActive(!canConfirm);

        // 안내
        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "홍보 기능이 활성화되지 않았습니다.";
            else if (!hasSelection) msg = "영웅을 선택해 주세요.";
            else if (pool <= 0) msg = "이번 주 진행 가능 횟수를 모두 사용했습니다.";
            else if (capacity <= 0) msg = $"이미 최대 I.P({(hasSelection ? Mathf.RoundToInt(unit.IngameStats.Influence) : PublicityManager.MaxIP)})에 도달했습니다.";
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
