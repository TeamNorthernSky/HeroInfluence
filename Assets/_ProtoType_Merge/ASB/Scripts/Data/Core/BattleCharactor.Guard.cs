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
