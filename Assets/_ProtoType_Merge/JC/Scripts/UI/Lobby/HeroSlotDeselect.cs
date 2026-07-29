using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [KJ 260728] HeroSlot 우클릭 → 영웅 선택 해제. 시설 모달의 Modal_*/Panel/HeroSlot에 부착.
/// 소유 컨트롤러는 GetComponentInParent&lt;IHeroSelectionOwner&gt;()로 해석하므로 인스펙터 결선이 필요 없다.
/// HeroSlot 자체에는 Graphic이 없지만 자식 HeroProfile의 포인터 이벤트가 상위로 전파되어 수신된다.
/// </summary>
[DisallowMultipleComponent]
public class HeroSlotDeselect : MonoBehaviour, IPointerClickHandler
{
    private IHeroSelectionOwner owner;

    private void Awake() => ResolveOwner();

    private void ResolveOwner()
    {
        if (owner != null) return;
        owner = GetComponentInParent<IHeroSelectionOwner>(true);
        if (owner == null)
            Debug.LogWarning($"[HeroSlotDeselect] 상위에서 IHeroSelectionOwner를 찾지 못했습니다. ({name})");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        ResolveOwner();
        if (owner != null) owner.ClearHeroSelection();
    }
}
