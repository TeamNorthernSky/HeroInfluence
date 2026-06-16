using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [JC 260617] 훈련실 강화 단계 셀 클릭 핸들러. 좌클릭=선택, 우클릭=선택 해제.
/// 단계 게이트(다음 단계만 선택 가능)는 컨트롤러가 판정한다.
/// </summary>
[DisallowMultipleComponent]
public class TrainingStageCell : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TrainingModalController controller;
    [SerializeField] private int rowIndex;
    [SerializeField] private int stageIndex;

    public void Bind(TrainingModalController c, int row, int stage)
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
