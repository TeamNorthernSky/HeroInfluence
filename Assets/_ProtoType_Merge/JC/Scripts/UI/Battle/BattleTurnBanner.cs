using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>씬에 선배치한 시작/턴 안내만 표시합니다. 턴 계산과 행동 시작은 BattleFlowManager가 소유합니다.</summary>
public class BattleTurnBanner : MonoBehaviour
{
    [Tooltip("전투 시작 시 중앙에 표시할 배너입니다. 초기에는 비활성으로 배치합니다.")]
    [SerializeField] private GameObject startBanner;
    [Tooltip("유닛마다 지정한 높이로 화면 중앙을 통과할 턴 배너입니다. 초기에는 비활성으로 배치합니다.")]
    [SerializeField] private GameObject turnBanner;
    [Tooltip("아군/적에 따라 스프라이트를 바꿀 턴 배너 이미지입니다.")]
    [SerializeField] private Image turnImage;
    [Tooltip("아군 턴/적군 턴 문구를 표시하는 씬의 고정 텍스트입니다.")]
    [SerializeField] private TMP_Text turnLabel;
    [Tooltip("아군 순번에 표시하는 배너 스프라이트입니다.")]
    [SerializeField] private Sprite playerSprite;
    [Tooltip("적 순번에 표시하는 배너 스프라이트입니다.")]
    [SerializeField] private Sprite enemySprite;
    [Tooltip("배너 대기 중 하단/필드 클릭 전달을 막는 투명한 전 화면 Image입니다. 메뉴/튜토리얼 모달 중에는 잠시 끕니다.")]
    [SerializeField] private GameObject inputBlocker;
    [Tooltip("이동 연출을 끈 경우의 턴 안내 시간(초)입니다. 클릭하면 즉시 닫습니다. 0이면 다음 프레임에 종료합니다.")]
    [Min(0f)] [SerializeField] private float duration = 3f;
    [Tooltip("전투 시작 안내의 표시 시간(초)입니다. 클릭으로 닫지 않으며 배속과 무관합니다. 메뉴/튜토리얼 일시정지 시간은 제외합니다. 0이면 다음 프레임에 종료합니다.")]
    [Min(0f)] [SerializeField] private float startDuration = 1f;

