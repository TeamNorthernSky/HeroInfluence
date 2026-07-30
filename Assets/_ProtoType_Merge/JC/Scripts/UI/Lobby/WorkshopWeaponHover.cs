using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 공방 무기 행 롤오버 감지 → 리치 툴팁.
/// [KJ 260729] 코어 5종 개편으로 무기 행/강화 단계셀이 사라져 툴팁을 보류한다.
/// 파일과 결선은 유지하고 본문만 비운다. 2단계에서 CoreCard 기준으로 재작성 예정.
/// </summary>
[DisallowMultipleComponent]
public class WorkshopWeaponHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void Bind(WorkshopModalController c, int row, int stage) { }

    public void OnPointerEnter(PointerEventData eventData) { }

    public void OnPointerExit(PointerEventData eventData) { }
}
