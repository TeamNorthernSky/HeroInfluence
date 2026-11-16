using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// [KJ 260910] 파티 카드 랭크 배지. 레벨 → 랭크(UnitRankLookup) → 'UI_icon_rank{랭크}' 그림.
/// 맞는 그림이 없으면(랭크 미확인 등) 배지를 숨긴다.
/// </summary>
[DisallowMultipleComponent]
public class HeroRankBadge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const string SpritePrefix = "UI_icon_rank";

    [SerializeField] private Image icon;
    [Tooltip("UI_icon_rankF ~ UI_icon_rankSSS. 이름으로 찾으므로 순서 무관.")]
    [SerializeField] private Sprite[] rankSprites;
    private TMP_FontAsset tooltipFont;
    private string currentRank;
    private bool hovering;
    private LicenseHoverTooltip licenseInfluence;
    private WorldInfluenceBadgeTooltip worldInfluence;
    private JcHeroInfluenceBadge battleInfluence;

    public void ConfigureTooltip(Sprite[] sprites, TMP_FontAsset font, LicenseHoverTooltip influence = null)
    {
        rankSprites = sprites;
        tooltipFont = font;
        licenseInfluence = influence;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();
    private void OnDisable() => HideTooltip();

    private void LateUpdate()
    {
        if (hovering && !CanShowTooltip()) { HideTooltip(); return; }
        if (hovering && SkillTooltip.Instance) SkillTooltip.Instance.RefreshRankPosition(transform as RectTransform);
    }

    private bool CanShowTooltip()
    {
        if (licenseInfluence != null) return licenseInfluence.CanShowInfluenceTooltip;
        if (worldInfluence != null) return worldInfluence.CanShowTooltip;
        if (battleInfluence != null) return battleInfluence.CanShowTooltip;
        // 같은 카드/HUD의 I.P 호버가 있는 경우에만 랭크 안내도 제공한다.
        for (var scope = transform.parent; scope != null; scope = scope.parent)
        {
            worldInfluence = scope.GetComponentInChildren<WorldInfluenceBadgeTooltip>(true);
            if (worldInfluence != null) return worldInfluence.CanShowTooltip;
            battleInfluence = scope.GetComponentInChildren<JcHeroInfluenceBadge>(true);
            if (battleInfluence != null) return battleInfluence.CanShowTooltip;
            if (scope.GetComponent<HeroProfileButton>() != null || scope.GetComponent<Canvas>() != null) break;
        }
        return false;
    }

    private void HideTooltip()
    {
        hovering = false;
        if (SkillTooltip.Instance) SkillTooltip.Instance.HideRank(transform as RectTransform);
    }

    private void ShowTooltip()
    {
        if (!CanShowTooltip()) { HideTooltip(); return; }
        if (!SkillTooltip.Instance || string.IsNullOrEmpty(currentRank)) return;
        if (!tooltipFont)
        {
            var text = transform.parent ? transform.parent.GetComponentInChildren<TMP_Text>(true) : null;
            tooltipFont = text ? text.font : TMP_Settings.defaultFontAsset;
        }
        SkillTooltip.Instance.ShowRank(currentRank, rankSprites, transform as RectTransform, tooltipFont);
    }

    public void SetLevel(int level) => SetRank(UnitRankLookup.GetRank(level));

    public void SetRank(string rank)
    {
        bool changed = currentRank != rank;
        currentRank = rank;
        if (icon == null) icon = GetComponent<Image>();
        if (icon == null) return;

        Sprite s = FindSprite(rankSprites, rank);
        icon.enabled = s != null;
        icon.raycastTarget = true;
        if (s != null) icon.sprite = s;
        if (hovering && changed) ShowTooltip();
    }

    public static Sprite FindSprite(Sprite[] sprites, string rank)
    {
        if (sprites == null || string.IsNullOrEmpty(rank)) return null;
        string wanted = SpritePrefix + rank;
        foreach (var s in sprites)
            if (s != null && s.name == wanted) return s;
        return null;
    }
}
