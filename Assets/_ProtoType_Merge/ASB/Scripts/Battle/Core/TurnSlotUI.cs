using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnSlotUI : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject highlightFrame;

    [Header("Sprites")]
    [SerializeField] private Sprite playerTurnSprite;
    [SerializeField] private Sprite enemyTurnSprite;
    [SerializeField] private Image playerFrame;
    [SerializeField] private Image enemyFrame;

    private void Awake()
    {
        if (playerTurnSprite == null)
        {
            playerTurnSprite = Resources.Load<Sprite>("UI_Sprite/UI_Battle/UI_HUD_playerTurn");
        }

        if (enemyTurnSprite == null)
        {
            enemyTurnSprite = Resources.Load<Sprite>("UI_Sprite/UI_Battle/UI_HUD_enemyTurn");
        }
    }

    public void Setup(BattleCharactor unit, bool isCurrentTurn)
    {
        if (unit == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        bool isPlayer = unit.IsPlayer;

        if (nameText != null)
        {
            nameText.text = unit.UnitName;
        }

        if (portrait != null)
        {
            portrait.sprite = isPlayer ? playerTurnSprite : enemyTurnSprite;
            portrait.color = Color.white;
            portrait.enabled = portrait.sprite != null;
        }

        if (playerFrame != null)
        {
            playerFrame.gameObject.SetActive(isPlayer);
        }

        if (enemyFrame != null)
        {
            enemyFrame.gameObject.SetActive(!isPlayer);
        }

        if (highlightFrame != null)
        {
            highlightFrame.SetActive(isCurrentTurn);
        }
    }
}
