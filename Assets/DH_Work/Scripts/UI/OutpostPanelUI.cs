using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class OutpostPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [FormerlySerializedAs("mineTypeText")]
    [SerializeField] private TMP_Text outpostTypeText;
    [SerializeField] private TMP_Text productionText;
    [SerializeField] private Button okButton;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private void Awake()
    {
        if (okButton != null)
            okButton.onClick.AddListener(HidePanel);

        HidePanel();
    }

    private void OnEnable()
    {
        Outpost.OutpostClaimed += HandleOutpostClaimed;
    }

    private void OnDisable()
    {
        Outpost.OutpostClaimed -= HandleOutpostClaimed;
    }

    private void OnDestroy()
    {
        if (okButton != null)
            okButton.onClick.RemoveListener(HidePanel);
    }

    private void Update()
    {
        if (!IsPanelVisible())
            return;

        if (Input.GetKeyDown(closeKey))
            HidePanel();
    }

    private void HandleOutpostClaimed(Outpost outpost)
    {
        if (outpost == null)
            return;

        if (outpostTypeText != null)
            outpostTypeText.text = $"Type : {outpost.GetOutpostTypeDisplayName()}";

        if (productionText != null)
            productionText.text = outpost.GetProductionDisplayText();

        ShowPanel();
    }

    private void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    private void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private bool IsPanelVisible()
    {
        return panelRoot != null && panelRoot.activeSelf;
    }
}
