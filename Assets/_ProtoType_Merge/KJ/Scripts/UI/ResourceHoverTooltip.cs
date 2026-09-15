using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ResourceHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public ResourceType resource;
    public GameObject panel;
    public TMP_Text amountText;
    private void Awake() { if (panel != null) panel.SetActive(false); }
    private void OnDisable() { if (panel != null) panel.SetActive(false); }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (panel == null) return;
        panel.SetActive(true);
        var canvas = panel.GetComponent<Canvas>();
        var parent = GetComponentInParent<Canvas>();
        if (canvas != null && parent != null) canvas.sortingOrder = parent.sortingOrder + 1;
        RefreshAmount();
    }
    public void OnPointerExit(PointerEventData eventData) { if (panel != null) panel.SetActive(false); }
    private void Update() { if (panel != null && panel.activeSelf) RefreshAmount(); }
    private void RefreshAmount()
    {
        var gm = GameManager.Instance;
        if (amountText != null) amountText.text = gm != null && gm.Economy != null
            ? gm.Economy.Get(resource).ToString("N0") : "-";
    }
}