using UnityEngine;
using UnityEngine.UI;

/// <summary>[JC 260630] 공유 TopBar의 옵션/툴팁 버튼 결선. 옵션→시스템메뉴, 툴팁→안내 모달 토글.</summary>
[DisallowMultipleComponent]
public class TopBarButtons : MonoBehaviour
{
    [SerializeField] private Button optionButton;     // BTN_Explor_Option
    [SerializeField] private Button tooltipButton;    // BTN_Explor_Tooltip
    [SerializeField] private GameObject tooltipNotice; // Modals 하위 안내 모달(Modal 부착, 비활성 시작)

    private void Awake()
    {
        if (optionButton != null) optionButton.onClick.AddListener(OnOption);
        if (tooltipButton != null) tooltipButton.onClick.AddListener(OnTooltip);
    }

    private void OnOption()
    {
        var sys = FindObjectOfType<SystemMenuController>(true);
        if (sys != null) sys.OpenMenu();
        else Debug.LogWarning("[TopBar] SystemMenuController 없음");
    }

    private void OnTooltip()
    {
        if (tooltipNotice == null) return;
        tooltipNotice.transform.SetAsLastSibling();
        tooltipNotice.SetActive(true); // 닫기: Modal 컴포넌트(ESC/외곽클릭)
    }
}
