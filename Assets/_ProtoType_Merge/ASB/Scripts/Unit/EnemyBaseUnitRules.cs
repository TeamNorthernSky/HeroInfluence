using System;
using System.Collections.Generic;

/// <summary>
/// 네임드 변형 적(예: 아놀드 200051)의 전투 동작 키(예: 강화병 20005)를 정한다.
/// 전투씬 이벤트 빌더(EnemySpawnPlanBuilder)만 읽는다. 데이터 키(EnemyData.Index·이름·스탯·EXP)는 바꾸지 않고
/// 전투 동작 키(프리팹·AI·스킬 번호/키·Timeline)만 결정한다. 외형·연출 전용 자산이 생기면 해당 줄을 지운다.
/// 20006/20007처럼 외형만 공유하고 스킬·AI가 다른 변형은 여기에 넣지 않는다(인질 위협 판정이 Data.Index 기준).
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
        };

    /// <summary>매핑된 변형 유닛이면 전투 동작 키를 돌려준다. "FV200051"/"200051" 모두 허용.</summary>
    public static bool TryGetBaseUnitKey(string unitKey, out string baseUnitKey)
    {
        baseUnitKey = string.Empty;
        string normalized = BattleScenarioUnitKey.Normalize(unitKey);
        return !string.IsNullOrEmpty(normalized) &&
               BaseUnitKeyByVariant.TryGetValue(normalized, out baseUnitKey);
    }
}
