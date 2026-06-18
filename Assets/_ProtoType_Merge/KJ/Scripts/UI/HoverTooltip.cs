using NPOI.SS.Formula.Functions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Content")]
    [SerializeField] private string title;
    [SerializeField] [TextArea] private string description;

    public bool IsHovering { get; private set; }
    public event System.Action<bool> OnHoverChanged;

    private void Awake()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovering = true;
        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
        //OnHoverChanged?.Invoke(true);
        tooltipPanel.SetActive(true);

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovering = false;
        OnHoverChanged?.Invoke(false);
        tooltipPanel.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(visible);
    }

    public void SetContent(string newTitle, string newDescription)
    {
        title = newTitle;
        description = newDescription;
    }
}
