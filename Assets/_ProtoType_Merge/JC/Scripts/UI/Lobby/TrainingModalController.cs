using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 트레이닝 모달 컨트롤러. 영웅 선택 후 공격력(칼)/체력(하트) 두 스탯을 각각 강화.
/// 강화 시 TrainingManager가 영웅 BaseStats를 영구 가산(전투·HeroInfo 즉시 반영).
/// 영웅 선택 sub-modal은 HeroListController(selectionMode) 재활용 — PublicityModalController와 동일 패턴.
/// 레이아웃/비주얼은 추후 공식 리소스로 개편 예정 — 본 컨트롤러는 기능 로직 결선만 담당.
/// </summary>
[DisallowMultipleComponent]
public class TrainingModalController : MonoBehaviour
{
    [Header("Modal_Training 본체")]
    [SerializeField] private GameObject modalTrainingRoot;
    [SerializeField] private Button btnClose;

    [Header("영웅 영역")]
    [SerializeField] private Button btnHeroSlot;
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette;   // 미선택 시 활성
    [SerializeField] private GameObject selectPromptGo;   // 미선택 안내 — 미선택 시 활성
    [SerializeField] private TextMeshProUGUI heroNameText;

    [Header("공격력(칼) 강화 행")]
    [SerializeField] private TextMeshProUGUI atkCurrentValueText; // 현재 공격력
    [SerializeField] private TextMeshProUGUI atkLevelText;        // 강화 레벨 표시
    [SerializeField] private TextMeshProUGUI atkGainText;         // 다음 단계 증가량
    [SerializeField] private TextMeshProUGUI atkCostText;         // 다음 단계 비용
    [SerializeField] private Button btnTrainAttack;
    [SerializeField] private GameObject atkDisabledOverlay;

    [Header("체력(하트) 강화 행")]
    [SerializeField] private TextMeshProUGUI hpCurrentValueText;  // 현재 최대 체력
    [SerializeField] private TextMeshProUGUI hpLevelText;
    [SerializeField] private TextMeshProUGUI hpGainText;
    [SerializeField] private TextMeshProUGUI hpCostText;
    [SerializeField] private Button btnTrainHealth;
    [SerializeField] private GameObject hpDisabledOverlay;

    [Header("공통 안내")]
    [SerializeField] private TextMeshProUGUI stateInfoText;

    [Header("영웅 선택 sub-modal")]
    [SerializeField] private GameObject heroSelectModalRoot;
    [SerializeField] private HeroListController heroSelectListController;
    [SerializeField] private Button btnHeroSelectClose;

    private int selectedUnitIndex = -1;
    private TrainingManager subscribedTM;
    private EconomyManager subscribedEco;

