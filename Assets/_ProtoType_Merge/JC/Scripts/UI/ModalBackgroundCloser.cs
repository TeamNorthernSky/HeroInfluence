using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 모달 배경(전체화면 blocker Graphic) 클릭 시 모달을 닫는다.
/// 모달 루트(raycastTarget Image)가 panel 뒤를 덮고 있어, panel 영역 클릭은 panel이 소비하고
/// 그 밖(어두운 영역) 클릭만 이 핸들러에 도달한다. EventSystem 기반이라 ModalRegistry.Top 의존이 없다.
/// Modal.closeOnOutsideClick(기하 판정)과 독립적으로 동작하는 견고한 외부클릭 닫기.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Graphic))]
public class ModalBackgroundCloser : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("닫을 대상. 비우면 이 GameObject(모달 루트).")]
    [SerializeField] private GameObject target;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;
        // 이벤트 버블링 가드: 실제로 배경(이 GameObject)을 직접 눌렀을 때만 닫는다.
        // 패널/버튼 등 자식을 누른 클릭이 부모로 전파된 경우는 무시.
        if (eventData.pointerPressRaycast.gameObject != gameObject) return;
        GameObject go = target != null ? target : gameObject;
        go.SetActive(false);
    }
}
