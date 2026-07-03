using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260616] 출전 버튼(BTN_HQLobby_Go) 진입 게이트. (기획서 협회(본부,출전) ④)
///
/// [JC 260703] 열기 전용으로 축소. 닫기(편성완료/취소)는 Modal_Sortie 내부 버튼(BTN_Sortie_Confirm/BTN_Close)이
/// SortieController.TryClose로 담당한다. 모달 열림 동안 이 열기버튼은 SortieController가 비활성화한다
/// (구 '재클릭=닫기' 분기 및 런타임 메시지 모달 제거 — "1개 파티만" 안내는 이제 SortieController.TryClose가 표시).
///
/// 상태별 분기:
///  - 출전 가능 영웅 0명(상주 파티 멤버 + 무소속 모두 없음) → 버튼 비활성 + 회색 오버레이(hover·클릭 차단).
///  - 그 외 → 진형 편집 모달 오픈(상주 파티 없어도 새 파티 편성 화면으로 열되, 확정 시 SortieController가 "1개 파티만" 안내).
///
/// 회색 오버레이는 런타임 생성(씬 의존 최소화). 상주/카운트 판정은 SortieController 정적 메서드 단일 출처.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SortieEntryGate : MonoBehaviour
{
    [Header("참조 (비우면 자동 해석)")]
    [SerializeField] private Button button;
    [SerializeField] private SortieController controller;
    [SerializeField] private GameObject modalRoot;

    [Header("0명 회색 오버레이")]
    [SerializeField] private Color disabledOverlayColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);

    private Image greyOverlay;
    private float nextPoll;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (controller == null) controller = FindObjectOfType<SortieController>(true);
        if (modalRoot == null && controller != null) modalRoot = controller.gameObject;

        // 충돌 방지: 기존 단순 토글 컴포넌트가 남아 있으면 제거(중복 토글 차단)
        var toggle = GetComponent<ModalToggleButton>();
        if (toggle != null) Destroy(toggle);

        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.2f;
        RefreshGate();
    }

    private void RefreshGate()
    {
        // [JC 260629] 크로스번들 — controller를 사용 시점에 레지스트리에서 해석.
        if (controller == null) controller = LobbyUIRegistry.Sortie;
        if (modalRoot == null && controller != null) modalRoot = controller.gameObject;

        // [JC 260703] 모달 열림 동안 이 버튼은 SortieController가 비활성화 → Update/폴링도 정지.
        // 열기 가능 여부(0명 → 비활성 + 회색)만 판정한다.
        bool active = SortieController.CountDeployableHeroes() > 0;
        if (button != null) button.interactable = active;
        EnsureGreyOverlay();
        if (greyOverlay != null) greyOverlay.gameObject.SetActive(!active);
    }

    private void OnClick()
    {
        // [JC 260703] 열기 전용. 닫기는 편성완료/X 버튼(→ SortieController.TryClose)이 담당.
        if (controller == null) controller = LobbyUIRegistry.Sortie;
        if (modalRoot == null && controller != null) modalRoot = controller.gameObject;

        bool open = modalRoot != null && modalRoot.activeSelf;
        if (open) return; // 이미 열림 → 무동작(재오픈 방지; 통상 이 버튼은 모달 중 비활성)

        if (SortieController.CountDeployableHeroes() <= 0) return; // 0명 → 비활성 상태여야 정상

        // [JC 260616] 상주 파티가 없어도 새 파티 편성 화면으로 열되, 확정 시 SortieController가 "1개 파티만" 안내로 막는다.
        if (modalRoot != null) modalRoot.SetActive(true);
    }

    // ─── 회색 오버레이 ───────────────────────────────────────
    private void EnsureGreyOverlay()
    {
        if (greyOverlay != null || button == null) return;
        var go = new GameObject("SortieDisabledOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(button.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        greyOverlay = go.GetComponent<Image>();
        greyOverlay.color = disabledOverlayColor;
        greyOverlay.raycastTarget = true; // hover·클릭 동시 차단
        rt.SetAsLastSibling();
        go.SetActive(false);
    }
}