    [Header("턴 알림 이동 — 초안")]
    [Tooltip("턴 알림의 좌측 진입→중앙 정지→우측 퇴장을 켭니다. 전투 시작 안내에는 적용하지 않습니다.")]
    [SerializeField] private bool animateTurn;
    [InspectorName("배너 세로 위치 (px)")]
    [Tooltip("화면 세로 중앙에서 배너 피봇까지의 높이입니다. 0=중앙, 양수=위, 음수=아래. Canvas 기준 픽셀이므로 Canvas Scaler 배율을 따릅니다. 턴 배너에만 적용하며 이동 중에도 높이를 조절할 수 있습니다. 기본값은 0이고 씬에 저장됩니다.")]
    [SerializeField] private float verticalOffset;
    [Tooltip("등장 시간(초)입니다. 이동과 선형 페이드 인을 함께 수행합니다. 0이면 즉시 중앙에 표시합니다.")]
    [Min(0)] [SerializeField] private float enterDuration = .5f;
    [Tooltip("중앙에 정지하는 시간(초)입니다. 알파는 1을 유지합니다.")]
    [Min(0)] [SerializeField] private float holdDuration = 1f;
    [Tooltip("퇴장 시간(초)입니다. 이동과 선형 페이드 아웃을 함께 수행합니다. 0이면 즉시 숨깁니다.")]
    [Min(0)] [SerializeField] private float exitDuration = .5f;
    [Tooltip("등장 이동 곡선입니다. 가로=시간비율, 세로=이동비율(0~1). 기본은 빠르게 등장한 뒤 감속합니다.")]
    [SerializeField] private AnimationCurve enterCurve = new AnimationCurve(new Keyframe(0, 0, 3, 3), new Keyframe(1, 1, 0, 0));
    [Tooltip("퇴장 이동 곡선입니다. 기본 t²은 정지 상태에서 등가속 이동합니다. 곡선으로 가속 구간을 조절합니다.")]
    [SerializeField] private AnimationCurve exitCurve = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2));
    [Tooltip("클릭 후 퇴장의 최대 시간(초)입니다. 기존 남은 시간이 더 짧으면 그 시간 안에 끝냅니다. 0이면 즉시 숨깁니다.")]
    [Min(0)] [SerializeField] private float clickExitDuration = .3f;
    [Tooltip("클릭 퇴장의 이동 곡선입니다. 현재 위치에서 우측 끝으로 이동하며 기본은 선형입니다.")]
    [SerializeField] private AnimationCurve clickExitCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Tooltip("등장 시작/퇴장 끝의 최저 알파입니다. 기본 0.1, 최대 알파는 1입니다. 클릭 퇴장은 현재 알파에서 이어집니다.")]
    [Range(0, 1)] [SerializeField] private float minimumAlpha = .1f;
    [Tooltip("화면 밖에 더 이동할 거리(Canvas 기준 픽셀)입니다. 팝업 폭과 화면 크기는 자동 반영합니다.")]
    [Min(0)] [SerializeField] private float offscreenMargin = 80;
    [Tooltip("턴 배너 전체(글자·블러·속도선)의 선형 페이드를 적용할 CanvasGroup입니다.")]
    [SerializeField] private CanvasGroup turnGroup;

    [Header("이동 블러 — 배경과 외곽, 글자는 선명하게 유지")]
    [Tooltip("이동 방향의 부드러운 배너 잔상을 켭니다.")] [SerializeField] private bool useBlur = true;
    [Tooltip("씬에 선배치한 배너 잔상 Image입니다. TurnNotifyMotion 재질을 사용합니다.")]
    [SerializeField] private Image blurImage;
    [Tooltip("블러 강도입니다. 0이면 보이지 않습니다.")] [Range(0, 2)] [SerializeField] private float blurStrength = .45f;
    [Tooltip("블러의 최대 길이(Canvas 픽셀)입니다. 주 배너 너비와 별개입니다.")] [Range(0, 300)] [SerializeField] private float blurMaxLength = 100;
    [Tooltip("블러/속도선이 최대가 되는 이동 속도(Canvas 픽셀/초)입니다. 낮추면 더 빨리 강해집니다.")]
    [Min(1)] [SerializeField] private float effectSpeed = 2200;
    [Tooltip("중앙 정지 시 잔상이 사라지는 시간(초)입니다. 0이면 즉시 사라집니다.")]
    [Min(0)] [SerializeField] private float effectSettleDuration = .08f;

    [Header("스트레이크 — 뒤쪽 수평 속도선")]
    [Tooltip("배너 뒤쪽에 수평 속도선을 표시합니다.")] [SerializeField] private bool useStreaks = true;
    [Tooltip("씬에 선배치한 속도선 Image들입니다. 개수 설정은 이 배열의 길이 이내로 제한됩니다.")]
    [SerializeField] private Image[] streakImages = new Image[0];
    [Tooltip("표시할 속도선 수입니다. 0이면 숨깁니다.")] [Range(0, 12)] [SerializeField] private int streakCount = 6;
    [Tooltip("아군 턴 속도선 색입니다.")] [SerializeField] private Color playerStreakColor = new Color(.25f, .65f, 1, 1);
    [Tooltip("적군 턴 속도선 색입니다.")] [SerializeField] private Color enemyStreakColor = new Color(1, .35f, .18f, 1);
    [Tooltip("속도선 최대 불투명도입니다. 배너 전체 페이드도 곱해집니다.")] [Range(0, 1)] [SerializeField] private float streakOpacity = .65f;
    [Tooltip("속도선 길이의 최소/최대(Canvas 픽셀)입니다. 실제 길이는 이동 속도에 따라 줄어듭니다.")]
    [SerializeField] private Vector2 streakLength = new Vector2(60, 240);
    [Tooltip("속도선 두께의 최소/최대(Canvas 픽셀)입니다.")]
    [SerializeField] private Vector2 streakThickness = new Vector2(2, 7);
    [Tooltip("배너 중앙에서 위아래로 속도선을 배치할 범위(Canvas 픽셀)입니다.")]
    [Min(0)] [SerializeField] private float streakSpread = 110;

    private RectTransform motionRect;
    private Vector2 restPosition;
    private float leftX, rightX, motionTime, skipTime, skipDuration, skipX, skipAlpha, effectAmount;
    private bool motionPrepared, skipping, playerTurn;
    private Material blurInstance, blurSource;

    private float MotionDuration => Mathf.Max(0, enterDuration) + Mathf.Max(0, holdDuration) + Mathf.Max(0, exitDuration);

    private bool showing;
    private int dismissedFrame = -1;
    public bool BlocksInput => showing || dismissedFrame == Time.frameCount;

    public IEnumerator ShowStart(BattleFlowManager flow) => Show(flow, false, true);
    public IEnumerator ShowTurn(BattleFlowManager flow, bool isPlayer) => Show(flow, true, isPlayer);

    private IEnumerator Show(BattleFlowManager flow, bool isTurn, bool isPlayer)
    {
        if (!isActiveAndEnabled) yield break;
        var visual = isTurn ? turnBanner : startBanner;
        if (visual == null) yield break;
        Hide();
        showing = true;
        visual.SetActive(true);
        if (isTurn && turnImage != null) turnImage.sprite = isPlayer ? playerSprite : enemySprite;
        if (isTurn && turnLabel != null) turnLabel.text = isPlayer ? "아군 턴" : "적군 턴";
        if (isTurn) ApplyTurnHeight(visual.transform as RectTransform);
        bool animated = isTurn && animateTurn && turnGroup != null;
        if (animated) BeginTurnMotion(isPlayer);
        else if (isTurn && turnGroup != null) turnGroup.alpha = 1;
        if (inputBlocker != null) inputBlocker.SetActive(true);
        int openedFrame = Time.frameCount;
        float elapsed = 0f;
        bool wasSuspended = false;
        bool drainClosingClick = false;
        try
        {
            while (showing && flow != null && !flow.IsEndingBattle)
            {
                yield return null;
                if (!showing || flow == null || flow.IsEndingBattle) break;
                bool suspended = Time.timeScale <= 0f || ModalManager.HasAny || flow.IsFlowBlocked;
                if (inputBlocker != null) inputBlocker.SetActive(!suspended);
                if (suspended) { wasSuspended = true; continue; }
                // 모달을 닫은 클릭은 턴 안내 종료에 다시 사용하지 않는다.
                if (wasSuspended) { wasSuspended = false; continue; }
                elapsed += Time.unscaledDeltaTime;
                bool clicked = isTurn && Time.frameCount > openedFrame &&
                    JcPointerInput.CanControl && JcPointerInput.Inside &&
                    (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1));
                if (animated)
                {
                    if (AdvanceTurnMotion(Time.unscaledDeltaTime, clicked)) break;
                }
                else if (clicked || elapsed >= (isTurn ? duration : startDuration)) break;
            }
            drainClosingClick = showing && flow != null && !flow.IsEndingBattle && !ModalManager.HasAny;
        }
        finally
        {
            Hide();
            // EventSystem이 이 코루틴보다 나중에 처리되어도 하단 버튼이 PointerDown을 받지 않는다.
            if (drainClosingClick && inputBlocker != null) inputBlocker.SetActive(true);
        }
        // 클릭을 처리한 프레임의 EventSystem/입력 Update가 모두 끝난 후 행동을 연다.
        yield return null;
        if (inputBlocker != null) inputBlocker.SetActive(false);
    }

    public void Hide()
    {
        if (showing) dismissedFrame = Time.frameCount;
        showing = false;
        if (startBanner != null) startBanner.SetActive(false);
        if (turnBanner != null) turnBanner.SetActive(false);
        if (inputBlocker != null) inputBlocker.SetActive(false);
        ResetMotion();
    }

    private void BeginTurnMotion(bool isPlayer)
    {
        motionRect = turnBanner.transform as RectTransform;
        if (motionRect == null) return;
        restPosition = motionRect.anchoredPosition;
        var parent = motionRect.parent as RectTransform;
        float halfScreen = parent != null ? parent.rect.width * .5f : Screen.width * .5f;
        float halfBanner = motionRect.rect.width * Mathf.Abs(motionRect.localScale.x) * .5f;
        leftX = -halfScreen - halfBanner - offscreenMargin;
        rightX = halfScreen + halfBanner + offscreenMargin;
        motionTime = skipTime = effectAmount = 0; skipping = false; motionPrepared = true; playerTurn = isPlayer;
        if (blurImage != null)
        {
            if (blurInstance == null)
            {
                blurSource = blurImage.material;
                blurInstance = new Material(blurSource) { name = "TurnNotify Blur (Instance)", hideFlags = HideFlags.DontSave };
                blurImage.material = blurInstance;
            }
            blurImage.sprite = turnImage != null ? turnImage.sprite : null;
        }
        ApplyMotion(leftX, Mathf.Clamp01(minimumAlpha), 0);
    }

    // 표시 시간만 진행합니다. 전투 상태나 입력 선택은 변경하지 않습니다.
    private bool AdvanceTurnMotion(float delta, bool clicked)
    {
        if (!motionPrepared) return true;
        float dt = Mathf.Max(0, delta);
        float minAlpha = Mathf.Clamp01(minimumAlpha);
        if (clicked && !skipping)
        {
            skipping = true; skipTime = 0;
            skipDuration = Mathf.Min(Mathf.Max(0, clickExitDuration), Mathf.Max(0, MotionDuration - motionTime));
            skipX = motionRect.anchoredPosition.x; skipAlpha = turnGroup.alpha;
        }
        float oldX = motionRect.anchoredPosition.x;
        float x, alpha; bool complete;
        if (skipping)
        {
            skipTime += dt;
            float t = skipDuration > 0 ? Mathf.Clamp01(skipTime / skipDuration) : 1;
            x = Mathf.Lerp(skipX, rightX, Curve(clickExitCurve, t)); alpha = Mathf.Lerp(skipAlpha, minAlpha, t);
            complete = t >= 1;
        }
        else
        {
            motionTime += dt;
            float enter = Mathf.Max(0, enterDuration), hold = Mathf.Max(0, holdDuration), leave = Mathf.Max(0, exitDuration);
            if (enter > 0 && motionTime < enter)
            {
                float t = motionTime / enter;
                x = Mathf.Lerp(leftX, restPosition.x, Curve(enterCurve, t)); alpha = Mathf.Lerp(minAlpha, 1, t);
            }
            else if (motionTime < enter + hold) { x = restPosition.x; alpha = 1; }
            else
            {
                float t = leave > 0 ? Mathf.Clamp01((motionTime - enter - hold) / leave) : 1;
                x = Mathf.Lerp(restPosition.x, rightX, Curve(exitCurve, t)); alpha = Mathf.Lerp(1, minAlpha, t);
            }
            complete = motionTime >= enter + hold + leave;
        }
        float speed = dt > 0 ? Mathf.Abs(x - oldX) / dt : 0;
        float target = Mathf.Clamp01(speed / Mathf.Max(1, effectSpeed));
        effectAmount = target >= effectAmount || effectSettleDuration <= 0 ? target : Mathf.MoveTowards(effectAmount, target, dt / effectSettleDuration);
        ApplyMotion(x, alpha, effectAmount);
        return complete;
    }

    private static float Curve(AnimationCurve curve, float time) => Mathf.Clamp01(curve != null && curve.length > 0 ? curve.Evaluate(time) : time);

    private void ApplyTurnHeight(RectTransform rect)
    {
        if (rect == null) return;
        var position = rect.anchoredPosition;
        var parent = rect.parent as RectTransform;
        // 전 화면 부모의 중앙을 기준으로 하며, 배너 피봇/앵커 변경에도 같은 높이를 유지합니다.
        float anchorY = Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y);
        position.y = verticalOffset + (parent != null ? parent.rect.height * (.5f - anchorY) : 0f);
        rect.anchoredPosition = position;
    }

    private void ApplyMotion(float x, float alpha, float amount)
    {
        motionRect.anchoredPosition = new Vector2(x, restPosition.y);
        ApplyTurnHeight(motionRect);
        if (turnGroup != null) turnGroup.alpha = alpha;
        float width = Mathf.Max(1, motionRect.rect.width);
        if (blurImage != null)
        {
            blurImage.gameObject.SetActive(useBlur && amount > .001f && blurStrength > 0);
            var rt = blurImage.rectTransform;
            rt.offsetMin = new Vector2(-blurMaxLength, 0); rt.offsetMax = new Vector2(blurMaxLength, 0);
            if (blurInstance != null)
            {
                blurInstance.SetFloat("_Padding", blurMaxLength / (width + 2 * blurMaxLength));
                blurInstance.SetFloat("_BlurLength", blurMaxLength * amount / width);
                blurInstance.SetFloat("_Strength", blurStrength * amount);
            }
        }
        for (int i = 0; i < streakImages.Length; i++)
        {
            var image = streakImages[i]; if (image == null) continue;
            bool visible = useStreaks && i < streakCount && amount > .001f;
            image.gameObject.SetActive(visible); if (!visible) continue;
            float seed = Mathf.Repeat((i + 1) * .618034f, 1);
            var color = playerTurn ? playerStreakColor : enemyStreakColor;
            color.a *= streakOpacity * amount; image.color = color;
            image.rectTransform.sizeDelta = new Vector2(Mathf.Max(0, Mathf.Lerp(streakLength.x, streakLength.y, seed)) * amount,
                Mathf.Max(.1f, Mathf.Lerp(streakThickness.x, streakThickness.y, 1 - seed)));
            image.rectTransform.anchoredPosition = new Vector2(-width * .35f - seed * 35,
                (Mathf.Repeat((i + 1) * .381966f, 1) * 2 - 1) * streakSpread);
        }
    }

    private void ResetMotion()
    {
        if (motionPrepared && motionRect != null) motionRect.anchoredPosition = restPosition;
        motionPrepared = skipping = false; effectAmount = 0;
        if (turnGroup != null) turnGroup.alpha = 1;
        if (blurImage != null) blurImage.gameObject.SetActive(false);
        foreach (var image in streakImages) if (image != null) image.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (blurImage != null && blurSource != null) blurImage.material = blurSource;
        if (blurInstance != null)
        {
            if (Application.isPlaying) Destroy(blurInstance); else DestroyImmediate(blurInstance);
        }
    }

    private void OnDisable() => Hide();
}
