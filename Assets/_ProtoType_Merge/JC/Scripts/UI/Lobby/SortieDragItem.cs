using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 출전 로스터 영웅 항목의 드래그 핸들러. 드래그 시작/이동/종료를 SortieController로 전달한다.
/// SortieController가 로스터 Rebuild 후 각 HeroProfileButton에 런타임 부착(unitIndex/owner 주입).
/// </summary>
[DisallowMultipleComponent]
public class SortieDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [System.NonSerialized] public int unitIndex;
    [System.NonSerialized] public SortieController owner;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner != null && unitIndex > 0) owner.BeginDrag(unitIndex, eventData);
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
