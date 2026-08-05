using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260701] 의무실 유닛 행. Modal_Infirmary의 Content 아래 런타임 복제되는 템플릿.
/// 프로필/이름/HP 표시 + 회복(생존) 또는 부활(사망) 버튼 1개.
/// 자금 차감·롤백은 컨트롤러가 담당(3계층 규약). 본 행은 표시 + 클릭 위임만.
/// </summary>
[DisallowMultipleComponent]
public class InfirmaryUnitRow : MonoBehaviour
{
    [SerializeField] private Image profileImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI ipText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private GameObject disabledOverlay;

    public int UnitIndex { get; private set; } = -1;

    private Action<int> onAction;

    private void Awake()
    {
        if (actionButton != null) actionButton.onClick.AddListener(RaiseAction);
    }

    private void RaiseAction() => onAction?.Invoke(UnitIndex);

    /// <summary>행 바인딩. downed = 사망(HP&lt;=0)이면 부활 버튼, 아니면 회복 버튼.</summary>
    public void Bind(int unitIndex, UnitPersistentData unit, InfirmaryManager infirmary, Action<int> action)
    {
        UnitIndex = unitIndex;
        onAction = action;

        if (profileImage != null)
        {
            profileImage.enabled = true;
            profileImage.sprite = HeroProfileCatalog.GetByUnitIndex(unitIndex);
        }

        float cur = unit != null ? unit.CurrentHp : 0f;
        float max = unit != null ? Mathf.Max(0f, unit.IngameStats.HP) : 0f;
        bool downed = cur <= 0f;

        if (nameText != null)
            nameText.text = ResolveDisplayName(unit);
        // [KJ 260703] 랭크=레벨 파생(RankUtil, 성장 시스템 V6.0), IP=CurrentInfluence 일원화(HeroInfoModal과 동일 규약)
        if (rankText != null)
            rankText.text = unit != null ? $"Rank {RankUtil.FromLevel(unit.Level)}" : "-";
        if (ipText != null)
            ipText.text = unit != null ? $"I.P {unit.CurrentInfluence:F0}" : "-";
        if (hpText != null)
            hpText.text = downed ? "사망" : $"HP {Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
        if (costText != null)
        {
            int cost = infirmary != null ? (downed ? infirmary.GetReviveCost() : infirmary.GetHealCost()) : 0;
            costText.text = $"{cost} 골드";
        }

        bool actionable = infirmary != null && (downed ? infirmary.CanRevive(unitIndex) : infirmary.CanHeal(unitIndex));
        if (actionLabel != null) actionLabel.text = downed ? "부활" : "회복";
        if (actionButton != null) actionButton.interactable = actionable;
        if (disabledOverlay != null) disabledOverlay.SetActive(!actionable);
    }

    // [KJ 260703] 이름 = 템플릿 카탈로그의 Name(HeroInfoModal.TryResolve와 동일 규약). 템플릿 없으면 키 폴백.
    private static string ResolveDisplayName(UnitPersistentData unit)
    {
        if (unit == null) return "-";
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey)
            && catalog.TryGetPlayerUnitTemplate(unit.UnitTemplateKey, out DHPlayerUnitTemplate template)
            && template != null && !string.IsNullOrWhiteSpace(template.UnitName))
            return template.UnitName;
        return string.IsNullOrWhiteSpace(unit.UnitTemplateKey) ? "-" : unit.UnitTemplateKey;
    }
}
