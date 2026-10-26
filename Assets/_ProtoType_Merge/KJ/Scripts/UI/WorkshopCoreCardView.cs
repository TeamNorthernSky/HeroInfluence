using UnityEngine;
using UnityEngine.UI;

public sealed class WorkshopCoreCardView : MonoBehaviour
{
    public Image background;
    public Sprite normalSprite;
    public Sprite equippedSprite;
    [SerializeField] private Image equippedFrame;

    public void SetEquipped(bool equipped)
    {
        if (background != null) background.sprite = equipped ? equippedSprite : normalSprite;
        if (equippedFrame != null) equippedFrame.gameObject.SetActive(equipped);
    }
}
