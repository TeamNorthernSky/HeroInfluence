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
        // 실제 장착 데이터가 생기기 전까지 프리팹에 지정한 기본 코어를 표시한다.
        if (core != null) core.enabled = core.sprite != null;
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
