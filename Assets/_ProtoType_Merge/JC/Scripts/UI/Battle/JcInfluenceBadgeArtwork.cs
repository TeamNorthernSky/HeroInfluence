using TMPro;
using UnityEngine;

// JC | 2026-11-15 | HUD와 IP 안내 패널이 같은 프레임과 실제 netmarbleB 폰트를 사용합니다.
public sealed class JcInfluenceBadgeArtwork : ScriptableObject
{
    public TMP_FontAsset font;
    public Sprite[] frames;
    public static JcInfluenceBadgeArtwork Load() => Resources.Load<JcInfluenceBadgeArtwork>("BattleEntry/InfluenceBadgeArtwork");
    public Sprite Frame(int tier) => frames != null && tier >= 0 && tier < frames.Length ? frames[tier] : null;
    public void Style(TMP_Text text)
    {
        if (font) text.font = font;
        text.fontStyle = FontStyles.Normal;
        text.color = new Color32(255, 250, 235, 255);
    }
}
