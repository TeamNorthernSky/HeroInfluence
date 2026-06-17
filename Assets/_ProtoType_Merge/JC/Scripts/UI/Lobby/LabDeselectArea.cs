using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 연구소 모달의 "단계 아이콘 외 영역" 클릭 감지 → 선택 해제.
/// </summary>
[DisallowMultipleComponent]
public class LabDeselectArea : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private LabModalController controller;
    public void Bind(LabModalController c) { controller = c; }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller != null) controller.OnDeselectAreaClicked();
    }
}
