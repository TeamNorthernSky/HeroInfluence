using UnityEngine;

/// <summary>
/// [JC 260628] 기능 번들 루트에 부착. 번들 내 Buttons/Modals/Tooltips 그룹을 런타임에 캔버스 레이어로
/// reparent하여 sort order를 보장한다. 편집 시엔 한 프리팹으로 묶여 있고(일원화), 런타임에만 분산.
/// 비어 있는 그룹 참조는 건너뛴다.
/// </summary>
[DisallowMultipleComponent]
public class LobbyUIModulePlacer : MonoBehaviour
{
    [Tooltip("backdrop(배경 프레임 등) — 최하단 Layer_Background로. 보통 UI_Shell만 사용.")]
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
        // [JC 260629] 자식들을 레이어로 순서 보존하며 이동. GetChild(0)를 반복 이동(append)해야 원래 형제 순서가 유지됨.
        // (역순 for + SetParent는 순서를 뒤집어 Background가 최상단으로 와 다른 UI를 가리는 버그가 있었음.)
        while (group.childCount > 0)
            group.GetChild(0).SetParent(layer, false);
    }
}
