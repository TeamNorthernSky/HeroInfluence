using TMPro;
using UnityEngine;

/// <summary>
/// [JC 260617] HQLobbyScene 전용 공유 롤오버 툴팁. KJ HoverTooltip(전투용)과 별개.
/// 씬에 단일 인스턴스로 배치하고, 각 항목의 <see cref="LobbyTooltipTrigger"/>가 hover 시 Show/Hide를 호출한다.
/// 배경 스프라이트는 UI_tooltip_A(9-slice, 좌하단 말풍선 꼬리). 로비 내 다른 곳에서도 재사용 가능.
///
/// 위치/사이즈 규칙:
///  - 피봇 = 좌하단(0,0). 꼬리 끝(tailTip, 좌하단 기준 px)을 "대상 중앙X, 대상 상단경계Y + topGap"에 맞춘다.
///  - 가로: 글자 수에 따라 동적. width = max(minWidth, textW + 2*(fontSize*fontMarginMul + borderMargin)).
///    피봇이 좌측이라 우측으로만 확장 → 꼬리 정렬 유지.
///  - 세로: height = max(minHeight, textH + 2*verticalMargin). 피봇이 하단이라 위쪽으로만 확장.
/// </summary>
[DisallowMultipleComponent]
public class LobbyTooltip : MonoBehaviour
{
    public static LobbyTooltip Instance { get; private set; }

    [Tooltip("툴팁 패널 루트(보이기/숨기기 토글). 배경 Image(UI_tooltip_A 9-slice)를 가진 오브젝트.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("툴팁 본문 텍스트")]
    [SerializeField] private TMP_Text bodyText;

    [Header("위치 (좌하단 0,0 피봇 기준)")]
    [Tooltip("말풍선 꼬리 끝의 픽셀 오프셋(좌하단 기준). 이 점을 대상 중앙X·상단+topGap에 맞춘다.")]
    [SerializeField] private Vector2 tailTip = new Vector2(55f, 0f);
    [Tooltip("대상 상단 경계 위로 꼬리 끝을 띄울 거리(px)")]
    [SerializeField] private float topGap = 10f;

    [Header("동적 사이즈")]
    [Tooltip("가로 마진 = fontSize * 이 값 + borderMargin (각 변)")]
    [SerializeField] private float fontMarginMul = 1.5f;
    [Tooltip("가로 테두리 마진(각 변, px)")]
    [SerializeField] private float borderMargin = 34f;
    [Tooltip("세로 마진(상/하 각각 고정, px) — 패널 높이 산정용")]
    [SerializeField] private float verticalMargin = 40f;
    [Tooltip("하단 말풍선 꼬리 높이(px). 이 영역을 제외한 나머지에서 텍스트를 세로 중앙 정렬")]
    [SerializeField] private float tailHeight = 28f;
    [Tooltip("9-slice 최소 폭(L+R 보더). 이보다 좁으면 프레임이 깨짐")]
    [SerializeField] private float minWidth = 246f;
    [Tooltip("9-slice 최소 높이(B+T 보더)")]
    [SerializeField] private float minHeight = 147f;

    private RectTransform panelRect;

    private void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRect = panelRoot.GetComponent<RectTransform>();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>지정 텍스트로 대상 항목 위에 툴팁 표시. 빈 문자열이면 숨김.</summary>
    public void Show(string content, RectTransform target)
    {
        if (panelRoot == null) return;
        if (string.IsNullOrEmpty(content)) { Hide(); return; }
        if (bodyText != null) bodyText.text = content;
        panelRoot.SetActive(true);
        transform.SetAsLastSibling(); // 항상 최상단
        if (panelRect == null || target == null) return;

        // ── 동적 사이즈 (줄바꿈 없음) ─────────────────────────────
        float w = minWidth, h = minHeight;
        if (bodyText != null)
        {
            Vector2 pref = bodyText.GetPreferredValues(content, Mathf.Infinity, 0f); // 폭 무제한 → 강제 줄바꿈 방지(1줄)
            float hMargin = bodyText.fontSize * fontMarginMul + borderMargin;
            w = Mathf.Max(minWidth, pref.x + 2f * hMargin + 2f); // +2 안전여유
            h = Mathf.Max(minHeight, pref.y + 2f * verticalMargin);

            // 좌우는 hMargin 패딩, 세로는 하단 꼬리(tailHeight)를 제외한 영역에서 중앙 정렬
            var trt = bodyText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(hMargin, tailHeight); // 하단 = 꼬리 높이만큼 제외
            trt.offsetMax = new Vector2(-hMargin, 0f);          // 상단 = 패널 상단까지
            bodyText.verticalAlignment = TMPro.VerticalAlignmentOptions.Middle; // 가로 정렬은 유지
        }
        panelRect.pivot = Vector2.zero;          // 좌하단 → 우/상으로만 확장
        panelRect.sizeDelta = new Vector2(w, h);

        // ── 위치: 꼬리 끝 = (대상 중앙X, 대상 상단 + topGap) ───────
        var corners = new Vector3[4];
        target.GetWorldCorners(corners); // 0=BL,1=TL,2=TR,3=BR (Overlay=스크린px)
        float centerX = (corners[0].x + corners[2].x) * 0.5f;
        float topY = corners[1].y;
        panelRect.position = new Vector3(centerX - tailTip.x, topY + topGap - tailTip.y, panelRect.position.z);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
