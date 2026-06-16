using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HQLobbyScene 협회 건물 상호작용 디스패처. (기획서 협회(본부,출전) ②, V3.0)
///
/// 상호작용 모델:
///   - 본부(HQ) 클릭 → 건설/업그레이드 모드 진입: 건설 가능(미건설+선행조건 충족) 또는
///     업그레이드 가능(건설됨+미만레벨) 공간을 강조 + 안내 표시. 강조된 공간 클릭 시
///     해당 건설/업그레이드 팝업(HQUpgradeFlowController.ShowProgress) → 모드 종료.
///   - (build mode) 본부 재클릭 → 본부 업그레이드(가능 시) 후 모드 종료.
///   - (normal mode) 건설된 방 클릭 → 방 기능 팝업(facilityModal).
///   - (normal mode) 미건설 placeholder 클릭 → 무동작(건설은 본부 모드 경유).
///
/// HQ.OnStateChanged 구독으로 해금 즉시 _cut↔non-cut 버튼 교체. 턴당 1회 제한·비용·선행조건
/// 최종 게이트는 HQUpgradeFlowController/HQStateManager가 보장(강조는 구조적 가능성만 표시).
/// </summary>
[DisallowMultipleComponent]
public class HQFacilityButtonController : MonoBehaviour
{
    [Serializable]
    public class FacilityEntry
    {
        public HQDepartment department;
        [Tooltip("미건설 시 표시되는 플레이스홀더 버튼(_cut)")]
        public Button lockedButton;
        [Tooltip("건설 후 표시되는 시설 버튼(non-cut)")]
        public Button unlockedButton;
        [Tooltip("건설 후 버튼 클릭 시 열 방 기능 모달")]
        public GameObject facilityModal;
    }

    [Header("건설/업그레이드 팝업 흐름")]
    [SerializeField] private HQUpgradeFlowController unlockFlow;

    [Header("본부 — 건설/업그레이드 모드 진입 버튼")]
    [SerializeField] private Button hqButton;
    [Tooltip("건설/업그레이드 모드 진입 시 표시할 안내(\"건설 또는 업그레이드할 공간을 선택하세요\")")]
    [SerializeField] private GameObject buildModePrompt;

    [Header("시설 6종 (본부 제외)")]
    [SerializeField] private List<FacilityEntry> facilities = new List<FacilityEntry>();

    [Header("건설/업그레이드 가능 강조 색 (테두리 아트 적용 전 임시 틴트)")]
    [SerializeField] private Color highlightColor = new Color(0.35f, 1f, 0.45f, 1f);

    private HQStateManager subscribedHQ;
    private bool buildMode;
    private readonly Dictionary<Graphic, Color> savedColors = new Dictionary<Graphic, Color>();

