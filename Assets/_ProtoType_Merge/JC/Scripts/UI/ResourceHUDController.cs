using TMPro;
using UnityEngine;

/// <summary>
/// [JC 260630] 공용 자원 HUD 구동. 자원 4종(자금/메달/수정/자재)을 EconomyManager와 실시간 연동.
/// 로비·탐사 공유 TopBar 프리팹에 부착(중복되던 HQLobbyMenuController/ExplorationHUDController의 자원 HUD 로직 흡수).
/// SerializeField 비우면 이름으로 자동 탐색.
/// </summary>
[DisallowMultipleComponent]
public class ResourceHUDController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI moneyText;   // Text_Money   → ResourceType.Money
    [SerializeField] private TextMeshProUGUI medalText;   // Text_Medal   → ResourceType.Chip
    [SerializeField] private TextMeshProUGUI crystalText; // Text_Crystal → ResourceType.Crystal
    [SerializeField] private TextMeshProUGUI supplyText;  // Text_Supply  → ResourceType.Supply

    [Tooltip("켜면 이 HUD만 튜토리얼 저장소의 보유량을 표시합니다. 일반 경제·보상은 변경하지 않습니다. 씬에 저장됩니다.")]
    public bool useTutorialResources;
    private EconomyManager subscribed;
    private TutorialProgressRepository tutorialSubscribed;

    private void Awake() => ResolveMissing();

    private void OnEnable() { TrySubscribe(); Refresh(); }

    private void OnDisable()
    {
        if (subscribed != null) { subscribed.OnResourceChanged -= OnResourceChanged; subscribed = null; }
        if (tutorialSubscribed != null) { tutorialSubscribed.TutorialResourceChanged -= OnResourceChanged; tutorialSubscribed = null; }
    }

    private void Update() { if (useTutorialResources ? tutorialSubscribed == null : subscribed == null) TrySubscribe(); }

    private void ResolveMissing()
    {
        if (moneyText == null) moneyText = Find("Text_Money");
        if (medalText == null) medalText = Find("Text_Medal");
        if (crystalText == null) crystalText = Find("Text_Crystal");
        if (supplyText == null) supplyText = Find("Text_Supply");
    }

    private static TextMeshProUGUI Find(string n)
    {
        var go = GameObject.Find(n);
        return go != null ? go.GetComponent<TextMeshProUGUI>() : null;
    }

    private void TrySubscribe()
    {
        if (useTutorialResources)
        {
            if (subscribed != null) { subscribed.OnResourceChanged -= OnResourceChanged; subscribed = null; }
            var progress = TutorialProgressRepository.Instance;
            if (progress == null || tutorialSubscribed != null) return;
            tutorialSubscribed = progress;
            tutorialSubscribed.TutorialResourceChanged += OnResourceChanged;
            Refresh();
            return;
        }
        if (tutorialSubscribed != null) { tutorialSubscribed.TutorialResourceChanged -= OnResourceChanged; tutorialSubscribed = null; }
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null || subscribed != null) return;
        subscribed = gm.Economy;
        subscribed.OnResourceChanged += OnResourceChanged;
        Refresh();
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    private void Refresh()
    {
        if (useTutorialResources)
        {
            var progress = TutorialProgressRepository.Instance;
            if (progress == null) return;
            if (moneyText != null) moneyText.text = progress.GetResource(ResourceType.Money).ToString("N0");
            if (medalText != null) medalText.text = progress.GetResource(ResourceType.Chip).ToString("N0");
            if (crystalText != null) crystalText.text = progress.GetResource(ResourceType.Crystal).ToString("N0");
            if (supplyText != null) supplyText.text = progress.GetResource(ResourceType.Supply).ToString("N0");
            return;
        }
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        if (moneyText != null)   moneyText.text   = gm.Economy.Get(ResourceType.Money).ToString("N0");
        if (medalText != null)   medalText.text   = gm.Economy.Get(ResourceType.Chip).ToString("N0");
        if (crystalText != null) crystalText.text = gm.Economy.Get(ResourceType.Crystal).ToString("N0");
        if (supplyText != null)  supplyText.text  = gm.Economy.Get(ResourceType.Supply).ToString("N0");
    }
    public RectTransform GetResourceAnchor(ResourceType type)
    {
        ResolveMissing();
        TMP_Text label = type == ResourceType.Chip ? medalText : type == ResourceType.Crystal ? crystalText : type == ResourceType.Supply ? supplyText : moneyText;
        return label != null ? label.rectTransform : null;
    }
}
