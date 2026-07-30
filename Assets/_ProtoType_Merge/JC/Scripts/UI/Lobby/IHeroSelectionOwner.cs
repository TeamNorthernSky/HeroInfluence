/// <summary>
/// [KJ 260728] 영웅 선택 상태를 소유하는 시설 모달 컨트롤러가 구현.
/// HeroSlotDeselect가 GetComponentInParent로 해석해 우클릭 해제를 위임한다.
/// </summary>
public interface IHeroSelectionOwner
{
    /// <summary>선택된 영웅을 해제하고 최초 상태로 되돌린다.</summary>
    void ClearHeroSelection();
}
