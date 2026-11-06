using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 기존 공용 모달의 선택 영웅을 읽어 라이선스 레이아웃의 추가 표시만 담당한다.
[DisallowMultipleComponent]
public sealed class HeroLicenseView : MonoBehaviour
{
    [SerializeField] private HeroInfoModal owner;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image expFill;
    [SerializeField] private TMP_Text hpValue;
    [SerializeField] private TMP_Text expValue;
    [SerializeField] private Image[] heroSkills;
    [SerializeField] private Image rankBadge;
    [SerializeField] private TMP_Text rankFallback;
    [SerializeField] private Sprite[] rankSprites;
    [SerializeField] private Image equippedCore;
    [SerializeField] private Image equippedCoreSkill;
    [SerializeField] private Sprite[] coreSprites;
    [SerializeField] private Sprite[] coreSkillSprites;
    private int lastCore = -1;
    private int lastCoreLevel = -1;
    private static readonly FieldInfo UnitIndexField = typeof(HeroInfoModal).GetField("currentUnitIndex", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo DamageReductionField = typeof(HeroInfoModal).GetField("damageReductionText", BindingFlags.Instance | BindingFlags.NonPublic);
    private TMP_Text damageReductionValue;
    private int lastUnit = -1;
    private int lastLevel = -1;
    private void OnEnable() { lastUnit = -1; lastLevel = -1; lastCore = -1; lastCoreLevel = -1; BindStatHovers(); }
    private void LateUpdate()
    {
        if (owner == null || UnitIndexField == null) return;
        int index = (int)UnitIndexField.GetValue(owner);
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(index, out var unit) || unit == null) return;
        if (damageReductionValue == null && DamageReductionField != null)
            damageReductionValue = DamageReductionField.GetValue(owner) as TMP_Text;
        if (damageReductionValue != null)
            damageReductionValue.text = $"{Mathf.Clamp01(unit.IngameStats.AvoidRate) * 100f:F0}%";
        float maximum = Mathf.Max(0f, unit.IngameStats.HP);
        hpValue.text = $"{unit.CurrentHp:F0}/{maximum:F0}";
        hpFill.fillAmount = maximum > 0f ? Mathf.Clamp01(unit.CurrentHp / maximum) : 0f;
        expValue.text = $"{unit.Exp}/{unit.MaxExp}";
        expFill.fillAmount = unit.MaxExp > 0 ? Mathf.Clamp01((float)unit.Exp / unit.MaxExp) : 0f;
        RefreshEquippedCore(index);
        var lab = GameManager.Instance != null ? GameManager.Instance.Lab : null;
        if (lab == null || (lastUnit == index && lastLevel == unit.Level)) return;
        lastUnit = index; lastLevel = unit.Level;
        string rank = UnitRankLookup.GetRank(unit.Level);
        var badge = System.Array.Find(rankSprites, sprite => sprite != null && sprite.name == "UI_icon_rank" + rank);
        rankBadge.sprite = badge;
        rankBadge.color = badge != null ? Color.white : new Color32(15, 96, 224, 255);
        rankFallback.text = rank;
        rankFallback.gameObject.SetActive(badge == null);
        var skills = lab.GetClassSkills(index);
        for (int i = 0; i < heroSkills.Length; i++)
        {
            var image = heroSkills[i];
            var skill = i < skills.Count ? skills[i] : null;
            int level = skill != null ? Mathf.Max(1, lab.GetSkillLevel(index, skill.NumericSkillId)) : 1;
            image.sprite = skill != null ? Sprites.Icon.ClassSkill(skill.NumericSkillId, level) : null;
            image.enabled = image.sprite != null;
            image.color = skill != null && lab.IsSkillLearned(index, skill) ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            var events = image.GetComponent<EventTrigger>();
            if (events == null) events = image.gameObject.AddComponent<EventTrigger>();
            events.triggers.Clear();
            if (skill == null) continue;
            image.raycastTarget = true;
            var tip = image.GetComponent<LicenseHoverTooltip>();
            if (!tip) tip = image.gameObject.AddComponent<LicenseHoverTooltip>();
            tip.Bind(skill.SkillName + $" Lv.{level}", SkillTooltipNumbers.OmitIPDescription(SkillTooltipNumbers.FormatPercentageDamage(
                ClassSkillTooltipText.BuildDesc(skill, level, percentageValues: true, unitIndex: index))), image.sprite,
                hpValue.font, false, SkillTagText.Format(skill));
        }
    }
    private void RefreshEquippedCore(int unitIndex)
    {
        var workshop = GameManager.Instance != null ? GameManager.Instance.Workshop : null;
        int core = workshop != null ? workshop.GetEquippedWeaponIndex(unitIndex) : 0;
        int level = core > 0 ? Mathf.Max(1, workshop.GetWeaponLevel(core)) : 0;
        // HeroInfoModal.Open may reapply its legacy CSV icon, even for the same hero/core.
        ApplyCoreIcons(core);
        if (lastCore == core && lastCoreLevel == level && lastUnit == unitIndex) return;
        lastCore = core; lastCoreLevel = level;
        DHWeaponTemplate data = null;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (core > 0 && catalog != null) catalog.TryGetWeaponTemplate(core, out data);
        BindCoreHover(equippedCore, core, data != null ? data.WeaponName : "코어");
        BindCoreHover(equippedCoreSkill, core, data != null ? data.WeaponName : "코어");
    }
    private void ApplyCoreIcons(int core)
    {
        int slot = core - 1; // Workshop's HC001..HC005 use numeric keys 1..5.
        SetCoreIcon(equippedCore, coreSprites, slot);
        SetCoreIcon(equippedCoreSkill, coreSkillSprites, slot);
    }

