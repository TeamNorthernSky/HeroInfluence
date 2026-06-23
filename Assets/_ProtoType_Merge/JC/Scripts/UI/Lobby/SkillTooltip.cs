using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260617] 연구소 스킬 전용 리치 툴팁(싱글턴). LobbyTooltip(단순 문자열)과 별개.
///  - TipB(1레벨): [스킬 아이콘][스킬 이름][스킬 설명] — UI_tooltip_B.
///  - TipC(2~5레벨): 좌[현재Lv 아이콘/이름 Lv.n/기존효과] · 우[다음Lv 아이콘/이름 Lv.n+1/변경효과] — UI_tooltip_C.
/// 위치: TipB는 대상 아이콘 중앙 + infoOffset(좌상단 모서리), TipC는 대상 아이콘 중앙 + compareOffset(상단-중앙).
/// </summary>
[DisallowMultipleComponent]
public class SkillTooltip : MonoBehaviour
{
    public static SkillTooltip Instance { get; private set; }

    [Header("TipB (1레벨: 아이콘/이름/설명)")]
    [SerializeField] private GameObject tipBRoot;
    [SerializeField] private Image tipBIcon;
    [SerializeField] private TMP_Text tipBName;
    [SerializeField] private TMP_Text tipBDesc;

    [Header("TipC (2~5레벨: 현재↔다음 비교)")]
    [SerializeField] private GameObject tipCRoot;
    [SerializeField] private Image tipCCurIcon;
    [SerializeField] private TMP_Text tipCCurName;
    [SerializeField] private TMP_Text tipCCurEffect;
    [SerializeField] private Image tipCNextIcon;
    [SerializeField] private TMP_Text tipCNextName;
    [SerializeField] private TMP_Text tipCNextEffect;

    [Header("동적 설명 패널 (9-slice, 텍스트 길이에 따라 아래로만 확장)")]
    [Tooltip("TipB 설명 배경(DescPanel9s) — pivot 상단(0.5,1) 권장")]
    [SerializeField] private RectTransform tipBDescPanel;
    [Tooltip("TipC 효과 비교 배경(DescPanel9s) — pivot 상단(0.5,1) 권장")]
    [SerializeField] private RectTransform tipCDescPanel;
    [SerializeField] private float descPadding = 16f;
    [SerializeField] private float descMinHeight = 60f;
    [Tooltip("라인 당 높이 증가폭 배율 — 텍스트가 상자 밖으로 삐져나올 때 키운다(기본 1.2)")]
    [SerializeField] private float descHeightScale = 1.2f;

    [Header("위치 오프셋")]
    [Tooltip("1레벨 툴팁: 아이콘 중앙 기준, 툴팁 좌상단 모서리 위치 오프셋(우+ 하-)")]
    [SerializeField] private Vector2 infoOffset = new Vector2(100f, -100f);
    [Tooltip("2~5레벨 툴팁: 아이콘 중앙 기준, 툴팁 상단-중앙 위치 오프셋(하-)")]
    [SerializeField] private Vector2 compareOffset = new Vector2(0f, -70f);

    private RectTransform tipBRect, tipCRect;
    // [JC 260617] 9-slice 패널 원본(디자인) 높이 — 동적 사이즈가 이보다 작아지지 않도록 최소 캡으로 사용.
    private float tipBDescBaseH, tipCDescBaseH;
    // [JC 260619] TipB 배경/본문 원본 너비 — ShowInfo의 extraWidth로 호출처별 가감(기본 0).
    private float tipBRootBaseW, tipBDescPanelBaseW, tipBDescBaseW;
    // [JC 260619] TipB 아이콘 원본 X — ShowInfo의 iconDeltaX로 호출처별 가감(기본 0).
    private float tipBIconBaseX;

    private bool promoted;

