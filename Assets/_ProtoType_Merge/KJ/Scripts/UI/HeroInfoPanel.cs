using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroInfoPanel : MonoBehaviour
{
    [SerializeField] private BattleFlowManager flowManager;
    [SerializeField] private Transform portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text ipText;
    [SerializeField] private UnityEngine.UI.Image hpGauge;
    [SerializeField] private UnityEngine.UI.Image ipGauge;

    private BattleCharactor currentUnit;
    private RawImage portraitImage;

    private void Awake()
    {
        if (portrait != null)
        {
            GameObject rawObj = new GameObject("Portrait_Image", typeof(RectTransform), typeof(RawImage));
            rawObj.transform.SetParent(portrait, false);
            RectTransform rt = rawObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            portraitImage = rawObj.GetComponent<RawImage>();
        }
    }

    private void OnEnable()
    {
        if (flowManager == null) flowManager = FindFirstObjectByType<BattleFlowManager>();
        if (flowManager != null)
            flowManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= OnTurnStarted;
        UnsubscribeUnit();
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        UnsubscribeUnit();
        currentUnit = unit;

        if (currentUnit != null)
        {
            currentUnit.OnHpChanged += OnHpChanged;
            currentUnit.OnInfluenceChanged += OnIpChanged;
        }

        Refresh();
    }

    private void UnsubscribeUnit()
    {
        if (currentUnit == null) return;
        currentUnit.OnHpChanged -= OnHpChanged;
        currentUnit.OnInfluenceChanged -= OnIpChanged;
        currentUnit = null;
    }

    private void OnHpChanged(float current, float max) => UpdateHpText(current, max);
    private void OnIpChanged(float current, float max) => UpdateIpText(current, max);

    private void Refresh()
    {
        if (currentUnit == null)
        {
            if (nameText != null) nameText.text = "-";
            if (rankText != null) rankText.text = "-";
            if (hpText != null) hpText.text = "-";
            if (ipText != null) ipText.text = "-";
            if (portraitImage != null) portraitImage.texture = null;
            return;
        }

        if (nameText != null)
            nameText.text = currentUnit.UnitName;

        if (rankText != null)
            rankText.text = GetRank(currentUnit).ToString();

        UpdateHpText(currentUnit.CurrentHp, currentUnit.MaxHp);
        UpdateIpText(currentUnit.CurrentInfluence, currentUnit.MaxInfluence);
        UpdatePortrait();
    }

    private void UpdateHpText(float current, float max)
    {
        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        if (hpGauge != null)
            hpGauge.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void UpdateIpText(float current, float max)
    {
        if (ipText != null)
            ipText.text = $"{(int)current} / {(int)max}";
        if (ipGauge != null)
            ipGauge.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void UpdatePortrait()
    {
        if (portraitImage == null || currentUnit == null) return;

        int unitIndex = currentUnit.SourceData != null ? currentUnit.SourceData.UnitIndex : 0;
        Sprite sp = HeroInfoResult.LoadPortraitByPartySlot(unitIndex);
        portraitImage.texture = sp != null ? sp.texture : null;
        portraitImage.enabled = sp != null;
    }

    private static char GetRank(BattleCharactor unit)
    {
        int level = unit.SourceData != null ? unit.SourceData.Level : unit.Level;
        var templates = DHCsvTemplateCatalog.Instance?.GetLevelUpTemplates();
        if (templates == null) return '-';

        LevelUpData match = null;
        foreach (var row in templates)
        {
            if (row.level <= level) match = row;
            else break;
        }

        return match != null && match.Rank != '\0' ? match.Rank : '-';
    }
}
