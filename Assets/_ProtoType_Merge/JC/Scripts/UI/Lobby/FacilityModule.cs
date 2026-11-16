using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260628] 시설 번들 1개를 대표. 자기 버튼/모달/문구를 보유하고 LobbyUIRegistry에 등록.
/// - cut↔non-cut 스왑(HQ 레벨 반영).
/// - 노멀 모드: 미건설 시설 클릭 시 해당 부서의 건설 패널을 연다.
/// - 빌드 모드: 선행조건을 충족한 미건설 시설만 상호작용한다.
/// </summary>
[DisallowMultipleComponent]
public class FacilityModule : MonoBehaviour
{
    [SerializeField] private HQDepartment department;
    [SerializeField] private Button lockedButton;
    [SerializeField] private Button unlockedButton;
    [SerializeField] private GameObject facilityModal;
    [Tooltip("본부 건설 모드에서만 사용할 망치 아이콘 없는 빈 방 이미지")]
    [SerializeField] private Sprite lockedBuildModeSprite;

    [Header("안내 문구")]
    [TextArea] public string buildDesc;
    [TextArea] public string buildCondition;
    [TextArea] public string upgradeDesc;

    [Header("잠금 placeholder 비상호작용 토글 대상 (호버 컴포넌트들)")]
    [Tooltip("예: ButtonEffectController, lockedButton의 BaseHoverOverlay/SweepCooldownReset 등")]
    [SerializeField] private Behaviour[] lockedHoverComponents;

    public HQDepartment Department => department;
    public Button LockedButton => lockedButton;
    public Button UnlockedButton => unlockedButton;
    public GameObject FacilityModal => facilityModal;

    private void Awake()
    {
        if (lockedButton != null) lockedButton.onClick.AddListener(() => { var bm = LobbyUIRegistry.BuildMode; if (bm != null) bm.NotifyLockedClicked(this); });
        if (unlockedButton != null) unlockedButton.onClick.AddListener(() => { var bm = LobbyUIRegistry.BuildMode; if (bm != null) bm.NotifyUnlockedClicked(this); else if (facilityModal != null) facilityModal.SetActive(true); });
    }

    private void OnEnable()
    {
        LobbyUIRegistry.RegisterFacility(this);
        SetLockedInteractive(true);
        Refresh();
        facilityModal.SetActive(false);
    }

    private void OnDisable()
    {
        SetBuildModeVisual(false);
        LobbyUIRegistry.UnregisterFacility(this);
    }

    /// <summary>HQ 레벨에 따라 cut↔non-cut 버튼 가시성 스왑.</summary>
    public void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        bool unlocked = gm.HQ.GetLevel(department) >= 1;
        if (lockedButton != null) lockedButton.gameObject.SetActive(!unlocked);
        if (unlockedButton != null) unlockedButton.gameObject.SetActive(unlocked);
        var buildMode = LobbyUIRegistry.BuildMode;
        SetBuildModeVisual(buildMode != null && buildMode.IsBuildMode);
        SetLockedInteractive(buildMode == null || !buildMode.IsBuildMode || HQBuildModeController.IsBuildable(department));
    }

    public void SetBuildModeVisual(bool on)
    {
        if (lockedButton != null && lockedButton.image != null)
            lockedButton.image.overrideSprite = on ? lockedBuildModeSprite : null;
    }

    /// <summary>미건설 시설의 상호작용(호버·클릭) ON/OFF. 노멀 모드 기본 true.</summary>
    public void SetLockedInteractive(bool on)
    {
        if (lockedButton != null) lockedButton.interactable = on;
        if (lockedHoverComponents != null)
            foreach (var b in lockedHoverComponents) if (b != null) b.enabled = on;
    }

    /// <summary>현재 레벨에 따른 (info, desc, cond).</summary>
    public (string info, string desc, string cond) ResolveText(string maxMsg)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return ("", "", "");
        int lv = gm.HQ.GetLevel(department);
        int max = gm.HQ.GetMaxLevel(department);
        if (lv == 0) return ("", buildDesc, buildCondition);
        if (lv >= max) return (maxMsg, "", "");
        return ("", upgradeDesc, "");
    }
}