    private static void SetCoreIcon(Image image, Sprite[] sprites, int slot)
    {
        if (image == null) return;
        image.sprite = sprites != null && slot >= 0 && slot < sprites.Length ? sprites[slot] : null;
        image.enabled = image.sprite != null; // Never retain the previous hero's icon.
        image.preserveAspect = true;
        image.color = Color.white;
    }

    private void BindCoreHover(Image image, int core, string title)
    {
        if (!image) return;
        var legacy = image.GetComponent<HeroInfoButtonHover>();
        if (legacy) legacy.enabled = false;
        var events = image.GetComponent<EventTrigger>();
        if (events) events.triggers.Clear();
        image.raycastTarget = true;
        var tip = image.GetComponent<CoreSelectionTooltip>();
        if (!tip) tip = image.gameObject.AddComponent<CoreSelectionTooltip>();
        tip.Bind(core, title, core > 0 && core <= coreSprites.Length ? coreSprites[core-1] : null, hpValue.font,
            UnitIndexField != null ? (int)UnitIndexField.GetValue(owner) : 0, omitIPDescription: true);
    }

    private void BindStatHovers()
    {
        var tooltips = transform.Find("Panel/Tooltips");
        if (!tooltips) return;
        foreach (string stat in new[] { "Atk", "Def", "CritRate", "CounterRate", "DamageReduction", "Speed", "HP", "IP", "EXP" })
        {
            var t = tooltips.Find("Info_" + stat);
            var image = tooltips.Find("VF_Info_" + (stat == "EXP" ? "EXE" : stat));
            if (!t || !image) continue;
            var hoverArea = t.Find("HoverCheckArea");
            var target = hoverArea ? hoverArea : t;
            if (hoverArea)
            {
                var valueGraphic = t.GetComponent<Graphic>();
                if (valueGraphic) valueGraphic.raycastTarget = false;
                var previous = t.GetComponent<LicenseHoverTooltip>();
                if (previous) previous.enabled = false;
            }
            var graphic=target.GetComponent<Graphic>();
            if (!graphic)
            {
                var hit = target.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                graphic = hit;
            }
            graphic.raycastTarget=true;
            var tip=target.GetComponent<LicenseHoverTooltip>();
            if (!tip) tip=target.gameObject.AddComponent<LicenseHoverTooltip>();
            tip.enabled = true;
            tip.BindImage(image.gameObject);
        }
    }
    private void OnDisable() { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); }
}
