using UnityEngine;

// 투명한 모달 차단 영역에서도 지정한 영웅 슬롯만 클릭을 통과시킵니다.
public sealed class ModalDimRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        var top = ModalManager.Top;
        var modal = top != null ? top.GetComponent<Modal>() : null;
        return modal == null || !modal.AllowsDimClickThrough(screenPoint);
    }
}
