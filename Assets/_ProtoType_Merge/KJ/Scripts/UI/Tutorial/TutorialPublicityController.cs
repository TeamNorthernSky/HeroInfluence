using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>튜토리얼 협회 홍보: 템플릿 키 선택, 전용 자금 지출, 전용 IP 증가.</summary>
public sealed class TutorialPublicityController : MonoBehaviour, IHeroSelectionOwner
{
    [SerializeField] private string[] heroKeys = { "10001", "10002", "10003", "10004" };
    [SerializeField] private TutorialCatalog catalog;
    [SerializeField] private GameObject modalPublicityRoot;
    [SerializeField] private Button btnClose, btnCancel, btnConfirm, btnPrev, btnNext, btnMax;
    [SerializeField] private Image heroProfileImage;
    [SerializeField] private GameObject heroSilhouette, selectPromptGo, disabledOverlay;
    [SerializeField] private TextMeshProUGUI currentIPText, cost1ValueText, availableValueText,
        countValueText, totalCostValueText, stateInfoText;
    [SerializeField] private Slider progressSlider;
    public string SelectedKey { get; private set; }
    public System.Collections.Generic.IReadOnlyList<string> HeroKeys => heroKeys;
    private int count;
    private float nextRefresh;

    private TutorialProgressRepository Repository => TutorialProgressRepository.EnsureInstance();

