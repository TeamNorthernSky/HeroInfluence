public interface ITargetSelectableVisual
{
    BattleCharactor Owner { get; }
    void SetSelectable(bool active);
    void SetHovered(bool active);
    void Clear();
}
