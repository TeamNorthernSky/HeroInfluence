using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260702] 공유 dim(ModalManager) 클릭 시 현재 Top 모달을 닫는다 — 단 Top 모달의 Modal.DimClosesModal=true일 때만.
/// dim은 top 모달 바로 아래 형제라, 모달 콘텐츠 밖(어두운 영역) 클릭만 이 핸들러에 도달한다.
/// 기본은 닫지 않음(내부 버튼 전용). 기획/팀원이 Modal 인스펙터의 dimClosesModal 토글로 제어.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Graphic))]
public class ModalDimClickCloser : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
        // 버블링 가드: 실제로 dim(이 GameObject)을 직접 눌렀을 때만.
        if (eventData.pointerPressRaycast.gameObject != gameObject) return;

        GameObject top = ModalManager.Top;
        if (top == null) return;
        Modal modal = top.GetComponent<Modal>();
        if (modal != null && modal.DimClosesModal)
            top.SetActive(false);
    }
}
