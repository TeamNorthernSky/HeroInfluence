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

    /// <summary>현재 이 슬롯에 표시 중인 유닛입니다. 전투 전용 호버 UI가 읽습니다.</summary>
    public BattleCharactor DisplayedUnit { get; private set; }

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
        DisplayedUnit = unit;
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
            // [JC 260625] Awake가 알파 0(투명)으로 초기화하므로 sprite 결선 시 알파 복구 필수.
            // (커밋 ecdff91이 Awake 가드 ==null→!=null 교정으로 투명화 코드를 활성화시킨 회귀 봉합)
            Portrait.color = new Color(1, 1, 1, portraitSprite != null ? 1f : 0f);
            //if(Portrait.texture == null)
            //{
            //    PortraitMask.color = new Color(1, 1, 1, 0);
            //}
        }

        if (highlightFrame != null)
            highlightFrame.SetActive(isCurrentTurn);


    }
}
