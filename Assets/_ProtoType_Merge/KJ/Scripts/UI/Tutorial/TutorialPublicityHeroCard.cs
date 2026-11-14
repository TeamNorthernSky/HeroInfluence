using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>기존 영웅 카드 디자인을 사용하는 튜토리얼 전용 선택 버튼.</summary>
public sealed class TutorialPublicityHeroCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText, classText, levelText, coreText;
    [SerializeField] private Image profileImage, background;
    [SerializeField] private GameObject partyMark;
    [SerializeField] private Sprite normalSprite, hoverSprite, selectedSprite;
    [SerializeField] private TMP_Text influence;
    [SerializeField] private GameObject maximum;
    [SerializeField] private Image core;
    private TutorialPublicityController owner;
    private string key;
    private bool hovering;
    private ExplorationCoreSelectController coreIconSource;
    public string HeroKey => key;
    public TutorialPublicityController Owner => owner;

    private void OnEnable() { if (button != null) button.onClick.AddListener(Select); }
    private void OnDisable() { if (button != null) button.onClick.RemoveListener(Select); }
    private void Select() { if (owner != null && owner.isActiveAndEnabled) owner.SelectHero(key); }

    public void Bind(string heroKey, TutorialPublicityController controller)
    {
        key = heroKey;
        owner = controller;
        Refresh();
    }

    public void Refresh()
    {
        if (owner == null || string.IsNullOrEmpty(key)) return;
        owner.TryGetHero(key, out var state, out var template);
        if (nameText != null) nameText.text = template != null ? template.UnitName : key;
        if (classText != null) classText.text = template != null ? template.ClassName : "-";
        if (levelText != null) levelText.text = $"Lv {(state != null ? state.Level : 1)}";
        if (coreText != null) coreText.text = "—";
        if (influence != null) influence.text = state != null ? state.CurrentIp.ToString() : "—";
        if (maximum != null) maximum.SetActive(state != null && state.MaxIp > 0 && state.CurrentIp >= state.MaxIp);
        // 탐사 HUD와 동일하게 튜토리얼 전투의 기본 무기 규칙으로 코어 아이콘을 연결합니다.
        if (core != null)
        {
            if (coreIconSource == null)
                foreach (var source in FindObjectsByType<ExplorationCoreSelectController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (source.gameObject.scene == gameObject.scene) { coreIconSource = source; break; }
            var catalog = TutorialCatalog.Instance;
            Sprite icon = null;
            if (catalog != null && template != null && coreIconSource != null)
            {
                TutorialCombatUnitFactory.ResolveDefaultWeapon(catalog, template, out var weapon);
                if (weapon != null) icon = coreIconSource.GetCoreIcon(weapon.NumericWeaponId);
            }
            core.overrideSprite = null;
            core.sprite = icon;
            core.enabled = icon != null;
            core.color = Color.white;
            core.preserveAspect = true;
        }
        if (partyMark != null) partyMark.SetActive(true);
        if (profileImage != null)
        {
            profileImage.sprite = Sprites.Portrait.Hero(key);
            profileImage.enabled = profileImage.sprite != null;
        }
        var badge = GetComponentInChildren<HeroRankBadge>(true);
        if (badge != null) badge.SetLevel(state != null ? state.Level : 1);
        if (background != null)
        {
            var sprite = owner.isActiveAndEnabled && owner.SelectedKey == key ? selectedSprite :
                (hovering && owner.isActiveAndEnabled ? hoverSprite : normalSprite);
            if (sprite != null) background.sprite = sprite;
        }
    }

    public void OnPointerEnter(PointerEventData data) { hovering = true; Refresh(); }
    public void OnPointerExit(PointerEventData data) { hovering = false; Refresh(); }
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Right && owner != null && owner.isActiveAndEnabled)
            owner.ClearHeroSelection();
    }
}
