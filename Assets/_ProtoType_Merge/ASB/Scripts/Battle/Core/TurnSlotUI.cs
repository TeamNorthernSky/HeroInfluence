using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnSlotUI : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject highlightFrame;

    [Header("Colors")]
    [SerializeField] private Color playerColor = Color.cyan;
    [SerializeField] private Color enemyColor = Color.red;

    public void Setup(BattleCharactor unit, bool isCurrentTurn)
    {
        if (unit == null) return;

        nameText.text = unit.UnitName;
        portrait.color = unit.IsPlayer ? playerColor : enemyColor;
        highlightFrame.SetActive(isCurrentTurn);
    }
}
