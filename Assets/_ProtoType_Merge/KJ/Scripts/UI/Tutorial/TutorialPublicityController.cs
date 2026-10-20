using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>튜토리얼 협회 홍보: 템플릿 키 선택, 전용 자금 지출, 전용 IP 증가.</summary>
public sealed class TutorialPublicityController : MonoBehaviour, IHeroSelectionOwner
{
    public const int TutorialMaxIp = 40;
    public const int PracticeCount = 20;
    public enum PracticeInput { Unrestricted, Blocked, HeroSelection, Count, Max, Confirm, Close, CountAndConfirm }
    private PracticeInput practiceInput;
    private int practiceCount = PracticeCount;
    public int MaximizeCount { get; private set; }
    public Button ConfirmButton => btnConfirm;
    public Button CloseButton => btnClose;
    public Button MaxButton => btnMax;
    public Button PreviousButton => btnPrev;
    public Button NextButton => btnNext;
    public Slider ProgressSlider => progressSlider;
    public TMPro.TMP_Text CountCaption
    {
        get
        {
            if (countCaption == null && modalPublicityRoot != null)
                countCaption = modalPublicityRoot.transform.Find("Panel/CountCaption")?.GetComponent<TextMeshProUGUI>();
            return countCaption;
        }
    }
    public GameObject[] InformationTargets => new[]
    {
        cost1ValueText != null ? cost1ValueText.transform.parent.gameObject : null,
        availableValueText != null ? availableValueText.transform.parent.gameObject : null,
        countValueText != null ? countValueText.transform.parent.gameObject : null,
        totalCostValueText != null ? totalCostValueText.transform.parent.gameObject : null
    };
    private bool Allows(PracticeInput input) => practiceInput == PracticeInput.Unrestricted || practiceInput == input ||
        (practiceInput == PracticeInput.CountAndConfirm && (input == PracticeInput.Count || input == PracticeInput.Confirm));
    private bool IsCountPractice => practiceInput == PracticeInput.CountAndConfirm;
    // 독립 Count 실습의 main 잠금은 보존하되 첫 자유 조절 실습은 20 도달 후에도 낮출 수 있습니다.
    private bool PracticeCountReached => practiceInput == PracticeInput.Count && count >= practiceCount;
    // 횟수 제한은 첫 수동 실습에만 적용합니다. 이후 MAX 확정은 남은 실제 횟수를 사용합니다.
    private bool MatchesPracticeCount => !IsCountPractice || (count >= 1 && count <= practiceCount);
    private Image practiceLimitShade;
    private float limitFeedbackUntil;
    [SerializeField, Tooltip("첫 실습에서 20 초과 입력을 시도하면 표시하는 비차단 안내 패널입니다.")]
    private GameObject practiceLimitNotice;
    [SerializeField, Min(1f), Tooltip("상한 안내를 표시하는 시간입니다. 반복 입력하면 시간이 갱신됩니다.")]
    private float limitNoticeDuration = 3f;
    public void SetPracticeInput(PracticeInput input, int targetCount = PracticeCount)
    {
        practiceInput = input;
        practiceCount = targetCount > 0 ? targetCount : PracticeCount;
        if (IsCountPractice && !string.IsNullOrEmpty(SelectedKey))
            count = Mathf.Clamp(count, 1, Mathf.Max(1, Mathf.Min(practiceCount, MaximumCount())));
        if (!IsCountPractice) limitFeedbackUntil = 0f;
        UpdatePracticeLimitVisual();
        Refresh();
    }
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
    public int Count => count;
    public int MaxCount => MaximumCount();
    // 분모는 남은 홍보 횟수이며, 고정 슬라이더 눈금(40)과 실습 목표(20)와 구분한다.
    public int AvailableCount => TutorialPublicityState.Get(Repository)?.Pool ?? 0;
    /// <summary>홍보 확정이 성공한 누적 횟수. 튜토리얼 안내가 "진행을 눌렀다"를 감지하는 데 쓴다.</summary>
    public int ConfirmCount { get; private set; }
    public bool IsModalOpen => modalPublicityRoot != null && modalPublicityRoot.activeInHierarchy;
    private int count;
    private float nextRefresh;
    private TextMeshProUGUI countCaption;

    private TutorialProgressRepository Repository => TutorialProgressRepository.EnsureInstance();

