using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 의무실의 표시만 담당한다. 회복/부활/비용 차감은 기존 컨트롤러가 처리한다.
[DisallowMultipleComponent]
public sealed class InfirmaryLayoutView : MonoBehaviour
{
    [SerializeField] private InfirmaryUnitRow row;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text recoveryText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text totalCost;
    [SerializeField] private Button actionButton;
    [SerializeField] private Sprite healSprite;
    [SerializeField] private Sprite disabledHealSprite;
    [SerializeField] private TMP_Text reviveLabel;
    private void LateUpdate()
    {
        var inf = GameManager.Instance != null ? GameManager.Instance.Infirmary : null;
        if (title != null) title.text = "의무실 Lv." + (inf != null ? inf.Level : 0);
        if (totalCost != null && totalCost.text.EndsWith(" 골드"))
            totalCost.text = totalCost.text.Substring(0, totalCost.text.Length - 3);
        if (row == null || inf == null) return;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(row.UnitIndex, out var unit) || unit == null) return;
        RefreshCard(unit.CurrentHp, unit.IngameStats.HP, inf.GetHealPercent(), inf.GetRevivePercent(), inf.GetHealCost(), inf.GetReviveCost());
        bool downed = unit.CurrentHp <= 0f;
        int cost = downed ? inf.GetReviveCost() : inf.GetHealCost();
        bool shortMoney = GameManager.Instance.Economy != null && !GameManager.Instance.Economy.Has(ResourceType.Money, cost);
        costText.color = shortMoney && (downed || unit.CurrentHp < unit.IngameStats.HP)
            ? new Color(.7f, .03f, .2f) : new Color(.02f, .23f, .62f);
        if (actionButton != null)
        {
            actionButton.image.sprite = actionButton.interactable ? healSprite : disabledHealSprite;
            if (reviveLabel != null) reviveLabel.transform.parent.gameObject.SetActive(downed);
        }
    }
    public void RefreshCard(float current, float maximum, int healPercent, int revivePercent, int healCost, int reviveCost)
    {
        maximum = Mathf.Max(0f, maximum);
        bool downed = current <= 0f;
        hpFill.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        hpText.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(maximum)}";
        float after = Mathf.Clamp(current + maximum * Mathf.Max(0, downed ? revivePercent : healPercent) / 100f, 0f, maximum);
        recoveryText.text = (downed ? "부활 후 " : "회복 후 ") + $"<color=#50751C>{Mathf.CeilToInt(after)}/{Mathf.CeilToInt(maximum)}</color>";
        costText.text = !downed && current >= maximum ? "-" : (downed ? reviveCost : healCost).ToString();
    }
}