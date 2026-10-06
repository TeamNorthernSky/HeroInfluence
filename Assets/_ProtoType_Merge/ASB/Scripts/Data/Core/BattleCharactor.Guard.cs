using System.Collections.Generic;

// 대신 맞기 버프는 보호 대상에게 저장한다. source가 현재 보호자이며 버프 하나가 공격 1회를 뜻한다.
public partial class BattleCharactor
{
    public BattleCharactor GuardSource
    {
        get
        {
            StatusEffectInstance effect = Effects.GetStatusEffect(StatusEffectType.guarded);
            BattleCharactor source = effect?.source;
            return source != null && source != this && !source.IsDead && !source.IsIncapacitated
                && !IsDead && !IsIncapacitated && source.IsPlayer == IsPlayer ? source : null;
        }
    }

    public void RemoveGuardedFromSource(BattleCharactor source)
    {
        if (Effects.RemoveStatusEffectFromSource(StatusEffectType.guarded, source))
            RecalculateStats(applyCurrentHpClamp: true);
    }

    // [호환] JC/BattleTesting/Compatibility(전투 코드 복사본)가 옛 보호자 소유 API를 부른다.
    // 같은 guarded 상태이상으로 위임하므로 판정은 신규 경로와 같다. 신규 코드는 GuardSkillHandler의 상태이상 결과를 쓴다.
    private BattleCharactor legacyGuardedAlly; // ClearGuard가 참여자 목록 없이 해제할 수 있게 마지막 보호 대상만 기억

    public void BeginGuard(BattleCharactor ally)
    {
        if (ally == null || ally == this) return;
        ally.ApplyStatusEffect(new StatusEffectInstance
        {
            effectType = StatusEffectType.guarded,
            category = StatusEffectCategory.buff,
            remainingTurns = 1,
            source = this
        });
        legacyGuardedAlly = ally;
    }

    public void ClearGuard()
    {
        if (legacyGuardedAlly != null) legacyGuardedAlly.RemoveGuardedFromSource(this);
        legacyGuardedAlly = null;
    }
}

public static class GuardLink
{
    public static BattleCharactor FindGuardianFor(BattleCharactor ward, IReadOnlyList<BattleCharactor> candidates)
    {
        if (ward == null || candidates == null) return null;

        BattleCharactor source = ward.GuardSource;
        if (source != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == source) return source;
            }
        }

        // 사망·무력화·전투 이탈 등으로 보호자가 유효하지 않으면 표시도 즉시 정리한다.
        if (ward.HasStatusEffect(StatusEffectType.guarded))
            ward.RemoveStatusEffect(StatusEffectType.guarded);
        return null;
    }

    public static void ExpireFromSource(BattleCharactor source, IReadOnlyList<BattleCharactor> candidates)
    {
        if (source == null || candidates == null) return;
        for (int i = 0; i < candidates.Count; i++)
        {
            BattleCharactor ward = candidates[i];
            if (ward != null) ward.RemoveGuardedFromSource(source);
        }
    }
}
