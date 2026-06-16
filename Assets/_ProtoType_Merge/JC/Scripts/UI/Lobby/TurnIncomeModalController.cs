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
        if (modalRoot == null) return;
        if (titleText != null) titleText.text = string.Format(titleFormat, day);
        if (incomeText != null) incomeText.text = string.Format(incomeFormat, incomeAmount.ToString("N0"));
        modalRoot.SetActive(true);
    }

    public void Close()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
        // [JC 260615] 다음 턴 income 모달 확인 = 턴 전환 시퀀스 종료 → 월드 입력 차단 해제.
        WorldInputGate.IsTurnResolving = false;
    }
}
