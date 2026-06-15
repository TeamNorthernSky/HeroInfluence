using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 협회 팝업 공용 버튼 비주얼. (기획서: normal 이미지 + hover 시 hover 이미지 오버레이 + 텍스트 위에,
/// 비활성 시 closed 이미지)
/// - background: 버튼 배경 Image(normal↔closed 교체)
/// - hoverOverlay: hover 시 표시할 오버레이 Image(hover 스프라이트). 텍스트 라벨은 그 위(최상위 자식).
/// Button.interactable 변화에 따라 closed/normal 자동 전환.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class PopupButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image hoverOverlay;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Sprite closedSprite;

    private Button button;
    private bool hovering;
    private bool lastInteractable = true;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (background == null) background = button != null ? button.image : null;
        if (hoverOverlay != null)
        {
            if (hoverSprite != null) hoverOverlay.sprite = hoverSprite;
            hoverOverlay.raycastTarget = false;
        }
    }

    private void OnEnable() { hovering = false; Apply(); }

    public void OnPointerEnter(PointerEventData eventData) { hovering = true; Apply(); }
    public void OnPointerExit(PointerEventData eventData) { hovering = false; Apply(); }

    private void Update()
    {
        if (button != null && button.interactable != lastInteractable) Apply();
    }

    private void Apply()
    {
        bool disabled = button != null && !button.interactable;
        lastInteractable = !disabled;
        if (background != null && normalSprite != null)
            background.sprite = disabled ? (closedSprite != null ? closedSprite : normalSprite) : normalSprite;
        if (hoverOverlay != null)
            hoverOverlay.gameObject.SetActive(hovering && !disabled);
    }
}
