/// <summary>
/// 전투 유닛의 런타임 식별자(UnitData.Index 등)를 노출합니다.
/// 유효하지 않을 때는 빈 문자열을 반환하는 것을 권장합니다.
/// </summary>
public interface IUnitIdentifier
{
    string UnitID { get; }
}