    private void OnEnable()
    {
        if (btnClose != null) btnClose.onClick.AddListener(CloseModal);
        if (btnCancel != null && btnCancel != btnClose) btnCancel.onClick.AddListener(ClearHeroSelection);
        if (btnConfirm != null) btnConfirm.onClick.AddListener(Confirm);
        if (btnPrev != null) btnPrev.onClick.AddListener(Decrease);
        if (btnNext != null) btnNext.onClick.AddListener(Increase);
        PublicityButtonHoldRepeat.Attach(btnPrev);
        PublicityButtonHoldRepeat.Attach(btnNext);
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
        limitFeedbackUntil = 0f;
        if (practiceLimitNotice != null) practiceLimitNotice.SetActive(false);
        if (practiceLimitShade != null) practiceLimitShade.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdatePracticeLimitVisual();
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
            state.SetStats(hp, hp, 0, ip, Mathf.RoundToInt(template.BaseStats.Atk));
        }
        return true;
    }

    public void SelectHero(string key)
    {
        if (!Allows(PracticeInput.HeroSelection)) return;
        if (!TryGetHero(key, out _, out _)) return;
        SelectedKey = key;
        count = 0;
        Refresh();
    }

    /// <summary>홍보 안내를 처음부터 시작할 때만 튜토리얼 영웅의 IP를 준비한다.</summary>
    public void BeginExplanation(string key)
    {
        if (!TryGetHero(key, out var state, out _)) return;
        // 첫 실습(1~20) 후 남은 양을 최대 충전할 수 있도록 튜토리얼 자원만 준비한다.
        var repo = Repository;
        int requiredMoney = TutorialMaxIp * TutorialPublicityState.CostPerProgress;
        repo.SetResource(ResourceType.Money, Mathf.Max(repo.GetResource(ResourceType.Money), requiredMoney));
        TutorialPublicityState.Get(repo).PreparePractice(TutorialMaxIp);
        Repository.SetUnitStats(key, state.CurrentHp, state.MaxHp, 0, TutorialMaxIp,
            state.Atk, state.Level, state.Exp, state.MaxExp);
        foreach (var unit in FindObjectsByType<TutorialUnitState>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (unit.gameObject.scene == gameObject.scene && unit.UnitTemplateKey == key)
                unit.InitializeFromTutorialState();
        SelectedKey = null;
        count = 0;
        Refresh();
    }

    public void ClearHeroSelection() { if (!Allows(PracticeInput.HeroSelection)) return; SelectedKey = null; count = 0; Refresh(); }
    public void CloseModal() { if (Allows(PracticeInput.Close) && modalPublicityRoot != null) modalPublicityRoot.SetActive(false); }
    private void Decrease() => SetCount(count - 1);
    private void Increase() => SetCount(count + 1);
    private void Maximize()
    {
        if (!Allows(PracticeInput.Max)) return;
        count = MaximumCount();
        MaximizeCount++;
        Refresh();
    }
    private void OnSliderChanged(float value) => SetCount(Mathf.RoundToInt(value));
    public void SetCount(int value)
    {
        if (!Allows(PracticeInput.Count) || PracticeCountReached) { Refresh(); return; }
        int maximum = IsCountPractice || practiceInput == PracticeInput.Count ? Mathf.Min(practiceCount, MaximumCount()) : MaximumCount();
        if (IsCountPractice && value > practiceCount) limitFeedbackUntil = Time.unscaledTime + limitNoticeDuration;
        count = ClampSelectedCount(value, maximum, IsCountPractice);
        Refresh();
    }

    private static int ClampSelectedCount(int value, int maximum, bool limitedPractice)
        => Mathf.Clamp(value, limitedPractice && maximum > 0 ? 1 : 0, Mathf.Max(0, maximum));

    // 노브의 이동 영역을 기준으로 20 위치의 오른쪽 끝부터 음영을 표시한다.
    private void UpdatePracticeLimitVisual()
    {
        if (practiceLimitNotice != null)
            practiceLimitNotice.SetActive(IsCountPractice && IsModalOpen && Time.unscaledTime < limitFeedbackUntil);
        if (progressSlider == null) return;
        if (IsCountPractice && practiceLimitShade == null)
        {
            var go = new GameObject("PracticeLimitShade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(progressSlider.transform, false);
            practiceLimitShade = go.GetComponent<Image>();
            practiceLimitShade.raycastTarget = false;
            practiceLimitShade.color = new Color(.32f, .34f, .38f, .55f);
            // 노브가 마지막으로 그려지도록 음영은 슬라이더 자식의 맨 앞에 둔다.
            go.transform.SetAsFirstSibling();
        }
        if (practiceLimitShade == null) return;
        practiceLimitShade.gameObject.SetActive(IsCountPractice);
        if (!IsCountPractice) return;
        var sliderRect = (RectTransform)progressSlider.transform;
        var handle = progressSlider.handleRect;
        var travel = handle != null ? handle.parent as RectTransform : sliderRect;
        if (travel == null) return;
        float ratio = Mathf.InverseLerp(1f, TutorialMaxIp, practiceCount);
        Vector3 edge = travel.TransformPoint(new Vector3(Mathf.Lerp(travel.rect.xMin, travel.rect.xMax, ratio), 0));
        float start = sliderRect.InverseTransformPoint(edge).x;
        if (handle != null)
            start += Mathf.Abs(sliderRect.InverseTransformVector(handle.TransformVector(Vector3.right * handle.rect.width)).x) * .5f + 2f;
        var rect = practiceLimitShade.rectTransform;
        rect.anchorMin = rect.anchorMax = sliderRect.pivot;
        rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = new Vector2(start, sliderRect.rect.center.y);
        rect.sizeDelta = new Vector2(Mathf.Max(0, sliderRect.rect.xMax - start - 2f), Mathf.Max(0, sliderRect.rect.height - 4f));
    }

    private int MaximumCount()
    {
        if (!TryGetHero(SelectedKey, out var state, out _)) return 0;
        var budget = TutorialPublicityState.Get(Repository);
        return budget != null ? Mathf.Min(TutorialMaxIp,
            Mathf.Min(budget.Pool, Mathf.Max(0, state.MaxIp - state.CurrentIp))) : 0;
    }

    public void Confirm()
    {
        if (!Allows(PracticeInput.Confirm)) return;
        if (!MatchesPracticeCount) return;
        if (!TryGetHero(SelectedKey, out _, out _)) return;
        if (TutorialPublicityState.Get(Repository).TryProgress(SelectedKey, count))
        {
            count = 0;
            ConfirmCount++;
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
        if (IsCountPractice) maximum = Mathf.Min(maximum, practiceCount);
        count = Mathf.Clamp(count, 0, maximum);
        long cost = (long)count * TutorialPublicityState.CostPerProgress;
        bool affordable = cost <= repo.GetResource(ResourceType.Money);
        bool canConfirm = selected && count > 0 && affordable && Allows(PracticeInput.Confirm) && MatchesPracticeCount;
        if (heroSilhouette != null) heroSilhouette.SetActive(false);
        if (heroProfileImage != null)
        {
            heroProfileImage.gameObject.SetActive(true);
            heroProfileImage.enabled = true;
            heroProfileImage.sprite = selected ? Sprites.Portrait.Hero(SelectedKey) : Sprites.Portrait.Unselected;
        }
        if (currentIPText != null)
            currentIPText.text = selected
                ? $"현재 IP : {state.CurrentIp}"
                : "현재 IP : -";
        if (selectPromptGo != null) selectPromptGo.SetActive(!selected);
        if (cost1ValueText != null) cost1ValueText.text = $"{TutorialPublicityState.CostPerProgress:N0}";
        if (availableValueText != null) availableValueText.text = $"{budget.Pool}";
        if (countValueText != null) countValueText.text = $"{count}";
        if (totalCostValueText != null) totalCostValueText.text = $"{cost:N0}";
        if (progressSlider != null)
        {
            progressSlider.wholeNumbers = true;
            progressSlider.minValue = IsCountPractice ? 1 : 0;
            // 20 초과 구간은 음영 표시하고 첫 실습의 입력을 차단한다.
            progressSlider.maxValue = TutorialMaxIp;
            progressSlider.SetValueWithoutNotify(count);
            progressSlider.interactable = maximum > 0 && Allows(PracticeInput.Count) && !PracticeCountReached;
            if (CountCaption != null) CountCaption.text = $"{count} / {AvailableCount}";
        }
        if (btnPrev != null) btnPrev.interactable = count > 0 && Allows(PracticeInput.Count) && !PracticeCountReached;
        if (btnNext != null) btnNext.interactable = count < maximum && Allows(PracticeInput.Count) && !PracticeCountReached;
        if (btnMax != null) btnMax.interactable = maximum > 0 && Allows(PracticeInput.Max);
        if (btnClose != null) btnClose.interactable = Allows(PracticeInput.Close);
        if (btnConfirm != null) btnConfirm.interactable = canConfirm;
        if (disabledOverlay != null) disabledOverlay.SetActive(!canConfirm);
        if (stateInfoText != null)
        {
            string message = !selected ? "영웅을 선택해 주세요." :
                (budget.Pool <= 0 ? "이번 주 진행 가능 횟수를 모두 사용했습니다." :
                (state.CurrentIp >= state.MaxIp ? $"이미 최대 I.P({state.MaxIp})에 도달했습니다." :
                (!affordable ? "자원이 부족합니다." : "")));
            stateInfoText.text = message;
            stateInfoText.gameObject.SetActive(message.Length > 0);
        }
    }
}
