using System.Collections.Generic;
using ASB.Work.BattleGrid;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.SkillExecution;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;
using GridManagerRef = ASB.Work.BattleGrid.BattleGridManager;

/// <summary>
/// 아군 스킬의 인질(시민) 피해 규칙.
/// 타격 범위는 여기서 따로 계산하지 않고 스킬 핸들러(<see cref="BaseSkillHandler.ResolveAreaCells"/>,
/// <see cref="TargetAroundRandom"/>)가 정한 범위를 그대로 따른다. 적이 맞는 칸과 시민이 맞는 칸이 항상 같다.
/// </summary>
public static class HostageFriendlyFireResolver
{
    public static HashSet<HostageBattleActor> GetSelectableTargets(BattleCharactor actor, SkillData skill)
    {
        var result = new HashSet<HostageBattleActor>();
        if (!CanTargetHostages(actor, skill))
            return result;

        HostageScenarioController scenario = HostageScenarioController.Active;
        IReadOnlyList<HostageBattleActor> hostages = scenario.Hostages;
        for (int i = 0; i < hostages.Count; i++)
        {
            HostageBattleActor hostage = hostages[i];
            if (hostage != null && hostage.IsSafe)
                result.Add(hostage);
        }

        return result;
    }

    public static bool IsStillValidTarget(BattleCharactor actor, PendingActionType actionType, HostageBattleActor target)
    {
        if (target == null || !target.IsSafe || actor == null)
            return false;

        SkillData skill = null;
        switch (actionType)
        {
            case PendingActionType.ClassSkill:
                actor.ResolveSelectedSkill(false);
                skill = actor.SelectedSkillData;
                break;
            case PendingActionType.WeaponSkill:
                skill = actor.EquippedWeaponData != null ? actor.EquippedWeaponData.ToSkillData() : null;
                break;
        }

        return CanTargetHostages(actor, skill);
    }

    public static bool CanTargetHostages(BattleCharactor actor, SkillData skill)
    {
        return actor != null
               && actor.IsPlayer
               && !actor.IsDead
               && skill != null
               && skill.classSkillEffect == 0
               && HostageScenarioController.Active != null;
    }

    /// <summary>인질 방어력 적용 전 피해. 시전자 공격력 × 배율.</summary>
    public static float ComputeRawDamage(BattleCharactor actor, float multiplier)
    {
        return actor != null ? Mathf.Max(0f, actor.FinalStats.Atk * Mathf.Max(0.01f, multiplier)) : 0f;
    }

    /// <summary>
    /// 부수피해 칸을 <b>피해 확정 전에</b> 스냅샷한다.
    /// <para>
    /// 확정 후에 판정하면 <c>primaryTarget.IsDead</c>가 "이번 공격으로 죽었는지"를 뜻하게 되어
    /// <b>적을 죽이면 옆 인질이 무사하고, 살려두면 다치는</b> 역전이 생긴다.
    /// 시전자가 반격으로 사망하는 경우도 마찬가지라 <see cref="CanTargetHostages"/> 판정을 여기서 끝낸다.
    /// </para>
    /// 칸은 핸들러가 이번 실행에서 실제로 해석한 <see cref="SkillExecutionResult.AffectedCells"/>를 우선 쓴다.
    /// </summary>
    /// <returns>부수피해를 적용할 칸 목록, 적용하지 않으면 null.</returns>
    public static List<GridCellRef> CaptureCollateralCells(
        BattleCharactor actor,
        BattleCharactor primaryTarget,
        SkillData skill,
        SkillExecutionResult executionResult)
    {
        GridCellRef centerCell = CaptureCollateralCenter(actor, primaryTarget, skill);
        if (centerCell == null)
            return null;

        if (executionResult?.AffectedCells != null)
            return new List<GridCellRef>(executionResult.AffectedCells);

        return ResolveAffectedCells(actor, centerCell, skill);
    }

    /// <summary>부수피해 중심 칸 스냅샷. 판정 규칙은 <see cref="CaptureCollateralCells"/>와 같다.</summary>
    public static GridCellRef CaptureCollateralCenter(BattleCharactor actor, BattleCharactor primaryTarget, SkillData skill)
    {
        if (!CanTargetHostages(actor, skill) || primaryTarget == null || primaryTarget.IsDead)
            return null;

        return primaryTarget.OccupiedCell;
    }

