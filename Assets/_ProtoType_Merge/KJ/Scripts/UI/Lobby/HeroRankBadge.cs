using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260910] 파티 카드 랭크 배지. 레벨 → 랭크(UnitRankLookup) → 'UI_icon_rank{랭크}' 그림.
/// 맞는 그림이 없으면(랭크 미확인 등) 배지를 숨긴다.
/// </summary>
[DisallowMultipleComponent]
public class HeroRankBadge : MonoBehaviour
{
    private const string SpritePrefix = "UI_icon_rank";

    [SerializeField] private Image icon;
    [Tooltip("UI_icon_rankF ~ UI_icon_rankSSS. 이름으로 찾으므로 순서 무관.")]
    [SerializeField] private Sprite[] rankSprites;

    public void SetLevel(int level) => SetRank(UnitRankLookup.GetRank(level));

    public void SetRank(string rank)
    {
        if (icon == null) icon = GetComponent<Image>();
        if (icon == null) return;

        Sprite s = FindSprite(rankSprites, rank);
        icon.enabled = s != null;
        if (s != null) icon.sprite = s;
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
