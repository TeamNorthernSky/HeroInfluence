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
/// 광역(BaseAoESkillHandler) 스킬의 커스텀 연출 시퀀스. 연출은 1회, 피해 확정은 applyHits 한 번으로
/// 모든 대상의 hit callback을 실행한다(호출 측이 1회 가드). 부르지 못하고 끝나도 규칙 계층 스윕이 확정한다.
/// </summary>
public interface IAoESkillPresentationSequence
{
    IEnumerator Run(
        BattleManager battleManager,
        BattleCharactor actor,
        BattleCharactor primaryTarget,
        SkillData skill,
        IReadOnlyList<BattleCharactor> targets,
        Action applyHits);
}

/// <summary>
/// skillKey → 커스텀 연출 시퀀스 매핑. 등록이 없으면 기본 데이터 시퀀서(RunSkillSequenceCore / RunAoESkillSequence)가 사용된다.
/// </summary>
public static class SkillPresentationSequenceRegistry
{
    // §5-9 skillKey(string) 기반. 단일 대상 기본 등록 없음(항상 기본 시퀀서).
    private static readonly Dictionary<string, ISkillPresentationSequence> _map =
        new Dictionary<string, ISkillPresentationSequence>();

    private static readonly Dictionary<string, IAoESkillPresentationSequence> _aoeMap =
        new Dictionary<string, IAoESkillPresentationSequence>();

    // 첫 TryGet 전에 반드시 실행된다. 시퀀스 클래스 쪽 정적 코드는 그 타입이 참조되기 전엔 실행되지 않으므로 여기서 등록한다.
    static SkillPresentationSequenceRegistry()
    {
        RegisterBuiltIns();
    }

    private static void RegisterBuiltIns()
    {
        RegisterEnemyAoE("FV40005", 1, new HoopDashPresentationSequence()); // 절망의 굴렁쇠: 돌진+행광역+자폭
    }

    // 카탈로그 키(FV40005_1)와 이벤트 전투 숫자 키(40005_1) 모두 등록(SkillExecutionRegistry.RegisterEnemy와 동일).
    private static void RegisterEnemyAoE(string enemyKey, int slot, IAoESkillPresentationSequence sequence)
    {
        string catalogKey = EnemySkillKeyRules.Compose(enemyKey, slot);
        RegisterAoE(catalogKey, sequence);

        string eventKey = EnemySkillKeyRules.ToNumericKey(catalogKey);
        if (eventKey != catalogKey)
        {
            RegisterAoE(eventKey, sequence);
        }
    }

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

    public static void RegisterAoE(string skillKey, IAoESkillPresentationSequence sequence)
    {
        if (sequence != null && !string.IsNullOrEmpty(skillKey))
        {
            _aoeMap[skillKey] = sequence;
        }
    }

    public static bool TryGetAoE(string skillKey, out IAoESkillPresentationSequence sequence)
    {
        if (string.IsNullOrEmpty(skillKey))
        {
            sequence = null;
            return false;
        }
        return _aoeMap.TryGetValue(skillKey, out sequence);
    }

    /// <summary>기본 등록 상태로 리셋한다. 두 맵을 비운 뒤 기본 등록(RegisterBuiltIns)을 다시 실행한다.</summary>
    public static void Clear()
    {
        _map.Clear();
        _aoeMap.Clear();
        RegisterBuiltIns();
    }
}
