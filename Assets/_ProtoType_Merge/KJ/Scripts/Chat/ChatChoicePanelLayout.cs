using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260729] 선택지 영역의 동적 높이 + 말풍선 ScrollView 하단 여백 애니메이션.
/// 말풍선을 직접 옮기지 않는다 — 뷰포트 하단 inset만 줄이면 Content(pivot=top)가
/// 바닥에 붙어 있는 한 말풍선이 저절로 위로 밀린다.
/// 여백 상수는 프리팹 값에서 역산하므로 코드에 하드코딩된 수치가 없다.
/// </summary>
[DisallowMultipleComponent]
public class ChatChoicePanelLayout : MonoBehaviour
{
    [Header("선택지 영역")]
    [Tooltip("높이가 애니메이션되는 컨테이너. 보통 이 컴포넌트가 붙은 ChoiceArea 자신.")]
    [SerializeField] private RectTransform container;
    [Tooltip("선택지 버튼이 스폰되는 부모. ContentSizeFitter가 높이를 결정한다.")]
    [SerializeField] private RectTransform content;

    [Header("말풍선 영역")]
    [Tooltip("말풍선 ScrollView의 RectTransform. 하단 inset이 애니메이션된다.")]
    [SerializeField] private RectTransform chatScrollRect;
    [Tooltip("애니메이션 동안 바닥 고정에 사용.")]
    [SerializeField] private ScrollRect chatScroll;

    [Header("튜닝")]
    [Tooltip("선택지 영역 최대 높이 = 패널 높이 x 이 비율. 초과분은 영역 내부 스크롤.")]
    [SerializeField] private float maxHeightRatio = 0.45f;
    [Tooltip("선택지 영역 위와 말풍선 영역 아래 사이 간격.")]
    [SerializeField] private float gapAboveChoices = 20f;
    [SerializeField] private float duration = 0.18f;

    private float topInset;           // 프리팹에서 역산
    private float choiceBottomOffset; // 프리팹에서 역산
    private float fromHeight;
    private float toHeight;
    private float fromInset;
    private float toInset;
    private Tween activeTween;
    private bool hasTarget;

    /// <summary>선택지 버튼을 Instantiate 할 부모.</summary>
    public RectTransform SpawnParent => content;

    private void Awake()
    {
        // 완전 stretch된 rect에서 성립하는 항등식으로 프리팹 여백을 역산한다.
        //   -sizeDelta.y       = topInset + bottomInset
        //   anchoredPosition.y = (bottomInset - topInset) / 2
        // 반드시 어떤 수정보다 먼저 읽어야 한다.
        float sum = -chatScrollRect.sizeDelta.y;
        float diff = chatScrollRect.anchoredPosition.y * 2f;
        topInset = (sum - diff) * 0.5f;

        choiceBottomOffset = container.anchoredPosition.y;
    }

    /// <summary>대화 재시작(Begin) 시 애니메이션 없이 숨김 상태로 리셋.</summary>
    public void ApplyHiddenImmediate()
    {
        if (activeTween.isAlive) activeTween.Stop();

        hasTarget = false;
        SetChoiceHeight(0f);
        SetChatBottomInset(choiceBottomOffset);
        container.gameObject.SetActive(false);
    }

    /// <summary>선택지 버튼 스폰이 끝난 직후 호출. 자연 높이를 측정해 슬라이드 업.</summary>
    public void PlayShow()
    {
        // 측정하려면 활성 상태여야 한다. 비활성 오브젝트는 레이아웃이 리빌드되지 않는다.
        container.gameObject.SetActive(true);

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float measured = content.rect.height;
        float capped = Mathf.Min(measured, PanelHeight() * maxHeightRatio);

        AnimateTo(capped);
    }

    /// <summary>선택지가 사라질 때 호출. 슬라이드 다운 후 비활성화.</summary>
    public void PlayHide()
    {
        AnimateTo(0f);
    }

    private void AnimateTo(float targetHeight)
    {
        // SkipToEnd()는 한 프레임에 OnBranchShown을 수십 번 발사한다.
        // 목표가 이미 같으면 무시해 트윈 재시작으로 레이아웃이 어긋나는 것을 막는다.
        if (hasTarget && Mathf.Approximately(toHeight, targetHeight)) return;

        if (activeTween.isAlive) activeTween.Stop();

        container.gameObject.SetActive(true); // 애니메이션 동안은 항상 활성

        fromHeight = container.sizeDelta.y;
        toHeight = targetHeight;
        fromInset = CurrentChatBottomInset();
        toInset = choiceBottomOffset + (targetHeight > 0f ? targetHeight + gapAboveChoices : 0f);
        hasTarget = true;

        // target 바인딩 오버로드: 이 컴포넌트가 파괴되면 PrimeTween이 트윈을 자동 정지한다.
        // EndChat() 직후 Close()가 Destroy 하는 경로가 이것으로 안전해진다.
        activeTween = Tween.Custom(
            this, 0f, 1f, duration,
            static (layout, k) => layout.ApplyProgress(k),
            Ease.OutCubic,
            useUnscaledTime: true);

        activeTween.OnComplete(this, static layout => layout.OnAnimationComplete());
    }

    private void ApplyProgress(float k)
    {
        SetChoiceHeight(Mathf.Lerp(fromHeight, toHeight, k));
        SetChatBottomInset(Mathf.Lerp(fromInset, toInset, k));

        // 같은 프레임에 뷰포트를 리사이즈했으므로 ScrollRect의 내부 bounds가 stale이다.
        Canvas.ForceUpdateCanvases();
        if (chatScroll != null) chatScroll.verticalNormalizedPosition = 0f; // 이 구간에서만 바닥 고정
    }

    private void OnAnimationComplete()
    {
        SetChoiceHeight(toHeight);
        SetChatBottomInset(toInset);

        if (toHeight <= 0f) container.gameObject.SetActive(false);
        // 바닥 고정 해제 — 이후 플레이어가 자유롭게 위로 스크롤해 과거 대사를 읽을 수 있다.
    }

    private float PanelHeight()
    {
        RectTransform panel = container.parent as RectTransform;
        return panel != null ? panel.rect.height : Screen.height;
    }

    private void SetChoiceHeight(float h)
    {
        Vector2 sd = container.sizeDelta;
        container.sizeDelta = new Vector2(sd.x, h);
    }

    private void SetChatBottomInset(float b)
    {
        Vector2 sd = chatScrollRect.sizeDelta;
        chatScrollRect.sizeDelta = new Vector2(sd.x, -(topInset + b));

        Vector2 ap = chatScrollRect.anchoredPosition;
        chatScrollRect.anchoredPosition = new Vector2(ap.x, (b - topInset) * 0.5f);
    }

    /// <summary>SetChatBottomInset의 역함수.</summary>
    private float CurrentChatBottomInset()
    {
        return -chatScrollRect.sizeDelta.y - topInset;
    }
}
