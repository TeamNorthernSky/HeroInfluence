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
                return new HashSet<BattleCharactor>(GetValidTargetsForSkillData(actor, actor.SelectedSkillData));

            case PendingActionType.WeaponSkill:
                if (actor.EquippedWeaponData == null)
                {
                    return new HashSet<BattleCharactor>();
                }
                return new HashSet<BattleCharactor>(GetValidTargetsForSkillData(actor, actor.EquippedWeaponData.ToSkillData()));

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

    public static List<BattleCharactor> GetValidTargetsForSkillData(BattleCharactor actor, SkillData skill)
    {
        var result = new List<BattleCharactor>();
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

        // [2단계] 우선순위는 classSkillRangeLine만 사용합니다.
        // 부활 스킬은 사망자가 필요하므로 생존 필터(GetAllValidTargets)를 패스합니다.
        List<BattleCharactor> baseTargets = isRevive ? stage1 : GetAllValidTargets(stage1);
        List<BattleCharactor> finalTargets = ApplyPriorityFilter(actor, baseTargets, skill);

        for (int i = 0; i < finalTargets.Count; i++)
        {
            BattleCharactor target = finalTargets[i];
            if (target != null && !result.Contains(target))
            {
                result.Add(target);
            }
        }

        return result;
    }

    private static List<BattleCharactor> ApplyPriorityFilter(BattleCharactor actor, List<BattleCharactor> baseTargets, SkillData skill)
    {
        if (baseTargets == null || baseTargets.Count == 0)
        {
            return new List<BattleCharactor>();
        }

        if (skill == null)
        {
            return GetAllValidTargets(baseTargets);
        }

        switch (skill.classSkillRangeLine)
        {
            case 0: // FrontFirst
            {
                List<BattleCharactor> frontTargets = GetFrontTargets(baseTargets);
                return frontTargets.Count > 0 ? frontTargets : baseTargets;
            }

            case 1: // Any
            {
                return baseTargets;
            }

            case 2: // BackFirst
            {
                List<BattleCharactor> backTargets = GetBackTargets(baseTargets);
                return backTargets.Count > 0 ? backTargets : baseTargets;
            }

            case 3: // CasterPositionBased
            {
                if (actor == null)
                {
                    return baseTargets;
                }

                // 시전자가 전열이면 전열 대상만 허용(없으면 fallback 전체).
                // 시전자가 후열이면 전열/후열 구분 없이 전체 허용.
                bool isCasterFront = actor.IsInFrontRow;
                if (!isCasterFront)
                {
                    return baseTargets;
                }

                var frontOnly = new List<BattleCharactor>();
                for (int i = 0; i < baseTargets.Count; i++)
                {
                    BattleCharactor target = baseTargets[i];
                    if (target == null || target.IsDead)
                    {
                        continue;
                    }

                    if (target.IsInFrontRow)
                    {
                        frontOnly.Add(target);
                    }
                }

                return frontOnly.Count > 0 ? frontOnly : baseTargets;
            }


            default:
            {
                return baseTargets;
            }
        }
    }

    /// <summary>
    /// 입력된 후보 중 동적 전열 유닛만 반환합니다.
    /// 추후 "전열 우선" 타겟팅 룰의 1순위 필터로 사용합니다.
    /// </summary>
    public static List<BattleCharactor> GetFrontTargets(IEnumerable<BattleCharactor> candidates)
    {
        var result = new List<BattleCharactor>();
        if (candidates == null)
        {
            return result;
        }

        foreach (BattleCharactor unit in candidates)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }

            if (IsUnitInFrontRow(unit))
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>
    /// 입력된 후보 중 동적 후열 유닛만 반환합니다.
    /// 추후 "후열 우선" 타겟팅 룰의 1순위 필터로 사용합니다.
    /// </summary>
    public static List<BattleCharactor> GetBackTargets(IEnumerable<BattleCharactor> candidates)
    {
        var result = new List<BattleCharactor>();
        if (candidates == null)
        {
            return result;
        }

        foreach (BattleCharactor unit in candidates)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }

            if (IsUnitInBackRow(unit))
            {
                result.Add(unit);
            }
        }

        return result;
    }

    /// <summary>
    /// null/사망 제거만 적용된 기본 후보 목록입니다.
    /// 전열/후열 우선 대상이 없을 때 fallback으로 재사용할 수 있습니다.
    /// </summary>
    public static List<BattleCharactor> GetAllValidTargets(IEnumerable<BattleCharactor> candidates)
    {
        var result = new List<BattleCharactor>();
        if (candidates == null)
        {
            return result;
        }

        foreach (BattleCharactor unit in candidates)
        {
            if (unit == null || unit.IsDead)
            {
                continue;
            }

            result.Add(unit);
        }

        return result;
    }

    /// <summary>
    /// 동적 전열 판정:
    /// - 아군(IsPlayer=true): 같은 진영 생존자 중 X 최대 행이 전열
    /// - 적군(IsPlayer=false): 같은 진영 생존자 중 X 최소 행이 전열
    /// </summary>
    public static bool IsUnitInFrontRow(BattleCharactor unit)
    {
        if (!TryGetTeamRowBounds(unit, out int minX, out int maxX))
        {
            return false;
        }

        if (!TryResolveCell(unit, out GridCellRef cell))
        {
            return false;
        }

        return unit.IsPlayer ? cell.Coords.x == maxX : cell.Coords.x == minX;
    }

    /// <summary>
    /// 동적 후열 판정:
    /// - 아군(IsPlayer=true): 같은 진영 생존자 중 X 최소 행이 후열
    /// - 적군(IsPlayer=false): 같은 진영 생존자 중 X 최대 행이 후열
    /// </summary>
    public static bool IsUnitInBackRow(BattleCharactor unit)
    {
        if (!TryGetTeamRowBounds(unit, out int minX, out int maxX))
        {
            return false;
        }

        if (!TryResolveCell(unit, out GridCellRef cell))
        {
            return false;
        }

        return unit.IsPlayer ? cell.Coords.x == minX : cell.Coords.x == maxX;
    }

    private static bool TryGetTeamRowBounds(BattleCharactor pivot, out int minX, out int maxX)
    {
        minX = 0;
        maxX = 0;
        if (pivot == null || pivot.IsDead)
        {
            return false;
        }

        BattleCharactor[] all = Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
        {
            return false;
        }

        bool hasAny = false;
        for (int i = 0; i < all.Length; i++)
        {
            BattleCharactor unit = all[i];
            if (unit == null || unit.IsDead || unit.IsPlayer != pivot.IsPlayer)
            {
                continue;
            }

            if (!TryResolveCell(unit, out GridCellRef cell))
            {
                continue;
            }

            int x = cell.Coords.x;
            if (!hasAny)
            {
                minX = x;
                maxX = x;
                hasAny = true;
                continue;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
        }

        return hasAny;
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
