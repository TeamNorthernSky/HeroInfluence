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
    private void OnEnable() { lastUnit = -1; lastLevel = -1; lastCore = -1; lastCoreLevel = -1; }
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
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => {
                if (SkillTooltip.Instance != null)
                    SkillTooltip.Instance.ShowInfo(image.sprite, skill.SkillName + $" Lv.{level}", ClassSkillTooltipText.BuildDesc(skill, level), image.rectTransform, 250f, 0f, 0f);
            });
            var leave = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            leave.callback.AddListener(_ => { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); });
            events.triggers.Add(enter); events.triggers.Add(leave);
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
        BindCoreHover(equippedCore, data != null ? data.WeaponName + $" Lv.{level}" : "코어",
            data != null ? WeaponTooltipText.BuildWeaponLevelDesc(data, core, level) : "");
        BindCoreHover(equippedCoreSkill, data != null ? data.WeaponSkillName + $" Lv.{level}" : "코어 스킬",
            data != null ? WeaponTooltipText.BuildWeaponSkillDesc(data, core, level) : "");
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

    private static void BindCoreHover(Image image, string title, string description)
    {
        if (image == null) return;
        var legacy = image.GetComponent<HeroInfoButtonHover>();
        if (legacy != null) legacy.enabled = false;
        var events = image.GetComponent<EventTrigger>();
        if (events == null) events = image.gameObject.AddComponent<EventTrigger>();
        events.triggers.Clear();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => {
            if (image.sprite != null && SkillTooltip.Instance != null)
                SkillTooltip.Instance.ShowInfo(image.sprite, title, description, image.rectTransform, 250f, 0f, 0f);
        });
        var leave = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        leave.callback.AddListener(_ => { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); });
        events.triggers.Add(enter); events.triggers.Add(leave);
    }

    private void OnDisable() { if (SkillTooltip.Instance != null) SkillTooltip.Instance.Hide(); }
}