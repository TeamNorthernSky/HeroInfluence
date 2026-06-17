using System;
using System.Collections.Generic;
using TMPro;
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

        [Header("안내 문구 (건설/업그레이드 텍스트 오버레이)")]
        [TextArea] public string buildInfo;       // "○○를 건설합니다."
        [TextArea] public string buildDesc;        // 효과 설명
        [TextArea] public string buildCondition;   // "필요 조건: ..." (비우면 미표시)
        [TextArea] public string upgradeInfo;      // "○○를 업그레이드 합니다." (비우면 업그레이드 상태에서 미표시)
        [TextArea] public string upgradeDesc;      // 업그레이드 효과 설명
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

    [Header("안내 프리팹 (ConstructInfoText) + 본부/공통 문구")]
    [Tooltip("ConstructInfoText.prefab — 내부 Panel/Info·Describtion·Preconditions(TMP)")]
    [SerializeField] private GameObject constructInfoPrefab;
    [TextArea] [SerializeField] private string hqUpgradeInfo = "본부를 업그레이드 합니다.";
    [TextArea] [SerializeField] private string hqUpgradeDesc = "매 턴 얻는 자금이 증가합니다.";
    [TextArea] [SerializeField] private string maxLevelMessage = "업그레이드가 최고 단계입니다.";
    [Tooltip("각 위치 버튼 기준 안내 프리팹 오프셋")]
    [SerializeField] private Vector2 infoOffset = Vector2.zero;
    [Tooltip("건설 불가(선행조건 미충족) 위치 안내의 알파(패널+텍스트 일괄). 0.3 ≈ 70% 투명")]
    [Range(0f, 1f)] [SerializeField] private float unbuildableAlpha = 0.3f;

    private HQStateManager subscribedHQ;
    private bool buildMode;
    private readonly Dictionary<Graphic, Color> savedColors = new Dictionary<Graphic, Color>();
    private readonly List<GameObject> infoInstances = new List<GameObject>();

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
        SpawnInfos();
    }

    private void ExitBuildMode()
    {
        if (!buildMode) return;
        buildMode = false;
        if (buildModePrompt != null) buildModePrompt.SetActive(false);
        RestoreHighlights();
        ClearInfos();
    }

    // ─── 건설/업그레이드 안내 프리팹 ───────────────────────────
    private void SpawnInfos()
    {
        ClearInfos();
        if (constructInfoPrefab == null) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;

        // 본부(건설 없음, 업그레이드/최고단계만 — 항상 건설 가능 영역 아님 → dim 없음)
        SpawnInfo(hqButton, ResolveText(HQDepartment.Headquarters, null), false);

        for (int i = 0; i < facilities.Count; i++)
        {
            FacilityEntry f = facilities[i];
            if (f == null) continue;
            int lv = gm.HQ.GetLevel(f.department);
            bool unlocked = lv >= 1;
            Button pos = unlocked ? f.unlockedButton : f.lockedButton;
            bool dim = lv == 0 && !IsBuildable(f.department); // 건설 불가(선행조건 미충족) → 흐리게
            SpawnInfo(pos, ResolveText(f.department, f), dim);
        }
    }

    /// <summary>위치 상태(level)에 따라 (info, desc, condition) 결정.</summary>
    private (string info, string desc, string cond) ResolveText(HQDepartment d, FacilityEntry f)
    {
        var gm = GameManager.Instance;
        int lv = gm.HQ.GetLevel(d);
        int max = gm.HQ.GetMaxLevel(d);

        if (d == HQDepartment.Headquarters)
            return lv >= max ? (maxLevelMessage, "", "") : (hqUpgradeInfo, hqUpgradeDesc, "");

        if (f == null) return ("", "", "");
        if (lv == 0) return (f.buildInfo, f.buildDesc, f.buildCondition);          // 건설
        if (lv >= max) return (maxLevelMessage, "", "");                            // 최고단계
        return (f.upgradeInfo, f.upgradeDesc, "");                                  // 업그레이드
    }

    private void SpawnInfo(Button posBtn, (string info, string desc, string cond) t, bool dim)
    {
        if (posBtn == null || constructInfoPrefab == null) return;
        GameObject go = Instantiate(constructInfoPrefab, posBtn.transform);
        var rt = go.transform as RectTransform;
        if (rt != null) rt.anchoredPosition += infoOffset;
        go.transform.SetAsLastSibling();

        SetTmp(go.transform, "Info", t.info, true);
        SetTmp(go.transform, "Describtion", t.desc, true);
        SetTmp(go.transform, "Preconditions", t.cond, !string.IsNullOrWhiteSpace(t.cond));

        // 건설 불가 위치 → 패널+텍스트 일괄 흐리게(CanvasGroup, 기존 패널 알파 위에 곱연산)
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = dim ? unbuildableAlpha : 1f;

        infoInstances.Add(go);
    }

    private static void SetTmp(Transform root, string childName, string text, bool active)
    {
        Transform t = FindDeep(root, childName);
        if (t == null) return;
        var tmp = t.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = text ?? "";
        t.gameObject.SetActive(active);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private void ClearInfos()
    {
        for (int i = infoInstances.Count - 1; i >= 0; i--)
            if (infoInstances[i] != null) Destroy(infoInstances[i]);
        infoInstances.Clear();
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
        if (buildMode) { RestoreHighlights(); ApplyHighlights(); SpawnInfos(); }
    }
}
