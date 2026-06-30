using UnityEngine;

/// <summary>
/// [JC 260628 / 일반화 260630] 번들 루트 부착. 내부 Background/Buttons/Modals/Tooltips 그룹을
/// 런타임에 캔버스 레이어로 reparent하여 z-order 보장. 편집 시 한 프리팹, 런타임에만 분산.
/// </summary>
[DisallowMultipleComponent]
public class UIModulePlacer : MonoBehaviour
{
    [SerializeField] private Transform backgroundGroup;
    [SerializeField] private Transform buttonsGroup;
    [SerializeField] private Transform modalsGroup;
    [SerializeField] private Transform tooltipsGroup;

    public void Place(Transform backgroundLayer, Transform baseLayer, Transform modalLayer, Transform tooltipLayer)
    {
        Move(backgroundGroup, backgroundLayer);
        Move(buttonsGroup, baseLayer);
        Move(modalsGroup, modalLayer);
        Move(tooltipsGroup, tooltipLayer);
    }

    private static void Move(Transform group, Transform layer)
    {
        if (group == null || layer == null) return;
        while (group.childCount > 0)
            group.GetChild(0).SetParent(layer, false);
    }
}
