using UnityEngine;
using UnityEngine.UI;

public sealed class WorkshopCoreCardView : MonoBehaviour
{
    public Image background;
    public Sprite normalSprite;
    public Sprite equippedSprite;
    private Button button;
    private SpriteState normalButtonStates;
    private bool buttonStatesCaptured;

    public void SetEquipped(bool equipped)
    {
        if (background != null) background.sprite = equipped ? equippedSprite : normalSprite;
        if (button == null) button = GetComponent<Button>();
        if (button == null) return;
        if (!buttonStatesCaptured)
        {
            normalButtonStates = button.spriteState;
            buttonStatesCaptured = true;
        }
        var states = normalButtonStates;
        // Exploration keeps equipped cards on their selected artwork. Workshop
        // still needs their clicks so the equipped core can be upgraded.
        if (equipped)
        {
            states.highlightedSprite = equippedSprite;
            states.pressedSprite = equippedSprite;
            states.selectedSprite = equippedSprite;
        }
        button.spriteState = states;
    }
}
