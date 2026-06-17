using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnSlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject highlightFrame;
    [SerializeField] private RawImage Portrait;
    [SerializeField] private RawImage Frame;

    [Header("Frame Sprites")]
    [SerializeField] private Sprite playerTurnSprite;
    [SerializeField] private Sprite enemyTurnSprite;

    private void Awake()
    {
        if (playerTurnSprite == null)
            playerTurnSprite = Resources.Load<Sprite>("UI_Sprite/UI_Battle/UI_HUD_playerTurn");

        if (enemyTurnSprite == null)
            enemyTurnSprite = Resources.Load<Sprite>("UI_Sprite/UI_Battle/UI_HUD_enemyTurn");
    }

    public void Setup(BattleCharactor unit, bool isCurrentTurn, Sprite portraitSprite = null)
    {
        if (unit == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        bool isPlayer = unit.IsPlayer;

        if (nameText != null)
            nameText.text = unit.UnitName;

        if (Frame != null)
        {
            Sprite frameSprite = isPlayer ? playerTurnSprite : enemyTurnSprite;
            Frame.texture = frameSprite != null ? frameSprite.texture : null;
            Frame.enabled = frameSprite != null;
        }

        if (Portrait != null)
        {
            Portrait.texture = portraitSprite != null ? portraitSprite.texture : null;
            Portrait.enabled = portraitSprite != null;
        }

        if (highlightFrame != null)
            highlightFrame.SetActive(isCurrentTurn);
    }
}
