using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TurnIncomeModalController : MonoBehaviour
{
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private TextMeshProUGUI incomeText;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button btnOk;

    [Header("표시 양식")]
    [SerializeField] private string titleFormat = "Day {0} 시작";
    [SerializeField] private string incomeFormat = "본부에서 자금 +{0}을(를) 획득했습니다.";

    private void Awake()
    {
        if (btnOk != null) btnOk.onClick.AddListener(Close);
        if (modalRoot != null) modalRoot.SetActive(false);
    }

    public void Show(int day, int incomeAmount)
    {
        // [JC 260617] 기획 변경 — 매 턴 시작 자금획득 안내 모달 비표시.
        //   자금 가산은 호출부(GameManager)에서 이미 처리됨. 여기선 턴 전환 시퀀스만 즉시 종료(월드 입력 차단 해제).
        WorldInputGate.IsTurnResolving = false;
    }

    public void Close()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
        // [JC 260615] 다음 턴 income 모달 확인 = 턴 전환 시퀀스 종료 → 월드 입력 차단 해제.
        WorldInputGate.IsTurnResolving = false;
    }
}