    private void Awake()
    {
        for (int i = 0; i < facilities.Count; i++)
        {
            FacilityEntry f = facilities[i];
            if (f == null) continue;
            FacilityEntry captured = f;
            if (f.lockedButton != null)
                f.lockedButton.onClick.AddListener(() => OnFacilityClick(captured, true));
            if (f.unlockedButton != null)
                f.unlockedButton.onClick.AddListener(() => OnFacilityClick(captured, false));
        }

        if (hqButton != null) hqButton.onClick.AddListener(OnHQClick);
        if (buildModePrompt != null) buildModePrompt.SetActive(false);
    }

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        ExitBuildMode();
        if (subscribedHQ != null) { subscribedHQ.OnStateChanged -= Refresh; subscribedHQ = null; }
    }

    private void Update()
    {
        if (subscribedHQ == null) TrySubscribe();

        // 빌드 모드에서 건설/업그레이드 대상(본부·시설 버튼) 밖을 클릭하면 모드 종료
        if (buildMode && Input.GetMouseButtonDown(0) && !IsPointerOverBuildTarget())
            ExitBuildMode();
    }

    private bool IsPointerOverBuildTarget()
    {
        if (RectContainsPointer(hqButton)) return true;
        var gm = GameManager.Instance;
        for (int i = 0; i < facilities.Count; i++)
        {
            FacilityEntry f = facilities[i];
            if (f == null) continue;
            bool unlocked = gm != null && gm.HQ != null && gm.HQ.GetLevel(f.department) >= 1;
            Button active = unlocked ? f.unlockedButton : f.lockedButton;
            if (RectContainsPointer(active)) return true;
        }
        return false;
    }

    private static bool RectContainsPointer(Button btn)
    {
        if (btn == null || !btn.gameObject.activeInHierarchy) return false;
        var rt = btn.transform as RectTransform;
        if (rt == null) return false;
        var canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, cam);
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

    // ─── 클릭 라우팅 ───────────────────────────────────────────
    private void OnHQClick()
    {
        if (!buildMode)
        {
            EnterBuildMode();
            return;
        }
        // 빌드 모드에서 본부 재클릭 → 본부 업그레이드(가능 시) 후 모드 종료
        bool eligible = IsUpgradeable(HQDepartment.Headquarters);
        ExitBuildMode();
        if (eligible && unlockFlow != null) unlockFlow.ShowProgress(HQDepartment.Headquarters);
    }

    private void OnFacilityClick(FacilityEntry f, bool isLockedButton)
    {
        if (f == null) return;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;

        if (buildMode)
        {
            bool eligible = isLockedButton ? IsBuildable(f.department) : IsUpgradeable(f.department);
            ExitBuildMode();
            if (eligible && unlockFlow != null) unlockFlow.ShowProgress(f.department);
            return;
        }

        // normal 모드: 건설된 방 클릭 → 기능 팝업. 미건설 placeholder → 무동작.
        if (!isLockedButton && f.facilityModal != null)
            f.facilityModal.SetActive(true);
    }

    // ─── 건설/업그레이드 모드 ──────────────────────────────────
    private void EnterBuildMode()
    {
        buildMode = true;
        if (buildModePrompt != null) buildModePrompt.SetActive(true);
        ApplyHighlights();
    }

    private void ExitBuildMode()
    {
        if (!buildMode) return;
        buildMode = false;
        if (buildModePrompt != null) buildModePrompt.SetActive(false);
        RestoreHighlights();
    }

    private void ApplyHighlights()
    {
        Highlight(hqButton, IsUpgradeable(HQDepartment.Headquarters));
        var gm = GameManager.Instance;
        for (int i = 0; i < facilities.Count; i++)
        {
            FacilityEntry f = facilities[i];
            if (f == null) continue;
            bool unlocked = gm != null && gm.HQ != null && gm.HQ.GetLevel(f.department) >= 1;
            Button active = unlocked ? f.unlockedButton : f.lockedButton;
            bool eligible = unlocked ? IsUpgradeable(f.department) : IsBuildable(f.department);
            Highlight(active, eligible);
        }
    }

    private void Highlight(Button btn, bool eligible)
    {
        if (btn == null || !eligible) return;
        Graphic g = btn.targetGraphic != null ? btn.targetGraphic : btn.GetComponent<Graphic>();
        if (g == null) return;
        if (!savedColors.ContainsKey(g)) savedColors[g] = g.color;
        g.color = highlightColor;
    }

    private void RestoreHighlights()
    {
        foreach (var kv in savedColors)
            if (kv.Key != null) kv.Key.color = kv.Value;
        savedColors.Clear();
    }

    // ─── 가능 여부 ─────────────────────────────────────────────
    private bool IsBuildable(HQDepartment d)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return false;
        return gm.HQ.GetLevel(d) == 0 && gm.HQ.ArePrerequisitesMet(d);
    }

    private bool IsUpgradeable(HQDepartment d)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return false;
        int lv = gm.HQ.GetLevel(d);
        return lv >= 1 && lv < gm.HQ.GetMaxLevel(d);
    }

    // ─── 버튼 교체(해금 상태 반영) ─────────────────────────────
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
        if (buildMode) { RestoreHighlights(); ApplyHighlights(); }
    }
}
