using UnityEngine;
using TMPro; // TextMeshPro 쓸 경우

public class ResourceUI : MonoBehaviour
{
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
        var economy = Game.Economy;
        if (economy == null) return;

        if (moneyText != null)
            moneyText.text = "Money : " + economy.Get(ResourceType.Money);

        if (chipText != null)
            chipText.text = "Chip : " + economy.Get(ResourceType.Chip);

        if (crystalText != null)
            crystalText.text = "Crystal : " + economy.Get(ResourceType.Crystal);

        if (supplyText != null)
            supplyText.text = "Supply : " + economy.Get(ResourceType.Supply);
    }
}
