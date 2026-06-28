using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260628] 본부(HQ) 건설/업그레이드 빌드 모드 중앙 코디네이터. 구 HQFacilityButtonController 대체.
/// 시설 목록을 SerializedField 대신 LobbyUIRegistry.Facilities에서 해석(번들 분리 대응).
/// 흐름: 본부 버튼 클릭 → 빌드 모드(가능 시설 강조+상호작용 ON) → 가능 placeholder 클릭 → ShowProgress → 모드 종료.
/// 노멀 모드에서 잠금 placeholder는 FacilityModule이 비상호작용으로 유지.
/// </summary>
[DisallowMultipleComponent]
public class HQBuildModeController : MonoBehaviour
{
    [Header("건설/업그레이드 팝업 흐름")]
    [SerializeField] private HQUpgradeFlowController unlockFlow;
    [Header("본부 — 빌드 모드 진입 버튼 (비우면 레지스트리)")]
    [SerializeField] private Button hqButton;
    [SerializeField] private GameObject buildModePrompt;
    [Header("강조 색")]
    [SerializeField] private Color highlightColor = new Color(0.35f, 1f, 0.45f, 1f);
    [Header("안내 프리팹 + 본부 문구")]
    [SerializeField] private GameObject constructInfoPrefab;
    [TextArea][SerializeField] private string hqUpgradeInfo = "본부를 업그레이드 합니다.";
    [TextArea][SerializeField] private string hqUpgradeDesc = "매 턴 얻는 자금이 증가합니다.";
    [TextArea][SerializeField] private string maxLevelMessage = "업그레이드가 최고 단계입니다.";
    [SerializeField] private Vector2 infoOffset = Vector2.zero;
    [Range(0f, 1f)][SerializeField] private float unbuildableAlpha = 0.3f;

    public bool IsBuildMode { get; private set; }

    private HQStateManager subscribedHQ;
    private readonly Dictionary<Graphic, Color> savedColors = new Dictionary<Graphic, Color>();
    private readonly List<GameObject> infoInstances = new List<GameObject>();

    private void OnEnable()
    {
        LobbyUIRegistry.BuildMode = this;
        if (hqButton == null) hqButton = LobbyUIRegistry.HqButton;
        if (hqButton != null) hqButton.onClick.AddListener(OnHQClick);
        if (buildModePrompt != null) buildModePrompt.SetActive(false);
        TrySubscribe();
    }

    private void OnDisable()
    {
        ExitBuildMode();
        if (hqButton != null) hqButton.onClick.RemoveListener(OnHQClick);
        if (subscribedHQ != null) { subscribedHQ.OnStateChanged -= OnHQState; subscribedHQ = null; }
        if (LobbyUIRegistry.BuildMode == this) LobbyUIRegistry.BuildMode = null;
    }

