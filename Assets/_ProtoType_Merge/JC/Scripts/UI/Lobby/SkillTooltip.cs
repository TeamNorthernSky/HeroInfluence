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

    private void Awake()
    {
        Instance = this;
        if (tipBRoot != null) tipBRect = tipBRoot.GetComponent<RectTransform>();
        if (tipCRoot != null) tipCRect = tipCRoot.GetComponent<RectTransform>();
        if (tipBDescPanel != null) tipBDescBaseH = tipBDescPanel.sizeDelta.y;
        if (tipCDescPanel != null) tipCDescBaseH = tipCDescPanel.sizeDelta.y;
        Hide();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

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

    public void ShowInfo(Sprite icon, string skillName, string desc, RectTransform target)
    {
        Hide();
        if (tipBRoot == null || target == null) return;
        if (tipBIcon != null) { tipBIcon.sprite = icon; tipBIcon.enabled = icon != null; }
        if (tipBName != null) tipBName.text = skillName;
        if (tipBDesc != null) { tipBDesc.gameObject.SetActive(true); tipBDesc.text = desc; }
        SizeDescPanel(tipBDescPanel, tipBDescBaseH, tipBDesc);
        tipBRoot.SetActive(true);
        transform.SetAsLastSibling();
        var ctr = Center(target);
        tipBRect.pivot = new Vector2(0f, 1f); // 좌상단 모서리
        tipBRect.position = new Vector3(ctr.x + infoOffset.x, ctr.y + infoOffset.y, tipBRect.position.z);
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
