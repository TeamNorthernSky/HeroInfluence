using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>씬에 선배치한 시작/턴 안내만 표시합니다. 턴 계산과 행동 시작은 BattleFlowManager가 소유합니다.</summary>
public class BattleTurnBanner : MonoBehaviour
{
    [Tooltip("전투 시작 시 중앙에 표시할 배너입니다. 초기에는 비활성으로 배치합니다.")]
    [SerializeField] private GameObject startBanner;
    [Tooltip("유닛마다 상단 중앙에 표시할 턴 배너입니다. 초기에는 비활성으로 배치합니다.")]
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
    [Tooltip("배속과 무관한 표시 시간(초)입니다. 메뉴/튜토리얼 일시정지 시간은 제외합니다. 0이면 다음 프레임에 종료합니다.")]
    [Min(0f)] [SerializeField] private float duration = 1f;

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
                if (clicked || elapsed >= duration) break;
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
    }

    private void OnDisable() => Hide();
}
