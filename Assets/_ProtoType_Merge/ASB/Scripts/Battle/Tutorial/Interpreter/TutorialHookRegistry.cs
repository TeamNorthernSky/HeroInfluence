using System;
using System.Collections.Generic;

/// <summary>CustomHook 액션이 호출할 코드 핸들러 모음. 데이터로 못 담는 특수 로직 탈출구.</summary>
public sealed class TutorialHookRegistry
{
    private readonly Dictionary<string, Func<TutorialExecutionContext, TutorialActionResult>> hooks =
        new Dictionary<string, Func<TutorialExecutionContext, TutorialActionResult>>(StringComparer.Ordinal);

    public void Add(string hookId, Func<TutorialExecutionContext, TutorialActionResult> handler)
    {
        if (string.IsNullOrWhiteSpace(hookId) || handler == null)
        {
            return;
        }

        hooks[hookId.Trim()] = handler;
    }

    public bool TryInvoke(string hookId, TutorialExecutionContext ctx, out TutorialActionResult result)
    {
        result = TutorialActionResult.FailedContinue;
        if (string.IsNullOrWhiteSpace(hookId) || !hooks.TryGetValue(hookId.Trim(), out var handler))
        {
            return false;
        }

        result = handler(ctx);
        return true;
    }
}

/// <summary>
/// 튜토리얼별 훅 등록 지점. (ZoneId, BattleKey)로 조회. 특수 로직이 필요한 튜토리얼만 여기서 hookId→핸들러 등록.
/// </summary>
public static class TutorialHookProvider
{
    public static TutorialHookRegistry Build(int zoneId, string battleKey)
    {
        var registry = new TutorialHookRegistry();

        // TODO(콘텐츠): 특수 로직 튜토리얼만 등록. 예)
        // if (string.Equals(battleKey, "TUT_03", StringComparison.Ordinal))
        //     registry.Add("boss_enrage", ctx => { /* ctx.Host 로 런타임 접근, 효과는 Host.Track(scopeId) */ return TutorialActionResult.Success; });

        return registry;
    }
}
