using TMPro;
using UnityEngine;

// JC | 2026-11-15 | 전투 밖에서 사용하는 표시 전용 IP 배지. 포인터 이벤트나 툴팁을 연결하지 않습니다.
[DisallowMultipleComponent]
public sealed class JcInfluenceBadgeView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image frame;
    [SerializeField] private TMP_Text title;
    private JcInfluenceBadgeArtwork artwork;
    private int tier = -1;
    public UnitPersistentData Unit { get; private set; }

    public void SetUnit(UnitPersistentData unit)
    {
        Unit = unit;
        gameObject.SetActive(unit != null);
        if (unit != null) SetInfluence(unit.CurrentInfluence);
    }

    public void SetInfluence(float influence)
    {
        if (!artwork) artwork = JcInfluenceBadgeArtwork.Load();
        if (!artwork || !frame || !title) return;
        int next = JcHeroInfluenceBadge.Tier(influence);
        if (tier == next) return;
        tier = next;
        frame.sprite = artwork.Frame(tier);
        frame.color = Color.white;
        frame.raycastTarget = false;
        title.text = JcHeroInfluenceBadge.Title(influence);
        artwork.Style(title);
        title.raycastTarget = false;
    }
}
