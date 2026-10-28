using System.Collections.Generic;

// 대신 맞기 버프는 보호 대상에게 시전 순서대로 저장한다. source가 보호자이며 버프 하나가 공격 1회를 뜻한다.
public partial class BattleCharactor
{
    public BattleCharactor GuardSource
    {
        get
        {
            if (IsDead || IsIncapacitated) return null;
            foreach (StatusEffectInstance effect in ActiveStatusEffects)
            {
                if (effect == null || effect.effectType != StatusEffectType.guarded
                    || effect.remainingTurns <= 0) continue;

                BattleCharactor source = effect.source;
                if (source != null && source != this && !source.IsDead && !source.IsIncapacitated
                    && source.IsPlayer == IsPlayer)
                    return source;
            }

            return null;
        }
    }

    public bool RemoveGuardEffect(StatusEffectInstance effect) => Effects.RemoveGuardEffect(effect);

    public bool ConsumeGuardFrom(BattleCharactor source)
    {
        if (source == null) return false;
        foreach (StatusEffectInstance effect in ActiveStatusEffects)
        {
            if (effect != null && effect.effectType == StatusEffectType.guarded
                && effect.remainingTurns > 0 && effect.source == source)
                return RemoveGuardEffect(effect);
        }

        return false;
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

        if (ward.IsDead || ward.IsIncapacitated) return null;

        // 앞에서부터 검사하여 오래된 유효 버프를 우선 사용한다. 무효 항목만 개별 제거한다.
        for (int i = 0; i < ward.ActiveStatusEffects.Count;)
        {
            StatusEffectInstance effect = ward.ActiveStatusEffects[i];
            if (effect == null || effect.effectType != StatusEffectType.guarded)
            {
                i++;
                continue;
            }

            BattleCharactor source = effect.source;
            bool valid = effect.remainingTurns > 0 && source != null && source != ward
                && !source.IsDead && !source.IsIncapacitated && source.IsPlayer == ward.IsPlayer;
            if (valid)
            {
                for (int j = 0; j < candidates.Count; j++)
                    if (candidates[j] == source) return source;
            }

            ward.RemoveGuardEffect(effect);
        }
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
