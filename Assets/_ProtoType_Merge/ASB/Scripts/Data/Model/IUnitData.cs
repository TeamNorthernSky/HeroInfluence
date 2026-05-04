public interface IUnitData
{
    string unitId { get; }
    string unitName { get; }
    float maxHp { get; }
    float attack { get; }
    float defense { get; }
    float speed { get; }
    TeamType teamType { get; }
}
