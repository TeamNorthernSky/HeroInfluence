using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ModalHQDeptButtonStateBinder : MonoBehaviour
{
    [SerializeField] private HQDepartment department;
    [SerializeField] private TextMeshProUGUI captionLockText;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private HQUpgradeFlowController flowController;

    [Header("LV 표시 (해금 후만 표시)")]
    [SerializeField] private GameObject lvBgRoot;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("available (level=0, 선행조건 충족, 미사용)")]
    [SerializeField] private string availableCaption = "건설";
    [SerializeField] private Color availableColor = new Color(0.30f, 0.80f, 0.30f, 1f);
    [SerializeField] private FontStyles availableFontStyle = FontStyles.Bold;

    [Header("upgradable (1≤level<max, 미사용)")]
    [SerializeField] private string upgradableCaption = "업그레이드";
    [SerializeField] private Color upgradableColor = new Color(0.30f, 0.80f, 0.30f, 1f);
    [SerializeField] private FontStyles upgradableFontStyle = FontStyles.Bold;

    [Header("turnUsed (이번 턴 1회 사용 완료)")]
    [SerializeField] private string turnUsedCaption = "다음 턴에 사용 가능";
    [SerializeField] private Color turnUsedColor = new Color(0.85f, 0.55f, 0.15f, 1f);
    [SerializeField] private FontStyles turnUsedFontStyle = FontStyles.Bold;

    [Header("blocked (선행 조건 미충족, 캡션은 동적)")]
    [SerializeField] private Color blockedColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    [SerializeField] private FontStyles blockedFontStyle = FontStyles.Bold;

    [Header("maxed (level≥maxLevel)")]
    [SerializeField] private string maxedCaption = "최고 단계";
    [SerializeField] private Color maxedColor = new Color(0.50f, 0.50f, 0.50f, 1f);
    [SerializeField] private FontStyles maxedFontStyle = FontStyles.Bold;

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
        int maxLevel = gm.HQ.GetMaxLevel(department);
        bool unlocked = level >= 1;
        bool maxed = level >= maxLevel;
        bool prereqMet = gm.HQ.ArePrerequisitesMet(department);
        bool turnUsed = gm.HQ.UpgradedThisTurn;

        // LV 표시
        if (lvBgRoot != null) lvBgRoot.SetActive(unlocked);
        if (levelText != null && unlocked) levelText.text = $"Lv. {level}";

        // Label — content의 progressTitle / progressTitleUpgrade 사용
        if (labelText != null && flowController != null)
        {
            var content = flowController.GetContent(department);
            if (content != null)
            {
                string lbl;
                if (unlocked)
                    lbl = !string.IsNullOrEmpty(content.progressTitleUpgrade) ? content.progressTitleUpgrade : content.progressTitle;
                else
                    lbl = content.progressTitle;
                labelText.text = lbl;
            }
        }

        // 5상태 분기
        string caption;
        Color color;
        FontStyles style;
        bool interactable;

        if (!unlocked && !prereqMet)
        {
            caption = gm.HQ.GetUnmetReasonText(department);
            if (string.IsNullOrEmpty(caption)) caption = availableCaption;
            color = blockedColor;
            style = blockedFontStyle;
            interactable = false;
        }
        else if (maxed)
        {
            caption = maxedCaption;
            color = maxedColor;
            style = maxedFontStyle;
            interactable = false;
        }
        else if (turnUsed)
        {
            caption = turnUsedCaption;
            color = turnUsedColor;
            style = turnUsedFontStyle;
            // 이 분기 도달 시점에 prereqMet 이미 보장됨 (blocked 분기에서 미충족은 걸러짐).
            // 미해금이지만 해금 가능한 부서도 클릭 → Progress 모달에서 사유 안내.
            interactable = true;
        }
        else if (!unlocked)
        {
            caption = availableCaption;
            color = availableColor;
            style = availableFontStyle;
            interactable = true;
        }
        else
        {
            caption = upgradableCaption;
            color = upgradableColor;
            style = upgradableFontStyle;
            interactable = true;
        }

        if (button != null) button.interactable = interactable;
        if (captionLockText != null)
        {
            captionLockText.text = caption;
            captionLockText.color = color;
            captionLockText.fontStyle = style;
        }
    }
}
