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
    private int lastUnit = -1;
    private int lastLevel = -1;
    private void OnEnable() { lastUnit = -1; lastLevel = -1; lastCore = -1; lastCoreLevel = -1; BindStatHovers(); }
    private void LateUpdate()
    {
        if (owner == null || UnitIndexField == null) return;
        int index = (int)UnitIndexField.GetValue(owner);
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(index, out var unit) || unit == null) return;
        float maximum = Mathf.Max(0f, unit.IngameStats.HP);
        hpValue.text = $"{unit.CurrentHp:F0}/{maximum:F0}";
        hpFill.fillAmount = maximum > 0f ? Mathf.Clamp01(unit.CurrentHp / maximum) : 0f;
        expValue.text = $"{unit.Exp}/{unit.MaxExp}";
        expFill.fillAmount = unit.MaxExp > 0 ? Mathf.Clamp01((float)unit.Exp / unit.MaxExp) : 0f;
        RefreshEquippedCore(index);
        var lab = GameManager.Instance != null ? GameManager.Instance.Lab : null;
        if (lab == null || (lastUnit == index && lastLevel == unit.Level)) return;
        lastUnit = index; lastLevel = unit.Level;
        string rank = RankUtil.FromLevel(unit.Level);
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
            string effect = skill.Effect == 0 ? "공격" : skill.Effect == 1 ? "회복" : "효과";
            tip.Bind(skill.SkillName + $" Lv.{level}", ClassSkillTooltipText.BuildDesc(skill, level), image.sprite,
                hpValue.font, false, effect + $" · IP {skill.IpCost}");
        }
    }
    private void RefreshEquippedCore(int unitIndex)
    {
        var workshop = GameManager.Instance != null ? GameManager.Instance.Workshop : null;
        int core = workshop != null ? workshop.GetEquippedWeaponIndex(unitIndex) : 0;
        int level = core > 0 ? Mathf.Max(1, workshop.GetWeaponLevel(core)) : 0;
        // HeroInfoModal.Open may reapply its legacy CSV icon, even for the same hero/core.
        ApplyCoreIcons(core);
        if (lastCore == core && lastCoreLevel == level) return;
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
        tip.Bind(core, title, core > 0 && core <= coreSprites.Length ? coreSprites[core-1] : null, hpValue.font);
    }

    private void BindStatHovers()
    {
        foreach (var t in GetComponentsInChildren<RectTransform>(true))
        {
            string title = null, desc = null;
            switch (t.name)
            {
                case "VF_Info_Atk": title="공격력"; desc="상대 유닛을 공격할 때 주는 피해량입니다."; break;
                case "VF_Info_Def": title="방어력"; desc="상대의 공격으로 받는 피해를 줄이는 능력치입니다."; break;
                case "VF_Info_CritRate": title="치명타율"; desc="공격 시 치명타가 발생할 확률입니다."; break;
                case "VF_Info_CounterRate": title="반격률"; desc="공격을 받았을 때 반격할 확률입니다."; break;
                case "VF_Info_DamageReduction": title="피해 경감률"; desc="받는 피해를 줄이는 비율입니다."; break;
                case "VF_Info_Speed": title="행동 속도"; desc="전투에서 유닛의 행동 순서에 영향을 줍니다."; break;
                case "VF_Info_HP": title="HP"; desc="현재 체력 / 최대 체력입니다. 체력이 0이 되면 전투 불능 상태가 됩니다."; break;
                case "VF_Info_IP": title="IP"; desc="히어로의 영향력입니다. 스킬 사용에 필요한 자원입니다."; break;
                case "Info_EXP": title="EXP"; desc="현재 경험치 / 다음 레벨까지 필요한 경험치입니다."; break;
            }
            if (title == null) continue;
            var graphic=t.GetComponent<Graphic>();
            if (!graphic) { var hit=t.gameObject.AddComponent<Image>();hit.color=Color.clear;graphic=hit; }
            graphic.raycastTarget=true;
            var tip=t.GetComponent<LicenseHoverTooltip>();
            if (!tip) tip=t.gameObject.AddComponent<LicenseHoverTooltip>();
            tip.Bind(title,desc,null,hpValue.font,true);
        }
    }
    private void OnDisable() { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); }
}