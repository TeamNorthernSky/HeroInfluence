using UnityEngine;

[DisallowMultipleComponent]
public class Modal : MonoBehaviour
{
    // [JC 260513] 활성 중 Time.timeScale=0 적용 여부. 다른 pausesGame 모달이 스택에 남아 있으면 유지.
    [SerializeField] private bool pausesGame;

    // [JC 260519] 외부 클릭 닫기 옵트인. contentRect 밖 클릭 + 본 모달이 ModalRegistry.Top일 때만 SetActive(false).
    [Header("외부 클릭 닫기 (옵트인)")]
    [SerializeField] private bool closeOnOutsideClick;
    [Tooltip("내부로 간주할 RectTransform (보통 모달의 메인 패널). 비어 있으면 외부클릭 닫기 비활성.")]
    [SerializeField] private RectTransform contentRect;

    public bool PausesGame => pausesGame;

    private void OnEnable()
    {
        transform.SetAsLastSibling();
        ModalRegistry.Register(gameObject);
        if (pausesGame)
            ModalPauseGate.Refresh();
    }

    private void OnDisable()
    {
        ModalRegistry.Unregister(gameObject);
        if (pausesGame)
            ModalPauseGate.Refresh();
    }

    private void Update()
    {
        if (!closeOnOutsideClick) return;
        if (contentRect == null) return;
        if (ModalRegistry.Top != gameObject) return;
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
