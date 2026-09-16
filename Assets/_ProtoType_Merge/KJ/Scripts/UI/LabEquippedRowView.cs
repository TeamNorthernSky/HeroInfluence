using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uses the existing controller's equipped marker as the presentation state source.
public sealed class LabEquippedRowView : MonoBehaviour
{
    public GameObject equippedMarker;
    public CanvasGroup markerVisibility;
    public Image background;
    public Sprite normalSprite, equippedSprite;
    public TMP_Text nameText, levelText;
    public GameObject lockedMarker;
    public CanvasGroup lockMarkerVisibility;
    public Image lockedOverlay;
    public Button[] rowButtons;

    private void LateUpdate()
    {
        bool locked = lockedMarker != null && lockedMarker.activeSelf;
        if (lockMarkerVisibility != null) lockMarkerVisibility.alpha = 0f;
        if (lockedOverlay != null) lockedOverlay.gameObject.SetActive(locked);
        if (rowButtons != null)
            foreach (var button in rowButtons) if (button != null) button.interactable = !locked;
        bool equipped = equippedMarker != null && equippedMarker.activeSelf;
        if (markerVisibility != null)
        {
            markerVisibility.alpha = 0f;
            markerVisibility.blocksRaycasts = false;
        }
        if (background != null) background.sprite = equipped ? equippedSprite : normalSprite;
        Color color = equipped ? Color.white : Color.black;
        if (nameText != null) nameText.color = color;
        if (levelText != null) levelText.color = color;
    }
}