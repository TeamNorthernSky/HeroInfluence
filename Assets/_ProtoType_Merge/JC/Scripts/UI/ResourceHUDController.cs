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
    private readonly ResourceGainNumberMotion moneyMotion = new ResourceGainNumberMotion();
    private readonly ResourceGainNumberMotion medalMotion = new ResourceGainNumberMotion();
    private readonly ResourceGainNumberMotion crystalMotion = new ResourceGainNumberMotion();
    private readonly ResourceGainNumberMotion supplyMotion = new ResourceGainNumberMotion();

    private void Awake() => ResolveMissing();

    private void OnEnable() { TrySubscribe(); Refresh(); }

    private void OnDisable()
    {
        if (subscribed != null) { subscribed.OnResourceChanged -= OnResourceChanged; subscribed = null; }
        if (tutorialSubscribed != null) { tutorialSubscribed.TutorialResourceChanged -= OnResourceChanged; tutorialSubscribed = null; }
        ResetMotions();
    }

    private void Update()
    {
        if (useTutorialResources ? tutorialSubscribed == null : subscribed == null) TrySubscribe();
        // Resource rewards can arrive while an NPC dialogue pauses the game.
        float delta = Time.unscaledDeltaTime;
        moneyMotion.Advance(delta);
        medalMotion.Advance(delta);
        crystalMotion.Advance(delta);
        supplyMotion.Advance(delta);
    }

    private void ResetMotions()
    {
        moneyMotion.Reset();
        medalMotion.Reset();
        crystalMotion.Reset();
        supplyMotion.Reset();
    }

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
            ResetMotions();
            Refresh();
            return;
        }
        if (tutorialSubscribed != null) { tutorialSubscribed.TutorialResourceChanged -= OnResourceChanged; tutorialSubscribed = null; }
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null || subscribed != null) return;
        subscribed = gm.Economy;
        subscribed.OnResourceChanged += OnResourceChanged;
        ResetMotions();
        Refresh();
    }

    private void OnResourceChanged(ResourceType _, int __) => Refresh();

    private void Refresh()
    {
        if (useTutorialResources)
        {
            var progress = TutorialProgressRepository.Instance;
            if (progress == null) return;
            moneyMotion.Set(moneyText, progress.GetResource(ResourceType.Money));
            medalMotion.Set(medalText, progress.GetResource(ResourceType.Chip));
            crystalMotion.Set(crystalText, progress.GetResource(ResourceType.Crystal));
            supplyMotion.Set(supplyText, progress.GetResource(ResourceType.Supply));
            return;
        }
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        moneyMotion.Set(moneyText, gm.Economy.Get(ResourceType.Money));
        medalMotion.Set(medalText, gm.Economy.Get(ResourceType.Chip));
        crystalMotion.Set(crystalText, gm.Economy.Get(ResourceType.Crystal));
        supplyMotion.Set(supplyText, gm.Economy.Get(ResourceType.Supply));
    }
    public RectTransform GetResourceAnchor(ResourceType type)
    {
        ResolveMissing();
        TMP_Text label = type == ResourceType.Chip ? medalText : type == ResourceType.Crystal ? crystalText : type == ResourceType.Supply ? supplyText : moneyText;
        return label != null ? label.rectTransform : null;
    }
}