    private void OnEnable()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null && btnCancel != btnClose) btnCancel.onClick.AddListener(ClearHeroSelection);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(Confirm);
        if (btnPrev != null) btnPrev.onClick.AddListener(Decrease);
        if (btnNext != null) btnNext.onClick.AddListener(Increase);
        if (btnMax != null) btnMax.onClick.AddListener(Maximize);
        if (progressSlider != null) progressSlider.onValueChanged.AddListener(OnSliderChanged);
        ClearHeroSelection();
    }

    private void OnDisable()
    {
        if (btnClose != null) btnClose.onClick.RemoveListener(CloseModal);
        if (btnCancel != null && btnCancel != btnClose) btnCancel.onClick.RemoveListener(ClearHeroSelection);
        if (btnConfirm != null) btnConfirm.onClick.RemoveListener(Confirm);
        if (btnPrev != null) btnPrev.onClick.RemoveListener(Decrease);
        if (btnNext != null) btnNext.onClick.RemoveListener(Increase);
        if (btnMax != null) btnMax.onClick.RemoveListener(Maximize);
        if (progressSlider != null) progressSlider.onValueChanged.RemoveListener(OnSliderChanged);
        SelectedKey = null;
        count = 0;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.2f;
        Refresh();
    }

    public bool TryGetHero(string key, out TutorialUnitProgressState state, out DHPlayerUnitTemplate template)
    {
        state = null;
        template = null;
        if (string.IsNullOrEmpty(key) || System.Array.IndexOf(heroKeys, key) < 0) return false;
        var source = TutorialCatalog.Instance != null ? TutorialCatalog.Instance : catalog;
        if (source == null || !source.TryGetPlayerUnitTemplate(key, out template)) return false;
        var repo = Repository;
        if (repo == null) return false;
        state = repo.GetOrCreateUnitState(key);
        // 협회씬부터 단독 실행한 경우에만 기본 스탯을 생성한다. 기존 진행값은 유지한다.
        if (state.MaxHp <= 0 && state.MaxIp <= 0)
        {
            int hp = Mathf.RoundToInt(template.BaseStats.HP);
            int ip = Mathf.RoundToInt(template.BaseStats.Influence);
            state.SetStats(hp, hp, ip, ip, Mathf.RoundToInt(template.BaseStats.Atk));
        }
        return true;
    }

    public void SelectHero(string key)
    {
        if (!TryGetHero(key, out _, out _)) return;
        SelectedKey = key;
        count = 0;
        Refresh();
    }

    public void ClearHeroSelection() { SelectedKey = null; count = 0; Refresh(); }
    public void CloseModal() { if (modalPublicityRoot != null) modalPublicityRoot.SetActive(false); }
    private void Decrease() => SetCount(count - 1);
    private void Increase() => SetCount(count + 1);
    private void Maximize() => SetCount(int.MaxValue);
    private void OnSliderChanged(float value) => SetCount(Mathf.RoundToInt(value));
    public void SetCount(int value)
    {
        count = Mathf.Clamp(value, 0, MaximumCount());
        Refresh();
    }

    private int MaximumCount()
    {
        if (!TryGetHero(SelectedKey, out var state, out _)) return 0;
        var budget = TutorialPublicityState.Get(Repository);
        return budget != null ? Mathf.Min(budget.Pool, Mathf.Max(0, state.MaxIp - state.CurrentIp)) : 0;
    }

    public void Confirm()
    {
        if (!TryGetHero(SelectedKey, out _, out _)) return;
        if (TutorialPublicityState.Get(Repository).TryProgress(SelectedKey, count))
        {
            count = 0;
            // 탐사씬 안에서 홍보한 경우, 월드 영웅이 예전 IP를 다시 저장하지 않게 동기화한다.
            foreach (var unit in FindObjectsByType<TutorialUnitState>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (unit.gameObject.scene == gameObject.scene && unit.UnitTemplateKey == SelectedKey)
                    unit.InitializeFromTutorialState();
        }
        Refresh();
    }

    public void Refresh()
    {
        var repo = Repository;
        if (repo == null) return;
        bool selected = TryGetHero(SelectedKey, out var state, out _);
        var budget = TutorialPublicityState.Get(repo);
        int maximum = MaximumCount();
        count = Mathf.Clamp(count, 0, maximum);
        long cost = (long)count * TutorialPublicityState.CostPerProgress;
        bool affordable = cost <= repo.GetResource(ResourceType.Money);
        bool canConfirm = selected && count > 0 && affordable;
        if (heroSilhouette != null) heroSilhouette.SetActive(false);
        if (heroProfileImage != null)
        {
            heroProfileImage.gameObject.SetActive(true);
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = selected ? Sprites.Portrait.Hero(SelectedKey) : Sprites.Portrait.Unselected;
        }
        if (currentIPText != null) currentIPText.text = selected ? $"현재 IP : {state.CurrentIp}" : "현재 IP : -";
        if (selectPromptGo != null) selectPromptGo.SetActive(!selected);
        if (cost1ValueText != null) cost1ValueText.text = $"{TutorialPublicityState.CostPerProgress:N0}";
        if (availableValueText != null) availableValueText.text = $"{budget.Pool}";
        if (countValueText != null) countValueText.text = $"{count}";
        if (totalCostValueText != null) totalCostValueText.text = $"{cost:N0}";
        if (progressSlider != null)
        {
            progressSlider.wholeNumbers = true;
            progressSlider.minValue = 0;
            progressSlider.maxValue = Mathf.Max(1, maximum);
            progressSlider.SetValueWithoutNotify(count);
            progressSlider.interactable = maximum > 0;
        }
        if (btnPrev != null) btnPrev.interactable = count > 0;
        if (btnNext != null) btnNext.interactable = count < maximum;
        if (btnMax != null) btnMax.interactable = maximum > 0;
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;
        if (disabledOverlay != null) disabledOverlay.SetActive(!canConfirm);
        if (stateInfoText != null)
        {
            string message = !selected ? "영웅을 선택해 주세요." :
                (state.CurrentIp >= state.MaxIp ? "이미 최대 IP에 도달했습니다." :
                (budget.Pool <= 0 ? "진행 가능 횟수를 모두 사용했습니다." :
                (!affordable ? "튜토리얼 자금이 부족합니다." : "")));
            stateInfoText.text = message;
            stateInfoText.gameObject.SetActive(message.Length > 0);
        }
    }
}
