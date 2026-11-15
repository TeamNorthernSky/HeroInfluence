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
        var unit = badge != null ? badge.Unit : null;
        if (unit == null) { Hide(); return; }
        var tooltip = SkillTooltip.Instance;
        if (!tooltip || !positionAnchor || !font) return;
        float current = unit.CurrentInfluence, maximum = unit.IngameStats.Influence;
        if (lastUnit == unit.UnitIndex && lastIp == current && lastMaximum == maximum &&
            lastPosition == positionAnchor.position && lastScale == positionAnchor.lossyScale) return;
        lastUnit = unit.UnitIndex; lastIp = current; lastMaximum = maximum;
        lastPosition = positionAnchor.position; lastScale = positionAnchor.lossyScale;
        tooltip.ShowInfluence(current, maximum, JcHeroInfluenceBadge.Title(current), positionAnchor, font);
    }

    private void Hide()
    {
        if (hovering && SkillTooltip.Instance) SkillTooltip.Instance.Hide();
        hovering = false;
        lastUnit = -1;
    }
}
