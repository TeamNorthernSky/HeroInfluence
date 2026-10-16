using UnityEngine;
using UnityEngine.UI;

public sealed class WorkshopCoreActionView : MonoBehaviour
{
    public Image background;
    public Sprite craftSprite;
    public Sprite equipSprite;

    public void SetOwned(bool owned)
    {
        if (background == null) return;
        background.sprite = owned ? equipSprite : craftSprite;
        background.type = Image.Type.Simple;
    }
}
