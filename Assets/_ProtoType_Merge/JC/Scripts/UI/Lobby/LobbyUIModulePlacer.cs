using UnityEngine;

/// <summary>
/// [JC 260628] 기능 번들 루트에 부착. 번들 내 Buttons/Modals/Tooltips 그룹을 런타임에 캔버스 레이어로
/// reparent하여 sort order를 보장한다. 편집 시엔 한 프리팹으로 묶여 있고(일원화), 런타임에만 분산.
/// 비어 있는 그룹 참조는 건너뛴다.
/// </summary>
[DisallowMultipleComponent]
public class LobbyUIModulePlacer : MonoBehaviour
{
    [SerializeField] private Transform buttonsGroup;
    [SerializeField] private Transform modalsGroup;
    [SerializeField] private Transform tooltipsGroup;

    public void Place(Transform baseLayer, Transform modalLayer, Transform tooltipLayer)
    {
        Move(buttonsGroup, baseLayer);
        Move(modalsGroup, modalLayer);
        Move(tooltipsGroup, tooltipLayer);
    }

    private static void Move(Transform group, Transform layer)
    {
        if (group == null || layer == null) return;
        // 자식들을 레이어로 옮긴다(그룹 컨테이너 자체는 빈 채로 남아 무해). full-stretch 동일 → 위치 보존.
        for (int i = group.childCount - 1; i >= 0; i--)
            group.GetChild(i).SetParent(layer, false);
    }
}
