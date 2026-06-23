using UnityEngine;
using UnityEngine.UI;

public class BattleResultView : MonoBehaviour
{
    [SerializeField] public Transform heroIndex;
    [SerializeField] public Transform skillSlotParent;
    [SerializeField] public Button acceptButton;
    [SerializeField] public HeroInfoResult heroInfoResultPrefab;
}
