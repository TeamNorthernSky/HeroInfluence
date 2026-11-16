using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 월드 슬롯의 등급 배지 크기에만 적용하는 IP 설명 호버입니다.
[DisallowMultipleComponent]
public sealed class WorldInfluenceBadgeTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private JcInfluenceBadgeView badge;
    [SerializeField] private RectTransform positionAnchor;
    [SerializeField] private TMP_FontAsset font;
    private bool hovering;
    private int lastUnit = -1;
    private float lastIp, lastMaximum;
    private Vector3 lastPosition, lastScale;
    private ExplorationHeroBoxController owner;

    public void BindOwner(ExplorationHeroBoxController controller) => owner = controller;

    public bool CanShowTooltip
    {
        get
        {
            var license = CommonUIManager.Instance != null ? CommonUIManager.Instance.HeroInfoModal : null;
            return isActiveAndEnabled && (license == null || !license.IsOpen) &&
                badge != null && badge.Unit != null && SkillTooltip.Instance && positionAnchor && font;
        }
    }

    public void OnPointerEnter(PointerEventData data)
    {
        hovering = true;
        lastUnit = -1;
        Show();
    }

    public void OnPointerExit(PointerEventData data) => Hide();
    private void OnDisable() => Hide();
    private void LateUpdate() { if (hovering) Show(); }

    private void Show()
    {
        var license = CommonUIManager.Instance != null ? CommonUIManager.Instance.HeroInfoModal : null;
        if (license != null && license.IsOpen) { Hide(); return; }
        var unit = badge != null ? badge.Unit : null;
        if (unit == null) { Hide(); return; }
        var tooltip = SkillTooltip.Instance;
        if (!tooltip || !positionAnchor || !font) return;
        var anchor = owner ? owner.GetInfluenceTooltipAnchor(unit.UnitTemplateKey, positionAnchor) : positionAnchor;
        float current = unit.CurrentInfluence, maximum = unit.IngameStats.Influence;
        if (lastUnit == unit.UnitIndex && lastIp == current && lastMaximum == maximum &&
            lastPosition == anchor.position && lastScale == anchor.lossyScale) return;
        lastUnit = unit.UnitIndex; lastIp = current; lastMaximum = maximum;
        lastPosition = anchor.position; lastScale = anchor.lossyScale;
        tooltip.ShowInfluence(current, maximum, JcHeroInfluenceBadge.Title(current), anchor, font);
    }

    private void Hide()
    {
        if (hovering && SkillTooltip.Instance) SkillTooltip.Instance.Hide();
        hovering = false;
        lastUnit = -1;
    }
}
