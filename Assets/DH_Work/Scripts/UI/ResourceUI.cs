using UnityEngine;
using TMPro; // TextMeshPro 쓸 경우

public class ResourceUI : MonoBehaviour
{
    [SerializeField] private ResourceManager resourceManager;

    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text chipText;
    [SerializeField] private TMP_Text crystalText;
    [SerializeField] private TMP_Text supplyText;

    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        if (moneyText != null)
            moneyText.text = "Money : " + resourceManager.GetAmount(ResourceType.Money);

        if (chipText != null)
            chipText.text = "Chip : " + resourceManager.GetAmount(ResourceType.Chip);

        if (crystalText != null)
            crystalText.text = "Crystal : " + resourceManager.GetAmount(ResourceType.Crystal);

        if (supplyText != null)
            supplyText.text = "Supply : " + resourceManager.GetAmount(ResourceType.Supply);
    }
}
