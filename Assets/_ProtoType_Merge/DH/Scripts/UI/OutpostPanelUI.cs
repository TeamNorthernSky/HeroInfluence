using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class OutpostPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [FormerlySerializedAs("mineTypeText")]
    [FormerlySerializedAs("outpostTypeText")]
    [SerializeField] private TMP_Text titleText;
    [FormerlySerializedAs("productionText")]
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Image buildingImage;
    [SerializeField] private Image resourceImage;
    [SerializeField] private Button okButton;

    [Header("Display Presets")]
    [SerializeField] private OutpostUnlockDisplayEntry[] displayEntries = Array.Empty<OutpostUnlockDisplayEntry>();

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

        if (Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space))
        {
            HidePanel();
        }
    }

    private void HandleOutpostClaimed(Outpost outpost)
    {
        if (outpost == null)
            return;

        OutpostUnlockDisplayEntry displayEntry = GetDisplayEntry(outpost.OutpostType);
        string outpostName = GetOutpostDisplayName(displayEntry, outpost.OutpostType);
        string resourceName = GetResourceDisplayName(displayEntry, outpost.OutpostType);
        int amount = Mathf.Max(0, outpost.resourcePerTurn);

        if (titleText != null)
            titleText.text = $"{outpostName} 해방";

        if (descriptionText != null)
            descriptionText.text = $"빌런에게서 '{outpostName}' 해방\n매턴 '{resourceName}' '{amount}' 지급";

        if (amountText != null)
            amountText.text = amount.ToString();

        ApplyImage(buildingImage, displayEntry.BuildingSprite);
        ApplyImage(resourceImage, displayEntry.ResourceSprite);

        ShowPanel();
    }

    private void ShowPanel()
    {
        if (okButton != null)
            okButton.interactable = true;

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

    private OutpostUnlockDisplayEntry GetDisplayEntry(OutpostType outpostType)
    {
        OutpostType normalizedType = NormalizeDisplayType(outpostType);
        if (displayEntries == null)
            return default;

        for (int i = 0; i < displayEntries.Length; i++)
        {
            OutpostUnlockDisplayEntry entry = displayEntries[i];
            if (NormalizeDisplayType(entry.OutpostType) == normalizedType)
                return entry;
        }

        return default;
    }

    private static OutpostType NormalizeDisplayType(OutpostType outpostType)
    {
        return outpostType == OutpostType.Composite
            ? OutpostType.Library
            : OutpostTypeUtility.Normalize(outpostType);
    }

    private static string GetOutpostDisplayName(OutpostUnlockDisplayEntry entry, OutpostType outpostType)
    {
        if (!string.IsNullOrWhiteSpace(entry.OutpostName))
            return entry.OutpostName;

        return NormalizeDisplayType(outpostType) switch
        {
            OutpostType.Bank => "은행",
            OutpostType.Library => "도서관",
            OutpostType.JewelryShop => "보석상",
            OutpostType.BlockStore => "공구상",
            _ => "거점"
        };
    }

    private static string GetResourceDisplayName(OutpostUnlockDisplayEntry entry, OutpostType outpostType)
    {
        if (!string.IsNullOrWhiteSpace(entry.ResourceName))
            return entry.ResourceName;

        return NormalizeDisplayType(outpostType) switch
        {
            OutpostType.Bank => "자금",
            OutpostType.Library => "히어로 메달",
            OutpostType.JewelryShop => "아티펙트 수정",
            OutpostType.BlockStore => "건설 자재",
            _ => "자원"
        };
    }

    private static void ApplyImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
            return;

        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
    }
}

[Serializable]
public struct OutpostUnlockDisplayEntry
{
    [SerializeField] private OutpostType outpostType;
    [SerializeField] private string outpostName;
    [SerializeField] private string resourceName;
    [SerializeField] private Sprite buildingSprite;
    [SerializeField] private Sprite resourceSprite;

    public OutpostType OutpostType => outpostType;
    public string OutpostName => outpostName;
    public string ResourceName => resourceName;
    public Sprite BuildingSprite => buildingSprite;
    public Sprite ResourceSprite => resourceSprite;
}
