using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// BattleCharactor의 ISkillTarget 구현(연출/타깃수집 공통 경로용).
/// 전투유닛은 풀 전투 규칙(피해 해결 시 CommitDamage 경로)을 따른다.
/// 명시적 구현으로 기존 멤버명과 충돌을 피한다.
/// </summary>
public partial class BattleCharactor : ISkillTarget
{
    UnitTargetCategory ISkillTarget.TargetCategory => UnitTargetCategory.Combatant;
    Transform ISkillTarget.TargetTransform => transform;
    Vector3 ISkillTarget.TargetPosition => transform.position;
    bool ISkillTarget.IsValidSkillTarget => !IsDead;
    GridCellRef ISkillTarget.TargetCell => OccupiedCell;
}
