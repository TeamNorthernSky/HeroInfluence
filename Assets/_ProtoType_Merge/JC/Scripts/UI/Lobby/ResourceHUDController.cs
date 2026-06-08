using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ResourceHUDController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countMoney;
    [SerializeField] private TextMeshProUGUI countChip;
    [SerializeField] private TextMeshProUGUI countCrystal;
    [SerializeField] private TextMeshProUGUI countSupply;

    private EconomyManager subscribedEconomy;

    private void Awake()
    {
        AutoBindIfMissing();
    }

    private void OnEnable()
    {
        TryBindAndSubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // 영속 부트가 늦어 OnEnable 시점에 못 잡았다면 재시도
        if (subscribedEconomy == null) TryBindAndSubscribe();
    }

    private void AutoBindIfMissing()
    {
        if (countMoney == null) countMoney = FindCountTmp("Res_Money");
        if (countChip == null) countChip = FindCountTmp("Res_Medal");
        if (countCrystal == null) countCrystal = FindCountTmp("Res_Crystal");
        if (countSupply == null) countSupply = FindCountTmp("Res_Supply");
    }

    private TextMeshProUGUI FindCountTmp(string slotName)
    {
        var slot = transform.Find(slotName);
        if (slot == null) return null;
        var count = slot.Find("Count");
        if (count == null) return null;
        return count.GetComponent<TextMeshProUGUI>();
    }

    private void TryBindAndSubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        if (subscribedEconomy == gm.Economy)
        {
            RefreshAll(gm.Economy);
            return;
        }
        Unsubscribe();
        subscribedEconomy = gm.Economy;
        subscribedEconomy.OnResourceChanged += HandleResourceChanged;
        RefreshAll(subscribedEconomy);
    }

    private void Unsubscribe()
    {
        if (subscribedEconomy != null)
        {
            subscribedEconomy.OnResourceChanged -= HandleResourceChanged;
            subscribedEconomy = null;
        }
    }

    private void HandleResourceChanged(ResourceType type, int value)
    {
        var tmp = GetTmp(type);
        if (tmp != null) tmp.text = value.ToString("N0");
    }

    private void RefreshAll(EconomyManager economy)
    {
        SetCount(countMoney, economy.Get(ResourceType.Money));
        SetCount(countChip, economy.Get(ResourceType.Chip));
        SetCount(countCrystal, economy.Get(ResourceType.Crystal));
        SetCount(countSupply, economy.Get(ResourceType.Supply));
    }

    private static void SetCount(TextMeshProUGUI tmp, int value)
    {
        if (tmp != null) tmp.text = value.ToString("N0");
    }

    private TextMeshProUGUI GetTmp(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money: return countMoney;
            case ResourceType.Chip: return countChip;
            case ResourceType.Crystal: return countCrystal;
            case ResourceType.Supply: return countSupply;
            default: return null;
        }
    }
}
