using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 출전 진형 슬롯 입력. 좌클릭·우클릭 모두 해당 슬롯의 영웅을 해제한다.
/// SortieController.Awake에서 각 슬롯 버튼에 런타임 부착(index/owner 주입).
/// (드롭 배치는 SortieController의 드래그 레이캐스트가 담당하므로 여기선 클릭 해제만.)
/// </summary>
[DisallowMultipleComponent]
public class SortieSlotInput : MonoBehaviour, IPointerClickHandler
{
    [System.NonSerialized] public int index;
    [System.NonSerialized] public SortieController owner;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null) return;
        // 우클릭으로만 해제 (좌클릭은 해제하지 않음)
        if (eventData.button == PointerEventData.InputButton.Right)
            owner.RemoveSlot(index);
    }
}
