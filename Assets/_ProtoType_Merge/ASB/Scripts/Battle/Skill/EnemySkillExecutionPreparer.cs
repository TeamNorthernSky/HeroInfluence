using System.Collections.Generic;

/// <summary>
/// 실제 적 턴과 프리뷰가 동일한 실행용 SkillData 복사본을 사용하도록 만드는 공용 준비기입니다.
/// 원본 SkillData는 변경하지 않습니다.
/// </summary>
public static class EnemySkillExecutionPreparer
{
    public static SkillData Prepare(SkillData source)
    {
        if (source == null)
        {
            return null;
        }

        SkillData copy = Clone(source);
        if (!NeedsAnimationFallback(copy))
        {
            return copy;
        }

        if (string.IsNullOrWhiteSpace(copy.AnimationTrigger))
        {
            copy.AnimationTrigger = "Attack";
        }

        if (copy.HitDelay <= 0f)
        {
            copy.HitDelay = 0.25f;
        }

        if (copy.TotalDelay <= 0f)
        {
            copy.TotalDelay = 0.5f;
        }

        if (string.IsNullOrWhiteSpace(copy.TargetAnimationTrigger))
        {
            copy.TargetAnimationTrigger = "Hit";
        }

        copy.UseAnimEvent = false;
        return copy;
    }

    private static bool NeedsAnimationFallback(SkillData skill)
    {
        return skill != null &&
               (string.IsNullOrWhiteSpace(skill.AnimationTrigger)
                || string.IsNullOrWhiteSpace(skill.TargetAnimationTrigger)
                || skill.HitDelay <= 0f
                || skill.TotalDelay <= 0f);
    }

    private static SkillData Clone(SkillData source)
    {
        return new SkillData
        {
            skillIndex = source.skillIndex,
            skillKey = source.skillKey,
            category = source.category,
            slot = source.slot,
            skillClass = source.skillClass,
            acquireLevel = source.acquireLevel,
            skillName = source.skillName,
            description = source.description,
            ipCost = source.ipCost,
            classSkillEffect = source.classSkillEffect,
            classSkillRange = source.classSkillRange,
            EnemySkill1Range = source.EnemySkill1Range,
            EnemySkill2Range = source.EnemySkill2Range,
            classSkillRangeLine = source.classSkillRangeLine,
            classSkillTarget = source.classSkillTarget,
            boundary = source.boundary != null
                ? new List<int>(source.boundary)
                : new List<int>(),
            multiTargetType = source.multiTargetType,
            multiTargetCount = source.multiTargetCount,
            skillValue = source.skillValue,
            skillSubValue = source.skillSubValue,
            AnimationTrigger = source.AnimationTrigger,
            StateName = source.StateName,
            UseAnimEvent = source.UseAnimEvent,
            HitDelay = source.HitDelay,
            TotalDelay = source.TotalDelay,
            TargetAnimationTrigger = source.TargetAnimationTrigger
        };
    }
}
