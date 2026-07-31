using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// 스킬 타깃 진영/행동 분류.
/// 연출·타깃수집은 이 카테고리와 무관하게 공통 경로를 쓰고, 피해 해결만 분기한다.
/// </summary>
public enum UnitTargetCategory
{
    Combatant, // BattleCharactor(적/아군) — 풀 전투(크리/반격/상태이상/피격애니)
    Hostage    // HostageBattleActor — 아군 피해만(ApplyFriendlyDamage), 전투 규칙 미적용
}

/// <summary>
/// 스킬 타깃의 공통 최소 표면.
/// 연출/투사체/타깃수집이 BattleCharactor 구체 타입에 의존하지 않도록 하는 seam이다.
/// 피해 해결은 <see cref="TargetCategory"/>로 분기한다(전투유닛=CommitDamage, 인질=ApplyFriendlyDamage).
/// 인질을 BattleCharactor로 만들거나 턴/적AI 목록에 편입하지 않기 위해, 공통은 이 인터페이스로만 노출한다.
/// </summary>
public interface ISkillTarget
{
    /// <summary>진영/행동 분류. 피해 해결 분기와 타깃 필터에 사용.</summary>
    UnitTargetCategory TargetCategory { get; }

    /// <summary>연출/회전/투사체 앵커로 쓰는 루트 Transform.</summary>
    Transform TargetTransform { get; }

    /// <summary>조준/임팩트 목표 지점(월드 좌표).</summary>
    Vector3 TargetPosition { get; }

    /// <summary>현재 유효한 타깃인지. 전투유닛=!IsDead, 인질=IsSafe.</summary>
    bool IsValidSkillTarget { get; }

    /// <summary>점유 그리드 셀. AoE 중심/사거리 계산에 사용(없으면 null).</summary>
    GridCellRef TargetCell { get; }
}