    private void Awake()
    {
        // [JC 260619] 영속 싱글턴화 — 씬 재진입 시 생기는 중복 인스턴스는 제거(첫 인스턴스가 영속 유지).
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsurePromoted();
        if (tipBRoot != null) tipBRect = tipBRoot.GetComponent<RectTransform>();
        if (tipCRoot != null) tipCRect = tipCRoot.GetComponent<RectTransform>();
        if (tipBRect != null) tipBRootBaseW = tipBRect.sizeDelta.x;
        if (tipBDescPanel != null) { tipBDescBaseH = tipBDescPanel.sizeDelta.y; tipBDescPanelBaseW = tipBDescPanel.sizeDelta.x; }
        if (tipBDesc != null) tipBDescBaseW = tipBDesc.rectTransform.sizeDelta.x;
        if (tipBIcon != null) tipBIconBaseX = tipBIcon.rectTransform.anchoredPosition.x;
        if (tipCDescPanel != null) tipCDescBaseH = tipCDescPanel.sizeDelta.y;
        Hide();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>[JC 260619] 전용 영속 오버레이 캔버스(최상단)로 이동해 로비·탐사 양 씬에서 공용으로 동작하게 한다.
    /// SkillTooltip이 HQLobbyScene 캔버스 하위에 중첩돼 있어, 씬/프리팹 구조 변경 없이 코드만으로 영속화한다.</summary>
    private void EnsurePromoted()
    {
        if (promoted) return;
        var canvasGO = new GameObject("PersistentTooltipCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // 모달(영속 100)·디버그캔버스 위
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);
        transform.SetParent(canvas.transform, false);
        promoted = true;
    }

    private static Vector3 Center(RectTransform target)
    {
        var c = new Vector3[4]; target.GetWorldCorners(c);
        return (c[0] + c[2]) * 0.5f;
    }

    /// <summary>설명 패널 높이를 텍스트 최대 높이에 맞춰 설정(pivot 상단이면 아래로만 확장).
    /// baseHeight = 패널 원본 높이(이보다 작아지지 않도록 캡). 측정 시 TMP margin(좌우=폭 차감, 상하=높이 가산) 반영.</summary>
    private void SizeDescPanel(RectTransform panel, float baseHeight, params TMP_Text[] texts)
    {
        if (panel == null) return;
        float maxH = 0f;
        foreach (var t in texts)
        {
            if (t == null) continue;
            var m = t.margin; // x=left, y=top, z=right, w=bottom
            float w = t.rectTransform.rect.width - m.x - m.z; if (w < 1f) w = 99999f;
            float h = t.GetPreferredValues(t.text, w, 100000f).y + m.y + m.w;
            if (h > maxH) maxH = h;
        }
        float floor = Mathf.Max(descMinHeight, baseHeight);
        var sd = panel.sizeDelta;
        panel.sizeDelta = new Vector2(sd.x, Mathf.Max(floor, maxH * descHeightScale + descPadding * 2f));
    }

    public void ShowInfo(Sprite icon, string skillName, string desc, RectTransform target, float extraY = 0f, float extraWidth = 0f, float iconDeltaX = 0f, float extraX = 0f)
    {
        Hide();
        if (tipBRoot == null || target == null) return;
        if (tipBIcon != null)
        {
            tipBIcon.sprite = icon; tipBIcon.enabled = icon != null;
            // [JC 260619] 호출처별 아이콘 X 가감(기본 0).
            tipBIcon.rectTransform.anchoredPosition = new Vector2(tipBIconBaseX + iconDeltaX, tipBIcon.rectTransform.anchoredPosition.y);
        }
        if (tipBName != null) tipBName.text = skillName;
        if (tipBDesc != null) { tipBDesc.gameObject.SetActive(true); tipBDesc.text = desc; }
        // [JC 260619] 호출처별 너비 가감(기본 0). 배경 프레임(TipB) + 본문 패널 + 텍스트 모두 적용. SizeDescPanel(높이)보다 먼저.
        if (tipBRect != null) tipBRect.sizeDelta = new Vector2(tipBRootBaseW + extraWidth, tipBRect.sizeDelta.y);
        if (tipBDescPanel != null) tipBDescPanel.sizeDelta = new Vector2(tipBDescPanelBaseW + extraWidth, tipBDescPanel.sizeDelta.y);
        if (tipBDesc != null) tipBDesc.rectTransform.sizeDelta = new Vector2(tipBDescBaseW + extraWidth, tipBDesc.rectTransform.sizeDelta.y);
        SizeDescPanel(tipBDescPanel, tipBDescBaseH, tipBDesc);
        tipBRoot.SetActive(true);
        transform.SetAsLastSibling();
        var ctr = Center(target);
        tipBRect.pivot = new Vector2(0f, 1f); // 좌상단 모서리
        // [JC 260619] extraY: 호출처별 추가 Y 오프셋(HeroInfo 모달은 +250로 위로 올림).
        // [JC 260622] extraX: 호출처별 추가 X 오프셋(전투 스킬버튼은 인스펙터 tooltipOffset으로 위치 조정).
        tipBRect.position = new Vector3(ctr.x + infoOffset.x + extraX, ctr.y + infoOffset.y + extraY, tipBRect.position.z);
    }

    public void ShowCompare(Sprite curIcon, string curName, string curEffect,
                            Sprite nextIcon, string nextName, string nextEffect, RectTransform target)
    {
        Hide();
        if (tipCRoot == null || target == null) return;
        if (tipCCurIcon != null) { tipCCurIcon.sprite = curIcon; tipCCurIcon.enabled = curIcon != null; }
        if (tipCNextIcon != null) { tipCNextIcon.sprite = nextIcon; tipCNextIcon.enabled = nextIcon != null; }
        if (tipCCurName != null) tipCCurName.text = curName;
        if (tipCNextName != null) tipCNextName.text = nextName;
        if (tipCCurEffect != null) tipCCurEffect.text = curEffect;
        if (tipCNextEffect != null) tipCNextEffect.text = nextEffect;
        SizeDescPanel(tipCDescPanel, tipCDescBaseH, tipCCurEffect, tipCNextEffect);
        tipCRoot.SetActive(true);
        transform.SetAsLastSibling();
        var ctr = Center(target);
        tipCRect.pivot = new Vector2(0.5f, 1f); // 상단-중앙
        tipCRect.position = new Vector3(ctr.x + compareOffset.x, ctr.y + compareOffset.y, tipCRect.position.z);
    }

    public void Hide()
    {
        if (tipBRoot != null) tipBRoot.SetActive(false);
        if (tipCRoot != null) tipCRoot.SetActive(false);
    }
}
