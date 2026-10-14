using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TrainingCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TrainingModalController controller;
    private int rowIndex;

    public void Bind(TrainingModalController owner, int row)
    {
        controller = owner;
        rowIndex = row;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (controller != null) controller.OnTrainingCardHover(rowIndex, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (controller != null) controller.OnTrainingCardHover(rowIndex, false);
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnTrainingCardHover(rowIndex, false);
    }
}