    private void Awake()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnHeroSlot != null) btnHeroSlot.onClick.AddListener(OpenHeroSelect);
        if (btnHeroSelectClose != null) btnHeroSelectClose.onClick.AddListener(CloseHeroSelect);
        if (btnTrainAttack != null) btnTrainAttack.onClick.AddListener(() => TrainStat(TrainingStat.Attack));
        if (btnTrainHealth != null) btnTrainHealth.onClick.AddListener(() => TrainStat(TrainingStat.Health));

        if (heroSelectListController != null)
        {
            heroSelectListController.SetSelectionMode(true);
            heroSelectListController.SetVisitingOnlyMode(true); // [JC 260616] 본부 상주 파티 + 무소속만 트레이닝 대상
        }
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable() { Unsubscribe(); }
    private void Update()
    {
        if (subscribedTM == null || subscribedEco == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (subscribedTM == null && gm.Training != null)
        {
            subscribedTM = gm.Training;
            subscribedTM.OnStateChanged += Refresh;
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
        if (subscribedTM != null)
        {
            subscribedTM.OnStateChanged -= Refresh;
            if (heroSelectListController != null)
                heroSelectListController.UnitSelected -= OnHeroSelected;
            subscribedTM = null;
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
        if (modalTrainingRoot != null) modalTrainingRoot.SetActive(false);
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
        Refresh();
    }

    // ─── 강화 진행 ──────────────────────────────────────────
    private void TrainStat(TrainingStat stat)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Training == null || gm.Economy == null) return;
        if (selectedUnitIndex < 0) return;
        if (!gm.Training.CanTrain(selectedUnitIndex, stat)) return;

        int cost = gm.Training.GetNextCost(selectedUnitIndex, stat);
        if (cost < 0) return;
        if (!gm.Economy.Has(ResourceType.Money, cost)) return;
        if (!gm.Economy.Spend(ResourceType.Money, cost))
        {
            Debug.LogError($"[Training] Spend 실패: Money {cost}");
            return;
        }
        if (!gm.Training.TryTrain(selectedUnitIndex, stat))
        {
            Debug.LogError($"[Training] TryTrain 실패: unit {selectedUnitIndex} stat {stat}");
            return;
        }
        Refresh();
    }

    // ─── Refresh ────────────────────────────────────────────
    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Training == null || gm.Economy == null) return;

        bool unlocked = gm.Training.IsUnlocked();
        bool hasSelection = TryResolveSelected(out var unit);

        // 영웅 영역
        if (heroSilhouette != null) heroSilhouette.SetActive(!hasSelection);
        if (heroProfileImage != null) heroProfileImage.enabled = hasSelection;
        if (selectPromptGo != null) selectPromptGo.SetActive(!hasSelection);
        if (heroNameText != null)
            heroNameText.text = hasSelection && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey)
                ? unit.UnitTemplateKey : (hasSelection ? "—" : "");

        RefreshStatRow(TrainingStat.Attack, hasSelection, unit,
            atkCurrentValueText, atkLevelText, atkGainText, atkCostText, btnTrainAttack, atkDisabledOverlay, unlocked);
        RefreshStatRow(TrainingStat.Health, hasSelection, unit,
            hpCurrentValueText, hpLevelText, hpGainText, hpCostText, btnTrainHealth, hpDisabledOverlay, unlocked);

        if (stateInfoText != null)
        {
            string msg = null;
            if (!unlocked) msg = "트레이닝 기능이 활성화되지 않았습니다.";
            else if (!hasSelection) msg = "영웅을 선택해 주세요.";
            stateInfoText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) stateInfoText.text = msg;
        }
    }

    private void RefreshStatRow(
        TrainingStat stat, bool hasSelection, UnitPersistentData unit,
        TextMeshProUGUI currentValueText, TextMeshProUGUI levelText, TextMeshProUGUI gainText,
        TextMeshProUGUI costText, Button trainButton, GameObject disabledOverlay, bool unlocked)
    {
        var gm = GameManager.Instance;
        int idx = selectedUnitIndex;

        float currentValue = 0f;
        if (hasSelection && unit != null)
            currentValue = stat == TrainingStat.Attack ? unit.IngameStats.Atk : unit.IngameStats.HP; // [JC 260616] 표시는 인게임 스탯

        int level = hasSelection ? gm.Training.GetLevel(idx, stat) : 0;
        int maxLv = gm.Training.GetMaxTrainableLevel();
        int cost = hasSelection ? gm.Training.GetNextCost(idx, stat) : -1;
        float gain = hasSelection ? gm.Training.GetNextGain(idx, stat) : 0f;
        bool maxed = hasSelection && level >= maxLv;

        if (currentValueText != null) currentValueText.text = hasSelection ? $"{currentValue:N0}" : "—";
        if (levelText != null) levelText.text = hasSelection ? $"Lv. {level} / {maxLv}" : "—";
        if (gainText != null) gainText.text = (hasSelection && !maxed) ? $"+{gain:N0}" : "—";
        if (costText != null) costText.text = (hasSelection && !maxed && cost >= 0) ? $"{cost:N0}" : "—";

        bool moneyOk = hasSelection && cost >= 0 && gm.Economy.Has(ResourceType.Money, cost);
        bool canTrain = unlocked && hasSelection && gm.Training.CanTrain(idx, stat) && moneyOk;
        if (trainButton != null) trainButton.interactable = canTrain;
        if (disabledOverlay != null) disabledOverlay.SetActive(!canTrain);
    }

    private bool TryResolveSelected(out UnitPersistentData unit)
    {
        unit = null;
        if (selectedUnitIndex < 0) return false;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;
        if (!repo.TryGetUnit(selectedUnitIndex, out unit) || unit == null) return false;
        return true;
    }
}
