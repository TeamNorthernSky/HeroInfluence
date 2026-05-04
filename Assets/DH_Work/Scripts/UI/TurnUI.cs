using UnityEngine;
using TMPro; // TextMeshPro 쓸 경우

public class TurnUI : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;

    [SerializeField] private TMP_Text dayText;

    void Update()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        dayText.text = "Day : " + turnManager.GetDay().ToString();
    }
}