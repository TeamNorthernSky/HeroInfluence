using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260622] 현재 선택된 스킬 버튼에 "선택 외곽선 오버레이"를 상시 표시한다.
/// - 선택 판정 = 같은 GameObject의 ToggleButton.IsOn (SkillButtonController가 toggle1/2를 상호배타로 관리).
///   OnTurnStarted가 notify=false로 SetState하므로 이벤트 누락 방지 위해 IsOn을 매 프레임 폴링한다.
/// - 호버 스윕 우선: 마우스가 이 버튼 위에 있으면(hovered) 선택 오버레이를 숨긴다.
/// 호버 오버레이(BaseHoverOverlay)와는 별개 GameObject(selectedOverlay)다.
/// </summary>
[DisallowMultipleComponent]
public class SelectedSkillHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("선택 판정에 쓸 ToggleButton. 비우면 같은 GameObject에서 탐색.")]
    [SerializeField] private ToggleButton toggle;
    [Tooltip("선택 시 상시 켜질 외곽선 오버레이 GameObject.")]
    [SerializeField] private GameObject selectedOverlay;
    [Tooltip("자동전투·배속 등의 ON 순환광 표시입니다. 연결하면 기존 ToggleButton의 상태를 읽어 순환광을 재생하며 호버 중에도 유지합니다. 비우면 기존 고정 오버레이 동작을 유지합니다.")]
    [SerializeField] private BattleSkillButtonVisual stateVisual;

    private bool hovered;

    private void Awake()
    {
        if (toggle == null) toggle = GetComponent<ToggleButton>();
    }

    private void OnDisable()
    {
        hovered = false;
        if (stateVisual != null) stateVisual.SetState(false, false);
        if (selectedOverlay != null) selectedOverlay.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void Update()
    {
        if (stateVisual != null)
        {
            stateVisual.SetState(false, toggle != null && toggle.IsOn);
            return;
        }
        if (selectedOverlay == null) return;
        bool show = toggle != null && toggle.IsOn && !hovered;
        if (selectedOverlay.activeSelf != show) selectedOverlay.SetActive(show);
    }
}
