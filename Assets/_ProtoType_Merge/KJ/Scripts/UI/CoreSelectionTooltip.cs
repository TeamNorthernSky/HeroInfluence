using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>코어 선택 카드의 읽기 전용 정보. 비활성 Button에서도 호버할 수 있다.</summary>
public sealed class CoreSelectionTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private int core;
    private string label;
    private Sprite coreIcon;
    private TMP_FontAsset font;
    private RectTransform panel;
    private TMP_Text title, levelText, stats, description, skillName, skillDescription;
    private Image portrait, skillPortrait;
    private static CoreSelectionTooltip visible;
    private static readonly string[] SkillIcons = { "normal", "multi", "defend", "heal", "debuff" };

    public void Bind(int index, string name, Sprite icon, TMP_FontAsset textFont)
    {
        core = index; label = name; coreIcon = icon; font = textFont;
        if (panel && panel.gameObject.activeSelf) RefreshContent();
    }
    public void OnPointerEnter(PointerEventData data)
    {
        if (core < 1 || core > 5) return;
        if (visible && visible != this) visible.Hide();
        if (!panel) Build();
        if (!panel) return;
        visible = this;
        RefreshContent();
        panel.gameObject.SetActive(true);
        Place();
    }
    public void OnPointerExit(PointerEventData data) => Hide();
    private void OnDisable() => Hide();
    private void OnDestroy() { if (panel) Destroy(panel.gameObject); }
    private void Hide() { if (panel) panel.gameObject.SetActive(false); if (visible == this) visible = null; }
    private void LateUpdate() { if (panel && panel.gameObject.activeSelf) Place(); }

    private void RefreshContent()
    {
        var ws = GameManager.Instance != null ? GameManager.Instance.Workshop : null;
        int stored = ws != null ? ws.GetWeaponLevel(core) : 0;
        int level = Mathf.Clamp(stored, 1, WorkshopManager.MaxWeaponLevel);
        DHWeaponTemplate data = null;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null) catalog.TryGetWeaponTemplate(core, out data);
        title.text = data != null ? data.WeaponName : label;
        levelText.text = stored > 0 ? $"Lv. {level} / {WorkshopManager.MaxWeaponLevel}" : "미보유 · Lv.1 기준";
        stats.text = "현재: " + Bonus(core, level);
        stats.text += level < WorkshopManager.MaxWeaponLevel ? "\n다음 레벨: " + Bonus(core, level + 1) : "\n최대 레벨";
        description.text = data != null ? data.WeaponDescription : "코어 정보를 불러오는 중입니다.";
        skillName.text = data != null ? data.WeaponSkillName : "코어 스킬";
        skillDescription.text = data != null ? WeaponTooltipText.BuildWeaponSkillDesc(data, core, level) : "스킬 정보를 불러오는 중입니다.";
        portrait.sprite = coreIcon;
        skillPortrait.sprite = Resources.Load<Sprite>("UI_Sprite/UI_Icon/CoreSkill/UI_icon_coreSkill_" + SkillIcons[core - 1]);
        skillPortrait.enabled = skillPortrait.sprite != null;
    }
    private static string Bonus(int index, int level)
    {
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null || !catalog.TryGetWeaponBonusAtLevel(index, level, out var s)) return "정보 없음";
        var parts = new List<string>();
        if (s.Atk != 0) parts.Add($"공격력 +{s.Atk:0.##}");
        if (s.HP != 0) parts.Add($"체력 +{s.HP:0.##}");
        if (s.DEF != 0) parts.Add($"방어력 +{s.DEF:0.##}");
        if (s.CriticalRate != 0) parts.Add($"치명타 +{s.CriticalRate * 100:0.#}%");
        if (s.CounterRate != 0) parts.Add($"반격 +{s.CounterRate * 100:0.#}%");
        if (s.AvoidRate != 0) parts.Add($"피해 경감 +{s.AvoidRate * 100:0.#}%");
        return parts.Count > 0 ? string.Join(" / ", parts) : "추가 능력치 없음";
    }
    private RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    private Image Box(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var i = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>(); i.color = color; i.raycastTarget = false; return i;
    }
    private TMP_Text Text(string name, string value, float x, float y, float w, float h, int size, Color color)
    {
        var t = Rect(name, panel, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = value; t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = 17; t.fontSizeMax = size;
        t.color = color; t.raycastTarget = false; return t;
    }
    private void Build()
    {
        var ownerCanvas = GetComponentInParent<Canvas>();
        if (!ownerCanvas) return;
        panel = Rect("CoreInfoTooltip", ownerCanvas.rootCanvas.transform, 0, 0, 450, 590);
        var cv = panel.gameObject.AddComponent<Canvas>(); cv.overrideSorting = true; cv.sortingOrder = ownerCanvas.sortingOrder + 10;
        var group = panel.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        Box("GoldBorder", panel, 0, 0, 450, 590, new Color(1,.8f,.16f));
        Box("BlueBorder", panel, 4, 4, 442, 582, new Color(.04f,.28f,.6f));
        Box("Body", panel, 8, 8, 434, 574, new Color(.015f,.045f,.12f,.99f));
        portrait = Box("CoreIcon", panel, 25, 26, 110, 110, Color.white); portrait.preserveAspect = true;
        title = Text("Title", "", 151, 27, 273, 62, 29, Color.yellow);
        levelText = Text("Level", "", 151, 100, 273, 40, 23, Color.white);
        stats = Text("Stats", "", 26, 157, 398, 103, 23, Color.white);
        Box("Divider", panel, 26, 268, 398, 2, new Color(.1f,.35f,.6f));
        description = Text("Description", "", 26, 282, 398, 78, 23, Color.white);
        Box("SkillDivider", panel, 26, 372, 398, 2, new Color(.1f,.35f,.6f));
        Text("SkillHeader", "◆ 코어 스킬", 26, 384, 398, 36, 25, Color.yellow);
        skillPortrait = Box("SkillIcon", panel, 26, 433, 105, 105, Color.white); skillPortrait.preserveAspect = true;
        skillName = Text("SkillName", "", 146, 429, 278, 42, 25, Color.white);
        skillDescription = Text("SkillDescription", "", 146, 474, 278, 93, 22, Color.white);
        panel.gameObject.SetActive(false);
    }
    private void Place()
    {
        var parent = (RectTransform)panel.parent;
        var corners = new Vector3[4]; ((RectTransform)transform).GetWorldCorners(corners);
        Vector2 right = parent.InverseTransformPoint(corners[2]);
        Vector2 left = parent.InverseTransformPoint(corners[1]);
        float w = panel.rect.width, h = panel.rect.height;
        float x = right.x + 14;
        if (x + w > parent.rect.xMax - 12) x = left.x - w - 14;
        x = Mathf.Clamp(x, parent.rect.xMin + 12, Mathf.Max(parent.rect.xMin + 12, parent.rect.xMax - w - 12));
        float y = Mathf.Clamp(right.y, parent.rect.yMin + h + 12, parent.rect.yMax - 12);
        panel.localPosition = new Vector3(x, y, 0);
    }
}