    private void Update()
    {
        if (subscribedHQ == null) TrySubscribe();
        if (hqButton == null) { hqButton = LobbyUIRegistry.HqButton; if (hqButton != null) hqButton.onClick.AddListener(OnHQClick); }
        if (IsBuildMode && Input.GetMouseButtonDown(0) && !IsPointerOverBuildTarget()) ExitBuildMode();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null || subscribedHQ != null) return;
        subscribedHQ = gm.HQ;
        subscribedHQ.OnStateChanged += OnHQState;
    }

    /// <summary>[JC 260629] unlockFlow가 비결선(UI_HQUpgrade 번들 분리)일 때 런타임 단일 인스턴스를 Find로 해석.</summary>
    private HQUpgradeFlowController ResolveFlow()
    {
        if (unlockFlow == null) unlockFlow = FindObjectOfType<HQUpgradeFlowController>(true);
        return unlockFlow;
    }

    private void OnHQState()
    {
        foreach (var f in LobbyUIRegistry.Facilities) if (f != null) f.Refresh();
        if (IsBuildMode) { RestoreHighlights(); ApplyHighlights(); SpawnInfos(); }
    }

    // ─── 클릭 라우팅 ───
    private void OnHQClick()
    {
        if (!IsBuildMode) { EnterBuildMode(); return; }
        bool eligible = IsUpgradeable(HQDepartment.Headquarters);
        ExitBuildMode();
        var flow = ResolveFlow();
        if (eligible && flow != null) flow.ShowProgress(HQDepartment.Headquarters);
    }

    /// <summary>FacilityModule이 잠금 placeholder 클릭 시 호출.</summary>
    public void NotifyLockedClicked(FacilityModule f)
    {
        if (f == null || !IsBuildMode) return;
        bool eligible = IsBuildable(f.Department);
        ExitBuildMode();
        var flow = ResolveFlow();
        if (eligible && flow != null) flow.ShowProgress(f.Department);
    }

    /// <summary>FacilityModule이 해금된 버튼 클릭 시 호출.</summary>
    public void NotifyUnlockedClicked(FacilityModule f)
    {
        if (f == null) return;
        if (IsBuildMode)
        {
            bool eligible = IsUpgradeable(f.Department);
            ExitBuildMode();
            var flow = ResolveFlow();
            if (eligible && flow != null) flow.ShowProgress(f.Department);
            return;
        }
        if (f.FacilityModal != null) f.FacilityModal.SetActive(true);
    }

    // ─── 빌드 모드 ───
    private void EnterBuildMode()
    {
        IsBuildMode = true;
        if (buildModePrompt != null) buildModePrompt.SetActive(true);
        foreach (var f in LobbyUIRegistry.Facilities)
        {
            if (f == null) continue;
            bool unlocked = GetLevel(f.Department) >= 1;
            bool eligible = unlocked ? IsUpgradeable(f.Department) : IsBuildable(f.Department);
            if (!unlocked) f.SetLockedInteractive(eligible); // 가능한 잠금 시설만 상호작용 ON
        }
        ApplyHighlights();
        SpawnInfos();
    }

    private void ExitBuildMode()
    {
        if (!IsBuildMode) return;
        IsBuildMode = false;
        if (buildModePrompt != null) buildModePrompt.SetActive(false);
        foreach (var f in LobbyUIRegistry.Facilities) if (f != null) f.SetLockedInteractive(false);
        RestoreHighlights();
        ClearInfos();
    }

    private bool IsPointerOverBuildTarget()
    {
        if (RectContainsPointer(hqButton)) return true;
        foreach (var f in LobbyUIRegistry.Facilities)
        {
            if (f == null) continue;
            bool unlocked = GetLevel(f.Department) >= 1;
            Button active = unlocked ? f.UnlockedButton : f.LockedButton;
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
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, cam);
    }

    // ─── 강조 ───
    private void ApplyHighlights()
    {
        Highlight(hqButton, IsUpgradeable(HQDepartment.Headquarters));
        foreach (var f in LobbyUIRegistry.Facilities)
        {
            if (f == null) continue;
            bool unlocked = GetLevel(f.Department) >= 1;
            Button active = unlocked ? f.UnlockedButton : f.LockedButton;
            bool eligible = unlocked ? IsUpgradeable(f.Department) : IsBuildable(f.Department);
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
        foreach (var kv in savedColors) if (kv.Key != null) kv.Key.color = kv.Value;
        savedColors.Clear();
    }

    // ─── 안내 프리팹 ───
    private void SpawnInfos()
    {
        ClearInfos();
        if (constructInfoPrefab == null) return;
        SpawnInfo(hqButton, ResolveHqText(), false);
        foreach (var f in LobbyUIRegistry.Facilities)
        {
            if (f == null) continue;
            int lv = GetLevel(f.Department);
            bool unlocked = lv >= 1;
            Button pos = unlocked ? f.UnlockedButton : f.LockedButton;
            bool dim = lv == 0 && !IsBuildable(f.Department);
            SpawnInfo(pos, f.ResolveText(maxLevelMessage), dim);
        }
    }
    private (string, string, string) ResolveHqText()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return (maxLevelMessage, "", ""); // [JC 260629] null 가드(타 헬퍼와 정합)
        int lv = gm.HQ.GetLevel(HQDepartment.Headquarters);
        int max = gm.HQ.GetMaxLevel(HQDepartment.Headquarters);
        return lv >= max ? (maxLevelMessage, "", "") : (hqUpgradeInfo, hqUpgradeDesc, "");
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
        var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        cg.alpha = dim ? unbuildableAlpha : 1f;
        infoInstances.Add(go);
    }
    private static void SetTmp(Transform root, string childName, string text, bool active)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            if (tr.name == childName) { var tmp = tr.GetComponent<TMP_Text>(); if (tmp != null) tmp.text = text ?? ""; tr.gameObject.SetActive(active); return; }
    }
    private void ClearInfos()
    {
        for (int i = infoInstances.Count - 1; i >= 0; i--) if (infoInstances[i] != null) Destroy(infoInstances[i]);
        infoInstances.Clear();
    }

    // ─── 가능 여부 / 헬퍼 ───
    private static int GetLevel(HQDepartment d) { var gm = GameManager.Instance; return (gm != null && gm.HQ != null) ? gm.HQ.GetLevel(d) : 0; }
    public static bool IsBuildable(HQDepartment d) { var gm = GameManager.Instance; return gm != null && gm.HQ != null && gm.HQ.GetLevel(d) == 0 && gm.HQ.ArePrerequisitesMet(d); }
    public static bool IsUpgradeable(HQDepartment d) { var gm = GameManager.Instance; if (gm == null || gm.HQ == null) return false; int lv = gm.HQ.GetLevel(d); return lv >= 1 && lv < gm.HQ.GetMaxLevel(d); }
}
