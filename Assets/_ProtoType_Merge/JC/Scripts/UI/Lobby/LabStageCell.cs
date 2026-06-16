using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 연구소 스킬 강화 단계 셀 클릭 핸들러. 좌클릭=선택, 우클릭=선택 해제.
/// 단계 게이트(다음 단계만 선택 가능)는 LabModalController가 판정한다.
/// </summary>
[DisallowMultipleComponent]
public class LabStageCell : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private LabModalController controller;
    [SerializeField] private int rowIndex;
    [SerializeField] private int stageIndex;

    public void Bind(LabModalController c, int row, int stage)
    {
        controller = c; rowIndex = row; stageIndex = stage;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (controller == null) return;
        bool isRight = eventData.button == PointerEventData.InputButton.Right;
        controller.OnStageCellClicked(rowIndex, stageIndex, isRight);
    }
}
