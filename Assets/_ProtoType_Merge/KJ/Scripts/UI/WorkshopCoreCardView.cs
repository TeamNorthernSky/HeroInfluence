using UnityEngine;
using UnityEngine.UI;

public sealed class WorkshopCoreCardView : MonoBehaviour
{
    public Image background;
    public Sprite normalSprite;
    public Sprite equippedSprite;
    public GameObject equippedBadge;

    public void SetEquipped(bool equipped)
    {
        if (background != null) background.sprite = equipped ? equippedSprite : normalSprite;
        if (equippedBadge != null) equippedBadge.SetActive(equipped);
    }
}
