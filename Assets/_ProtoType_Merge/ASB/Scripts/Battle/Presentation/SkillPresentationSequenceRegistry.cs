using System;
using System.Collections;
using System.Collections.Generic;
using ASB.Work.Battle.Core;

/// <summary>
/// 고정 페이즈 골격에 안 맞는 "특이 스킬"의 커스텀 연출 시퀀스. skillIndex로 등록한다.
/// 로직용 SkillExecutionRegistry와 별도 — 로직 예외와 연출 예외는 변경 이유가 다르므로 분리한다.
/// </summary>
public interface ISkillPresentationSequence
{
    IEnumerator Run(
        BattleManager battleManager,
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skill,
        Func<BattleHitResult> onHitCallback);
}

/// <summary>
/// skillIndex → 커스텀 연출 시퀀스 매핑. 등록이 없으면 기본 데이터 시퀀서(RunSkillSequenceCore)가 사용된다.
/// </summary>
public static class SkillPresentationSequenceRegistry
{
    // §5-9 skillKey(string) 기반. 현재 등록 없음(항상 기본 시퀀서).
    private static readonly Dictionary<string, ISkillPresentationSequence> _map =
        new Dictionary<string, ISkillPresentationSequence>();

    public static void Register(string skillKey, ISkillPresentationSequence sequence)
    {
        if (sequence != null && !string.IsNullOrEmpty(skillKey))
        {
            _map[skillKey] = sequence;
        }
    }

    public static bool TryGet(string skillKey, out ISkillPresentationSequence sequence)
    {
        if (string.IsNullOrEmpty(skillKey))
        {
            sequence = null;
            return false;
        }
        return _map.TryGetValue(skillKey, out sequence);
    }

    public static void Clear() => _map.Clear();
}
