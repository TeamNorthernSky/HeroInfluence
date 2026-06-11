using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HQLobbyScene의 시설 프레임 버튼(본부 제외 6종) 해금 ↔ 진입 전환 디스패처.
/// - 미해금(HQ.GetLevel(dept)==0): _cut(잠금/플레이스홀더) 버튼만 표시 → 클릭 시 해금 모달
///   (HQUpgradeFlowController.ShowProgress, 본부 업그레이드 모달 레이아웃 재사용).
/// - 해금(level>=1): non-cut(해금) 버튼만 표시 → 클릭 시 시설 모달(있으면. 현재 교환소만).
/// HQ.OnStateChanged 구독해 해금 즉시 버튼 교체. 턴당 1회 제한은 HQStateManager가 보장.
/// </summary>
[DisallowMultipleComponent]
public class HQFacilityButtonController : MonoBehaviour
{
    [Serializable]
    public class FacilityEntry
    {
        public HQDepartment department;
        [Tooltip("미해금 시 표시되는 플레이스홀더 버튼(_cut)")]
        public Button lockedButton;
        [Tooltip("해금 후 표시되는 시설 버튼(non-cut)")]
        public Button unlockedButton;
        [Tooltip("해금 후 버튼 클릭 시 열 시설 모달(선택 — 현재 교환소만)")]
        public GameObject facilityModal;
    }

    [Header("해금 모달 흐름(본부 업그레이드 모달 재사용)")]
    [SerializeField] private HQUpgradeFlowController unlockFlow;

    [Header("시설 6종 (본부 제외)")]
    [SerializeField] private List<FacilityEntry> facilities = new List<FacilityEntry>();

    private HQStateManager subscribedHQ;

    private void Awake()
    {
        for (int i = 0; i < facilities.Count; i++)
        {
            FacilityEntry f = facilities[i];
            if (f == null) continue;
            FacilityEntry captured = f;
            if (f.lockedButton != null)
                f.lockedButton.onClick.AddListener(() => OnLockedClick(captured));
            if (f.unlockedButton != null)
                f.unlockedButton.onClick.AddListener(() => OnUnlockedClick(captured));
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        if (subscribedHQ != null) { subscribedHQ.OnStateChanged -= Refresh; subscribedHQ = null; }
    }

    private void Update()
    {
        if (subscribedHQ == null) TrySubscribe();
    }

    private void TrySubscribe()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        if (subscribedHQ == null)
        {
            subscribedHQ = gm.HQ;
            subscribedHQ.OnStateChanged += Refresh;
            Refresh();
        }
    }

    private void Refresh()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        for (int i = 0; i < facilities.Count; i++)
        {
            FacilityEntry f = facilities[i];
            if (f == null) continue;
            bool unlocked = gm.HQ.GetLevel(f.department) >= 1;
            if (f.lockedButton != null) f.lockedButton.gameObject.SetActive(!unlocked);
            if (f.unlockedButton != null) f.unlockedButton.gameObject.SetActive(unlocked);
        }
    }

    private void OnLockedClick(FacilityEntry f)
    {
        if (f == null || unlockFlow == null) return;
        unlockFlow.ShowProgress(f.department); // 해금 모달(선행/비용 확인 → 진행)
    }

    private void OnUnlockedClick(FacilityEntry f)
    {
        if (f == null) return;
        if (f.facilityModal != null) f.facilityModal.SetActive(true);
    }
}
