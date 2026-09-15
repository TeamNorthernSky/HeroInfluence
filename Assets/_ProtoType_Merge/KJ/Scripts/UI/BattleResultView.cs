using UnityEngine;
using UnityEngine.UI;

public class BattleResultView : MonoBehaviour
{
    [SerializeField] public Transform heroIndex;
    [SerializeField] public Transform skillSlotParent;
    [SerializeField] public Button acceptButton;
    [SerializeField] public HeroInfoResult heroInfoResultPrefab;

    [Tooltip("씬에 미리 배치한 결과 카드입니다. 비워 두면 기존 프리팹 생성 경로를 사용합니다.")]
    public HeroInfoResult[] sceneHeroSlots;
    [Tooltip("스킬 획득창이 열려 있는 동안 숨기는 결과 내용 부모입니다. 기존 프리팹은 비워 둡니다.")]
    public GameObject resultContent;
    [Tooltip("승리/패배 제목을 표시하는 이미지입니다.")]
    public Image resultTitle;
    [Tooltip("승리 결과에 표시할 제목 스프라이트입니다.")]
    public Sprite victoryTitle;
    [Tooltip("패배 결과에 표시할 제목 스프라이트입니다.")]
    public Sprite defeatTitle;
}
