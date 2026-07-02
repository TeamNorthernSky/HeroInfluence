using UnityEngine;

[DisallowMultipleComponent]
public class Modal : MonoBehaviour
{
    // [JC 260513] 활성 중 Time.timeScale=0 적용 여부. 다른 pausesGame 모달이 스택에 남아 있으면 유지.
    [SerializeField] private bool pausesGame;

    // [JC 260519] 외부 클릭 닫기 옵트인(기하 판정). contentRect 밖 클릭 + 본 모달이 ModalManager.Top일 때만 SetActive(false).
    // [JC 260702] 공유 dim 도입 후 dimClosesModal과 역할 중복 — Phase 3에서 점진 은퇴 예정. 현행 유지.
    [Header("외부 클릭 닫기 (옵트인 · 기하 판정)")]
    [SerializeField] private bool closeOnOutsideClick;
    [Tooltip("내부로 간주할 RectTransform (보통 모달의 메인 패널). 비어 있으면 외부클릭 닫기 비활성.")]
    [SerializeField] private RectTransform contentRect;

    // [JC 260702] 공유 dim(ModalManager)이 이 모달을 어떻게 다룰지 제어하는 인스턴스별 정책.
    [Header("공유 Dim (ModalManager)")]
    [Tooltip("이 모달이 활성일 때 공유 dim 오버레이를 뒤에 표시할지. (Phase 마이그레이션 중 기본 false — 자체 dim 보유 모달과의 이중 dim 방지)")]
    [SerializeField] private bool wantsDim = false;
    [Tooltip("공유 dim 영역(모달 밖) 클릭 시 이 모달을 닫을지. 기본 false = 내부 버튼으로만 닫힘. 기획/팀원이 인스펙터에서 토글.")]
    [SerializeField] private bool dimClosesModal = false;
    [Range(0f, 1f)]
    [Tooltip("공유 dim 농도(알파). 0~1.")]
    [SerializeField] private float dimAlpha = 0.75f;

    public bool PausesGame => pausesGame;
    public bool WantsDim => wantsDim;
    public bool DimClosesModal => dimClosesModal;
    public float DimAlpha => dimAlpha;

    private void OnEnable()
    {
        transform.SetAsLastSibling();
        ModalManager.Register(gameObject);
        if (pausesGame)
            ModalPauseGate.Refresh();
    }

    private void OnDisable()
    {
        ModalManager.Unregister(gameObject);
        if (pausesGame)
            ModalPauseGate.Refresh();
    }

    private void Update()
    {
        if (!closeOnOutsideClick) return;
        if (contentRect == null) return;
        if (ModalManager.Top != gameObject) return;
        if (!Input.GetMouseButtonDown(0)) return;

        var canvas = contentRect.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(contentRect, Input.mousePosition, cam))
        {
            gameObject.SetActive(false);
        }
    }
}
