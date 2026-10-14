using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>카탈로그 예제의 입력 상태만 공급합니다. 효과는 기존 버튼 컴포넌트가 재생합니다.</summary>
[DisallowMultipleComponent]
public sealed class CanvasButtonCatalogSample : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("팀원이 이 예제를 지칭할 때 사용하는 고정 식별자입니다.")]
    public string effectId;
    [Tooltip("카탈로그에 표시하는 효과 이름입니다.")]
    public string displayName;
    [TextArea, Tooltip("효과가 전달하는 의미와 미리보기 조작 방법입니다.")]
    public string description;
    [TextArea, Tooltip("동일한 효과를 적용할 때 참고할 기존 코드와 재질의 프로젝트 경로입니다.")]
    public string references;
    [Tooltip("예제 버튼입니다. 게임 로직이나 씬 전환은 연결하지 않습니다.")]
    public Button targetButton;
    [Tooltip("기존 전투 버튼 표시 컴포넌트입니다. 카탈로그가 사용 가능/선택 상태만 전달합니다.")]
    public BattleSkillButtonVisual stateVisual;
    [Tooltip("기존 호버 오버레이입니다. 초기화 및 입력 비활성 시 숨깁니다.")]
    public GameObject hoverOverlay;
    [Tooltip("선택 테두리 예제에서 클릭으로 선택 상태를 전환합니다.")]
    public bool toggleSelectionOnClick;
    [Tooltip("플레이 중 버튼 입력을 허용합니다. 끄면 호버·클릭·사용 가능 강조가 해제됩니다.")]
    public bool interactable = true;
    [Tooltip("플레이 중 사용 가능 스윕을 표시합니다. 실제 스킬 판정과 무관한 예제 상태입니다.")]
    public bool available = true;
    [Tooltip("플레이 중 선택 테두리를 표시합니다. 클릭으로도 전환할 수 있습니다.")]
    public bool selected;

    private Vector3 originalScale;
    private Vector2 originalPosition;
    private RectTransform rect;
    private ButtonEffectModule[] modules;
    private BattleSkillSweepSettings settings;
    private bool initialized;
    private bool previousInteractable;

    private void Awake()
    {
        rect = transform as RectTransform;
        originalScale = transform.localScale;
        originalPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
        modules = GetComponents<ButtonEffectModule>();
        settings = GetComponent<BattleSkillSweepSettings>();
        initialized = true;
        previousInteractable = interactable;
    }

    private void LateUpdate()
    {
        if (previousInteractable && !interactable) ResetMotion();
        previousInteractable = interactable;
        if (targetButton != null) targetButton.interactable = interactable;
        bool orbitSelected = selected && settings != null && settings.selectedOrbit;
        if (stateVisual != null) stateVisual.SetState(interactable && available && !orbitSelected, interactable && selected);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && interactable && toggleSelectionOnClick)
            selected = !selected;
    }

    public void ResetPreview()
    {
        selected = false;
        interactable = true;
        ResetMotion();
        if (stateVisual != null) stateVisual.SetState(false, false);
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        if (!value) ResetMotion();
        if (targetButton != null) targetButton.interactable = value;
    }

    private void OnDisable() => ResetMotion();

    private void ResetMotion()
    {
        if (!initialized) return;
        foreach (var module in modules)
        {
            module.StopAllCoroutines();
            module.CancelInvoke();
        }
        transform.localScale = originalScale;
        if (rect != null) rect.anchoredPosition = originalPosition;
        if (hoverOverlay != null) hoverOverlay.SetActive(false);
    }
}
