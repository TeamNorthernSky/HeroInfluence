using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 연구소 스킬 행 항목(rep 또는 단계셀) 롤오버 감지 → LabModalController가 리치 툴팁을 띄운다.
/// stageIndex = -1 이면 rep(1레벨), 0~3 이면 강화 단계셀.
/// </summary>
[DisallowMultipleComponent]
public class LabSkillHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private LabModalController controller;
    [SerializeField] private int rowIndex;
    [SerializeField] private int stageIndex = -1;

    public void Bind(LabModalController c, int row, int stage)
    {
        controller = c; rowIndex = row; stageIndex = stage;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (controller != null) controller.OnSkillHover(rowIndex, stageIndex, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (controller != null) controller.OnSkillHover(rowIndex, stageIndex, false);
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnSkillHover(rowIndex, stageIndex, false);
    }
}
