using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 훈련실 모달의 "단계 아이콘 외 영역" 클릭 감지 → 선택 해제.
/// 단계 셀 위 클릭은 셀이 소비하므로, 빈 모달 영역 클릭만 여기로 도달한다.
/// </summary>
[DisallowMultipleComponent]
public class TrainingDeselectArea : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TrainingModalController controller;
    public void Bind(TrainingModalController c) { controller = c; }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller != null) controller.OnDeselectAreaClicked();
    }
}
