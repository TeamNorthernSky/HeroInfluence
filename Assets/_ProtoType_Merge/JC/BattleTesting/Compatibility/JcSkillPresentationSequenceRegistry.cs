// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Presentation/SkillPresentationSequenceRegistry.cs
// 원본 객체: SkillPresentationSequenceRegistry -> JcSkillPresentationSequenceRegistry
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System;
using System.Collections;
using System.Collections.Generic;
using ASB.Work.Battle.Core;

/// <summary>
/// 고정 페이즈 골격에 안 맞는 "특이 스킬"의 커스텀 연출 시퀀스. skillIndex로 등록한다.
/// 로직용 SkillExecutionRegistry와 별도 — 로직 예외와 연출 예외는 변경 이유가 다르므로 분리한다.
/// </summary>
public interface JcISkillPresentationSequence
{
    IEnumerator Run(
        JcBattleManager battleManager,
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skill,
        Func<BattleHitResult> onHitCallback);
}

/// <summary>
/// skillIndex → 커스텀 연출 시퀀스 매핑. 등록이 없으면 기본 데이터 시퀀서(RunSkillSequenceCore)가 사용된다.
/// </summary>
public static class JcSkillPresentationSequenceRegistry
{
    // §5-9 skillKey(string) 기반. 현재 등록 없음(항상 기본 시퀀서).
    private static readonly Dictionary<string, JcISkillPresentationSequence> _map =
        new Dictionary<string, JcISkillPresentationSequence>();

    // 원본 함수 대응: SkillPresentationSequenceRegistry.Register (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Presentation/SkillPresentationSequenceRegistry.cs)

    public static void Register(string skillKey, JcISkillPresentationSequence sequence)
    {
        if (sequence != null && !string.IsNullOrEmpty(skillKey))
        {
            _map[skillKey] = sequence;
        }
    }

    // 원본 함수 대응: SkillPresentationSequenceRegistry.TryGet (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Presentation/SkillPresentationSequenceRegistry.cs)

    public static bool TryGet(string skillKey, out JcISkillPresentationSequence sequence)
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
