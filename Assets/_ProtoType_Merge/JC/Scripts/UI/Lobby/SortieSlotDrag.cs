using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 진형 슬롯의 드래그 핸들러. 슬롯에 배치된 영웅을 드래그해 다른 슬롯으로 이동/교환하거나
/// 진형 밖으로 드래그해 배치 취소한다. SortieController.Awake에서 각 슬롯 버튼에 런타임 부착.
/// </summary>
[DisallowMultipleComponent]
public class SortieSlotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [System.NonSerialized] public int index;
    [System.NonSerialized] public SortieController owner;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner != null) owner.BeginSlotDrag(index, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.UpdateDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner != null) owner.EndDrag(eventData);
    }
}
