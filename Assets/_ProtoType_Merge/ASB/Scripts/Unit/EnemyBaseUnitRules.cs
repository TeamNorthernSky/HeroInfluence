using System;
using System.Collections.Generic;

/// <summary>
/// 네임드 변형 적(예: 아놀드 200051)의 전투 동작 키(예: 강화병 20005)를 정한다.
/// 전투씬 이벤트 빌더(EnemySpawnPlanBuilder)만 읽는다. 데이터 키(EnemyData.Index·이름·스탯·EXP)는 바꾸지 않고
/// 전투 동작 키(프리팹·AI·스킬 번호/키·Timeline)만 결정한다. 외형·연출 전용 자산이 생기면 해당 줄을 지운다.
/// 20007은 고유 데이터/스탯을 유지하면서 20003의 전투 동작과 스킬 데이터를 사용한다.
/// 20006처럼 외형만 공유하고 스킬·AI가 다른 변형은 여기에 넣지 않는다.
/// </summary>
public static class EnemyBaseUnitRules
{
    // 키 = 변형 유닛 데이터 인덱스, 값 = 전투 동작 키(모두 FV 없는 숫자 키).
    private static readonly Dictionary<string, string> BaseUnitKeyByVariant =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "200051", "20005" }, // 아놀드
            { "200052", "20005" }, // 로니
            { "200053", "20005" }, // 은행 강도 두목
            { "20007", "20003" },   // 인질극 방패병: 스탯은 자기 행, 전투 동작은 방패병
        };

    /// <summary>번호만 재지정하는 네임드 변형과 달리 기준 유닛의 스킬 행 전체를 사용하는 변형.</summary>
    public static bool UsesBaseSkillTemplates(string unitKey) =>
        string.Equals(BattleScenarioUnitKey.Normalize(unitKey), "20007", StringComparison.Ordinal);

    /// <summary>매핑된 변형 유닛이면 전투 동작 키를 돌려준다. "FV200051"/"200051" 모두 허용.</summary>
    public static bool TryGetBaseUnitKey(string unitKey, out string baseUnitKey)
    {
        baseUnitKey = string.Empty;
        string normalized = BattleScenarioUnitKey.Normalize(unitKey);
        return !string.IsNullOrEmpty(normalized) &&
               BaseUnitKeyByVariant.TryGetValue(normalized, out baseUnitKey);
    }
}
