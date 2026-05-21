using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ModalHQDeptButtonStateBinder : MonoBehaviour
{
    [SerializeField] private HQDepartment department;
    [SerializeField] private TextMeshProUGUI captionLockText;
    [SerializeField] private HQUpgradeFlowController flowController;

    [Header("미활성(level=0) 상태 — 사용자 인스펙터 수정 가능")]
    [SerializeField] private string availableCaption = "사용 가능";
    [SerializeField] private Color availableColor = new Color(0.30f, 0.80f, 0.30f, 1f);
    [SerializeField] private FontStyles availableFontStyle = FontStyles.Bold;

    [Header("활성화 완료(level>=1) 상태")]
    [SerializeField] private string activatedCaption = "활성화 완료";
    [SerializeField] private Color activatedColor = new Color(0.50f, 0.50f, 0.50f, 1f);
    [SerializeField] private FontStyles activatedFontStyle = FontStyles.Bold;

    private Button button;
    private HQStateManager subscribedHQ;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (!button.interactable) return;
        if (flowController != null) flowController.ShowProgress(department);
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (subscribedHQ == null)
        {
            TrySubscribe();
            Refresh();
        }
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        if (subscribedHQ != null) return;
        subscribedHQ = gm.HQ;
        subscribedHQ.OnStateChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (subscribedHQ != null)
        {
            subscribedHQ.OnStateChanged -= Refresh;
            subscribedHQ = null;
        }
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        int level = gm.HQ.GetLevel(department);
        bool activated = level >= 1;

        if (button != null) button.interactable = !activated;

        if (captionLockText != null)
        {
            captionLockText.text = activated ? activatedCaption : availableCaption;
            captionLockText.color = activated ? activatedColor : availableColor;
            captionLockText.fontStyle = activated ? activatedFontStyle : availableFontStyle;
        }
    }
}
