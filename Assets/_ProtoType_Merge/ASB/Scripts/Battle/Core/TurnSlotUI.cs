using TMPro;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;

public class TurnSlotUI : MonoBehaviour
{
    //[SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject highlightFrame;
    [SerializeField] private Image Portrait;          // [JC 260621] RawImage→Image 전환(포트레이트 라이브러리 Sprite 직접 사용)
    [SerializeField] private RawImage Frame;
    [SerializeField] private RawImage PortraitMask;

    [Header("Frame Sprites")]
    [SerializeField] private Sprite playerTurnSprite;
    [SerializeField] private Sprite enemyTurnSprite;

    private void Awake()
    {
        if (playerTurnSprite == null)
            playerTurnSprite = Resources.Load<Sprite>("UI_Sprite/UI_Battle/UI_HUD_playerTurn");

        if (enemyTurnSprite == null)
            enemyTurnSprite = Resources.Load<Sprite>("UI_Sprite/UI_Battle/UI_HUD_enemyTurn");
        
        if(Portrait != null)
        {
            Portrait.color = new Color (1,1,1,0);
        }
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

        //if (nameText != null)
        //    nameText.text = unit.UnitName;

        if (Frame != null)
        {
            Sprite frameSprite = isPlayer ? playerTurnSprite : enemyTurnSprite;
            Frame.texture = frameSprite != null ? frameSprite.texture : null;
            Frame.enabled = frameSprite != null;
        }

        if (Portrait != null)
        {
            Portrait.sprite = portraitSprite;          // [JC 260621] Image.sprite 직접 결선
            Portrait.enabled = portraitSprite != null;
            //if(Portrait.texture == null)
            //{
            //    PortraitMask.color = new Color(1, 1, 1, 0);
            //}
        }

        if (highlightFrame != null)
            highlightFrame.SetActive(isCurrentTurn);


    }
}