    /// <summary>
    /// <see cref="CaptureCollateralCells"/>로 미리 잡아둔 칸의 인질에게 부수피해를 적용한다.
    /// 이 시점의 전투 상태(대상·시전자 생사)는 판정에 쓰지 않는다.
    /// </summary>
    /// <returns>피해를 받은 인질.</returns>
    public static List<HostageBattleActor> ApplyCollateralDamage(
        BattleCharactor actor,
        IReadOnlyList<GridCellRef> cells,
        SkillData skill)
    {
        var hitHostages = new List<HostageBattleActor>();
        if (actor == null || skill == null || cells == null)
            return hitHostages;

        // 적이 주 대상인 칸에는 인질이 없으므로 부수피해는 모두 '주 대상 외 범위 칸' 배율로 맞는다.
        float rawDamage = ComputeRawDamage(actor, ResolveSplashMultiplier(skill));
        for (int i = 0; i < cells.Count; i++)
        {
            HostageBattleActor hostage = cells[i] != null ? cells[i].GetComponentInChildren<HostageBattleActor>(true) : null;
            if (hostage == null || !hostage.IsSafe || hitHostages.Contains(hostage))
                continue;

            hostage.ApplyFriendlyDamage(rawDamage);
            hitHostages.Add(hostage);
        }

        return hitHostages;
    }

    /// <summary>
    /// 중심 칸 하나로 부수피해를 적용한다(호환용). 범위는 <see cref="ResolveAffectedCells"/>로 해석한다.
    /// </summary>
    public static int ApplyCollateralDamage(BattleCharactor actor, GridCellRef centerCell, SkillData skill)
    {
        if (actor == null || skill == null || centerCell == null)
            return 0;

        return ApplyCollateralDamage(actor, ResolveAffectedCells(actor, centerCell, skill), skill).Count;
    }

    /// <summary>
    /// Returns the cells that should be highlighted while a hostage is targeted.
    /// Random-around skills expose every possible splash cell, matching enemy targeting previews;
    /// the actual extra hostage is picked by <see cref="TargetAroundRandom.PickAdditionalHostages"/>.
    /// </summary>
    public static bool TryGetPreviewCells(
        HostageBattleActor primaryTarget,
        SkillData skill,
        out GridCellRef centerCell,
        out List<GridCellRef> previewCells,
        BattleCharactor actor = null)
    {
        centerCell = primaryTarget != null ? primaryTarget.GetComponentInParent<GridCellRef>() : null;
        previewCells = new List<GridCellRef>();

        GridManagerRef gridManager = GridManagerRef.Instance;
        if (centerCell == null || skill == null || gridManager == null)
            return false;

        if (SkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler) &&
            handler is TargetAroundRandom)
        {
            previewCells.Add(centerCell);
            TargetAroundRandomHelper.CollectSplashCells(
                centerCell.Coords,
                skill,
                gridManager,
                centerCell,
                previewCells);
            return true;
        }

