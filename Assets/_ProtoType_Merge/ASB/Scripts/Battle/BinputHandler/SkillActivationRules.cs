using System.Collections.Generic;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;

public enum SkillActivationKind { Unit, Column, Side, Self }

/// <summary>발동 클릭의 의미를 효과 범위와 분리합니다. 빈 칸은 열/진영 입력에서만 허용합니다.</summary>
public static class SkillActivationRules
{
    public static bool RequiresReviveTarget(BattleCharactor actor, SkillData skill)
    {
        if (!HeroSkillRules.IsFamily(skill, 4040) || actor == null || actor.HasUsedRevive) return false;
        foreach (var unit in Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (unit != null && unit != actor && unit.IsPlayer == actor.IsPlayer && unit.IsDead) return true;
        return false;
    }

    public static SkillActivationKind Kind(BattleCharactor actor, SkillData skill)
    {
        if (skill == null) return SkillActivationKind.Unit;
        if (skill.skillKey == "HCS003") return SkillActivationKind.Self;
        if (HeroSkillRules.IsFamily(skill, 4040))
            return RequiresReviveTarget(actor, skill) ? SkillActivationKind.Unit : SkillActivationKind.Side;
        if (HeroSkillRules.IsFamily(skill, 2030)) return SkillActivationKind.Column;
        if (skill.classSkillTarget == 2 || skill.skillKey == "HCS002" || skill.skillKey == "HCS005") return SkillActivationKind.Side;
        return SkillActivationKind.Unit;
    }

    public static bool TryResolveClick(BattleCharactor actor, SkillData skill, BattleCharactor unit, ASBGridCell cell, out BattleCharactor target)
    {
        target = null;
        var candidates = TargetingHelper.GetValidTargetsForSkillData(actor, skill);
        var kind = Kind(actor, skill);
        if (kind == SkillActivationKind.Unit || kind == SkillActivationKind.Self)
        {
            if (unit == null && cell != null) unit = cell.OccupyingUnit;
            if (unit != null && candidates.Contains(unit)) target = unit;
            return target != null;
        }
        if (cell == null && unit != null) cell = unit.OccupiedCell;
        if (cell == null) return false;
        // 실제 생존 대상이 있는 같은 열/진영의 유닛을 실행·연출 앵커로 사용합니다.
        candidates.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        foreach (var candidate in candidates)
        {
            var occupied = candidate.OccupiedCell;
            if (occupied == null || (occupied.Coords.x >= 2) != (cell.Coords.x >= 2)) continue;
            if (kind == SkillActivationKind.Column && occupied.Coords.x != cell.Coords.x) continue;
            target = candidate;
            return true;
        }
        return false;
    }
}
