using System;
using System.Collections.Generic;

/// <summary>
/// (ZoneId, BattleKey) → ITutorialBattleFlow factory. 코드 기반 선택(A2 확정).
/// 매 전투 새 인스턴스 생성(런타임 상태 오염 방지). ZoneId -1은 와일드카드.
/// </summary>
public sealed class TutorialBattleFlowRegistry
{
    private readonly Dictionary<(int zoneId, string battleKey), Func<ITutorialBattleFlow>> map =
        new Dictionary<(int, string), Func<ITutorialBattleFlow>>();

    /// <summary>중복 키/빈 키는 거부(false 반환).</summary>
    public bool Register(int zoneId, string battleKey, Func<ITutorialBattleFlow> factory)
    {
        string key = Normalize(battleKey);
        if (string.IsNullOrEmpty(key) || factory == null)
        {
            return false;
        }

        var mapKey = (zoneId, key);
        if (map.ContainsKey(mapKey))
        {
            UnityEngine.Debug.LogError($"[TutorialFlowRegistry] 중복 키 무시: zone={zoneId}, key={key}");
            return false;
        }

        map[mapKey] = factory;
        return true;
    }

    /// <summary>정확 매칭 → zone 와일드카드(-1) 순으로 조회. 없으면 null.</summary>
    public ITutorialBattleFlow Resolve(int zoneId, string battleKey)
    {
        string key = Normalize(battleKey);
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue((zoneId, key), out Func<ITutorialBattleFlow> exact))
        {
            return exact();
        }

        if (map.TryGetValue((-1, key), out Func<ITutorialBattleFlow> wildcard))
        {
            return wildcard();
        }

        return null;
    }

    private static string Normalize(string battleKey)
        => string.IsNullOrWhiteSpace(battleKey) ? string.Empty : battleKey.Trim();

    /// <summary>기본 튜토리얼 4개 등록. BattleKey/ZoneId는 콘텐츠 확정 시 각 Flow에서 교체.</summary>
    public static TutorialBattleFlowRegistry CreateDefault()
    {
        var registry = new TutorialBattleFlowRegistry();
        registry.Register(-1, "TUT_01", () => new TutorialBattle01Flow());
        registry.Register(-1, "TUT_02", () => new TutorialBattle02Flow());
        registry.Register(-1, "TUT_03", () => new TutorialBattle03Flow());
        registry.Register(-1, "TUT_04", () => new TutorialBattle04Flow());
        return registry;
    }
}