        previewCells = ResolveAffectedCells(actor, centerCell, skill);
        return previewCells.Count > 0;
    }

    public static bool TryGetAffectedCells(
        HostageBattleActor primaryTarget,
        SkillData skill,
        out GridCellRef centerCell,
        out List<GridCellRef> affectedCells,
        BattleCharactor actor = null)
    {
        centerCell = primaryTarget != null ? primaryTarget.GetComponentInParent<GridCellRef>() : null;
        affectedCells = new List<GridCellRef>();
        if (centerCell == null || skill == null || GridManagerRef.Instance == null)
            return false;

        affectedCells = ResolveAffectedCells(actor, centerCell, skill);
        return affectedCells.Count > 0;
    }

    /// <summary>
    /// 인질이 주 대상인 스킬의 인질 피해 목록을 만든다(적용 전).
    /// 범위 칸의 인질은 Primary, TargetAroundRandom이 주변에서 뽑은 인질은 Additional(추가 배율)이다.
    /// </summary>
    public static List<HostageHitContext> BuildSkillHits(BattleCharactor actor, HostageBattleActor primaryTarget, SkillData skill)
    {
        var hits = new List<HostageHitContext>();
        if (!CanTargetHostages(actor, skill) || primaryTarget == null || !primaryTarget.IsSafe)
            return hits;

        if (!TryGetAffectedCells(primaryTarget, skill, out GridCellRef centerCell, out List<GridCellRef> cells, actor))
            return hits;

        float primaryDamage = ComputeRawDamage(actor, skill.skillValue);
        float splashDamage = ComputeRawDamage(actor, ResolveSplashMultiplier(skill));
        var seen = new HashSet<HostageBattleActor>();
        for (int i = 0; i < cells.Count; i++)
        {
            HostageBattleActor hostage = cells[i] != null ? cells[i].GetComponentInChildren<HostageBattleActor>(true) : null;
            if (hostage == null || !hostage.IsSafe || !seen.Add(hostage))
                continue;

            hits.Add(new HostageHitContext
            {
                Caster = actor,
                Hostage = hostage,
                Role = DamageRole.Primary,
                RawDamage = hostage == primaryTarget ? primaryDamage : splashDamage,
                SkillIndex = skill.skillIndex
            });
        }

        if (SkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler) &&
            handler is TargetAroundRandom aroundRandom)
        {
            float additionalDamage = ComputeRawDamage(actor, aroundRandom.ResolveAdditionalMultiplier(skill));
            List<HostageBattleActor> extras = aroundRandom.PickAdditionalHostages(actor, primaryTarget, skill, centerCell);
            for (int i = 0; i < extras.Count; i++)
            {
                HostageBattleActor hostage = extras[i];
                if (hostage == null || !hostage.IsSafe || !seen.Add(hostage))
                    continue;

                hits.Add(new HostageHitContext
                {
                    Caster = actor,
                    Hostage = hostage,
                    Role = DamageRole.Additional,
                    RawDamage = additionalDamage,
                    SkillIndex = skill.skillIndex
                });
            }
        }

        return hits;
    }

    /// <summary>인질 피해를 확정한다. 이미 부상(비안전)인 인질은 건너뛴다.</summary>
    /// <returns>피해를 받은 인질.</returns>
    public static List<HostageBattleActor> ApplyHits(IReadOnlyList<HostageHitContext> hits)
    {
        var hitHostages = new List<HostageBattleActor>();
        if (hits == null)
            return hitHostages;

        for (int i = 0; i < hits.Count; i++)
        {
            HostageHitContext hit = hits[i];
            if (hit?.Hostage == null || !hit.Hostage.IsSafe)
                continue;

            hit.Hostage.ApplyFriendlyDamage(hit.RawDamage);
            hitHostages.Add(hit.Hostage);
        }

        return hitHostages;
    }

    public static List<HostageBattleActor> ApplySkillDamage(BattleCharactor actor, HostageBattleActor primaryTarget, SkillData skill)
    {
        return ApplyHits(BuildSkillHits(actor, primaryTarget, skill));
    }

    /// <summary>
    /// 중심 칸 기준 스킬 타격 칸. 스킬 핸들러가 정한 범위를 그대로 쓴다.
    /// <list type="bullet">
    /// <item>BaseSkillHandler(단일/광역/관통 등): 핸들러의 ResolveAffectedArea 결과</item>
    /// <item>TargetAroundRandom: 중심 1칸(주변은 무작위 추가 대상 선택이 따로 담당)</item>
    /// <item>그 외(전용 Execute 핸들러·기본 경로): 중심 1칸(기본 경로는 단일 대상)</item>
    /// </list>
    /// </summary>
    public static List<GridCellRef> ResolveAffectedCells(BattleCharactor actor, GridCellRef centerCell, SkillData skill)
    {
        if (centerCell == null || skill == null)
            return new List<GridCellRef>();

        if (SkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler) &&
            handler is BaseSkillHandler areaHandler)
        {
            List<GridCellRef> cells = areaHandler.ResolveAreaCells(actor, skill, centerCell);
            if (!cells.Contains(centerCell))
                cells.Insert(0, centerCell);
            return cells;
        }

        return new List<GridCellRef> { centerCell };
    }

    /// <summary>주 대상 외 범위 칸 배율. 핸들러가 정한 값을 쓴다(예: 관통 대쉬의 뒤 칸 = skillSubValue).</summary>
    private static float ResolveSplashMultiplier(SkillData skill)
    {
        if (SkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler) &&
            handler is BaseSkillHandler areaHandler)
            return areaHandler.ResolveSplashMultiplier(skill);

        return skill.skillValue;
    }
}
