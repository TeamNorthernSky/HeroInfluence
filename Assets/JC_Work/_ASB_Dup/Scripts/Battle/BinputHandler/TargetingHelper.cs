using System.Collections.Generic;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;
using GridManagerRef = ASB.Work.BattleGrid.GridManager;

public static class TargetingHelper
{
    private const int ClassSkillEffect_Heal = 1;
    private const int ClassSkillEffect_Revive = 2;

    public static HashSet<BattleCharactor> GetValidTargets(BattleCharactor actor, PendingActionType actionType)
    {
        if (actor == null || actor.IsDead || actionType == PendingActionType.None)
        {
            return new HashSet<BattleCharactor>();
        }

        switch (actionType)
        {
            case PendingActionType.BasicAttack:
                return GetAliveEnemyUnits(actor);

            case PendingActionType.ClassSkill:
                actor.ResolveSelectedSkill(false);
                return GetValidTargetsForSkillData(actor, actor.SelectedSkillData);

            case PendingActionType.WeaponSkill:
                if (actor.EquippedWeaponData == null)
                {
                    return new HashSet<BattleCharactor>();
                }
                return GetValidTargetsForSkillData(actor, actor.EquippedWeaponData.ToSkillData());

            default:
                return new HashSet<BattleCharactor>();
        }
    }

    public static bool IsStillValidTarget(BattleCharactor actor, PendingActionType actionType, BattleCharactor target)
    {
        if (target == null || actor == null || actor.IsDead)
        {
            return false;
        }

        HashSet<BattleCharactor> latest = GetValidTargets(actor, actionType);
        return latest.Contains(target);
    }

    private static HashSet<BattleCharactor> GetAliveEnemyUnits(BattleCharactor actor)
    {
        var result = new HashSet<BattleCharactor>();
        if (actor == null || actor.IsDead)
        {
            return result;
        }

        BattleCharactor[] all = Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
        {
            return result;
        }

        for (int i = 0; i < all.Length; i++)
        {
            BattleCharactor unit = all[i];
            if (unit == null || unit == actor || unit.IsDead || unit.CurrentHp <= 0f)
            {
                continue;
            }

            if (unit.IsPlayer == actor.IsPlayer)
            {
                continue;
            }

            result.Add(unit);
        }

        return result;
    }

    private static HashSet<BattleCharactor> GetValidTargetsForSkillData(BattleCharactor actor, SkillData skill)
    {
        var result = new HashSet<BattleCharactor>();
        if (actor == null || actor.IsDead || skill == null)
        {
            return result;
        }

        BattleCharactor[] all = Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
        {
            return result;
        }

        bool isHeal = skill.classSkillEffect == ClassSkillEffect_Heal;
        bool isRevive = skill.classSkillEffect == ClassSkillEffect_Revive;

        // [1단계] 진영 + 스킬 성격 1차 필터링
        // - 부활: 죽은 아군만 (적군·생존자 제외)
        // - 힐: 아군 생존자(자기 자신 포함)
        // - 공격: 적군 생존자(자기 자신 제외)
        var stage1 = new List<BattleCharactor>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            BattleCharactor unit = all[i];
            if (unit == null)
            {
                continue;
            }

            if (isRevive)
            {
                if (!unit.IsDead)
                {
                    continue;
                }

                if (unit.IsPlayer != actor.IsPlayer)
                {
                    continue;
                }

                stage1.Add(unit);
                continue;
            }

            if (unit.IsDead || unit.CurrentHp <= 0f)
            {
                continue;
            }

            if (isHeal)
            {
                if (unit.IsPlayer != actor.IsPlayer)
                {
                    continue;
                }
            }
            else
            {
                if (unit == actor)
                {
                    continue;
                }

                if (unit.IsPlayer == actor.IsPlayer)
                {
                    continue;
                }
            }

            stage1.Add(unit);
        }

        // [2단계] 사거리(boundary) 2차 필터링: 사거리 내 후보만 남김
        var inRange = new List<BattleCharactor>(stage1.Count);
        for (int i = 0; i < stage1.Count; i++)
        {
            BattleCharactor unit = stage1[i];
            if (IsWithinBoundary(actor, unit, skill))
            {
                inRange.Add(unit);
            }
        }

        // [3단계] 근접(사거리 0) 공격은 전열(x==0) 우선 (부활·힐에는 적용하지 않음)
        if (!isHeal && !isRevive && skill.classSkillRange == 0)
        {
            bool hasFront = false;
            for (int i = 0; i < inRange.Count; i++)
            {
                if (inRange[i] != null && inRange[i].IsInFrontRow)
                {
                    hasFront = true;
                    break;
                }
            }

            if (hasFront)
            {
                for (int i = inRange.Count - 1; i >= 0; i--)
                {
                    BattleCharactor unit = inRange[i];
                    if (unit == null || !unit.IsInFrontRow)
                    {
                        inRange.RemoveAt(i);
                    }
                }
            }
        }

        for (int i = 0; i < inRange.Count; i++)
        {
            result.Add(inRange[i]);
        }

        return result;
    }

    private static bool IsWithinBoundary(BattleCharactor actor, BattleCharactor target, SkillData skill)
    {
        if (skill == null || skill.boundary == null || skill.boundary.Count == 0)
        {
            return true;
        }

        if (!TryResolveCell(actor, out GridCellRef actorCell) || !TryResolveCell(target, out GridCellRef targetCell))
        {
            return false;
        }

        Vector2Int relative = targetCell.Coords - actorCell.Coords;
        return skill.boundary.Contains(relative);
    }

    private static bool TryResolveCell(BattleCharactor unit, out GridCellRef cell)
    {
        cell = null;
        if (unit == null)
        {
            return false;
        }

        cell = unit.OccupiedCell;
        if (cell == null && GridManagerRef.Instance != null)
        {
            cell = GridManagerRef.Instance.FindCellByUnit(unit);
        }

        return cell != null;
    }
}
