using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>CoreCard Variant의 표시만 담당한다. 데이터 소유권과 장착 처리는 모달에 있다.</summary>
[DisallowMultipleComponent]
public class ExplorationCoreCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject equippedBadge;
    [SerializeField] private GameObject unavailableOverlay;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite equippedSprite;

    public Button Button => button;

    public void BindTooltip(int index, string coreName, Sprite coreIcon)
    {
        var hover = GetComponent<CoreSelectionTooltip>();
        if (!hover) hover = gameObject.AddComponent<CoreSelectionTooltip>();
        hover.Bind(index, coreName, coreIcon, nameText.font);
    }

    public void SetState(string coreName, Sprite coreIcon, bool available, bool equipped, bool canEquip)
    {
        nameText.text = coreName;
        icon.sprite = coreIcon;
        icon.enabled = coreIcon != null;
        background.sprite = equipped ? equippedSprite : normalSprite;
        equippedBadge.SetActive(equipped);
        unavailableOverlay.SetActive(!available);
        button.interactable = available && canEquip && !equipped;
    }
}
