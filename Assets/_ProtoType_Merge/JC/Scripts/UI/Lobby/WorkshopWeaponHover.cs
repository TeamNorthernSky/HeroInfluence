using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 공방 무기 행 항목(대표 또는 단계셀) 롤오버 감지 → WorkshopModalController가 리치 툴팁을 띄운다.
/// stageIndex = -1 이면 대표(현재 레벨 정보), 0~3 이면 강화 단계셀(Lv 비교). 연구소 LabSkillHover 대응.
/// </summary>
[DisallowMultipleComponent]
public class WorkshopWeaponHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private WorkshopModalController controller;
    private int rowIndex;
    private int stageIndex = -1;

    public void Bind(WorkshopModalController c, int row, int stage)
    {
        controller = c; rowIndex = row; stageIndex = stage;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (controller != null) controller.OnWeaponHover(rowIndex, stageIndex, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (controller != null) controller.OnWeaponHover(rowIndex, stageIndex, false);
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnWeaponHover(rowIndex, stageIndex, false);
    }
}
