using System.Collections.Generic;

/// <summary>액션 실행 결과. FailedAbortRule이면 그 규칙의 나머지 액션을 중단한다.</summary>
public enum TutorialActionResult
{
    Success,
    FailedContinue,   // 이 액션 실패했지만 규칙의 다음 액션은 계속
    FailedAbortRule,  // 규칙 나머지 액션 중단
}

/// <summary>블로킹 UI의 활성 요청. B(입력)가 이 RequestId/RequiredActionId와 일치할 때만 잠금 해제·진행.</summary>
public sealed class TutorialUiRequest
{
    public string RequestId;        // 보통 ruleId
    public string UiKey;
    public string RequiredActionId;

    public TutorialUiRequest(string requestId, string uiKey, string requiredActionId)
    {
        RequestId = requestId;
        UiKey = uiKey;
        RequiredActionId = requiredActionId;
    }
}

/// <summary>
/// Executor·CustomHook이 Flow 상태를 제어하는 표면. 실제 구현은 DataDrivenTutorialFlow.
/// 효과 수명(scopeId) 추적은 Host(Director) 확장 API를 통해 이뤄진다.
/// </summary>
public interface IDataDrivenTutorialFlow
{
    ITutorialBattleFlowHost Host { get; }
    int CurrentStep { get; }
    bool HasFlag(string flag);
    void SetStep(int step);
    void SetFlag(string flag);
    void CompleteTutorial();
    void SetActiveUiRequest(TutorialUiRequest request);
    BattleCharactor FindUnit(TutorialUnitSide side, string templateId);
}

/// <summary>Executor·CustomHook에 전달되는 실행 컨텍스트.</summary>
public sealed class TutorialExecutionContext
{
    public IDataDrivenTutorialFlow Flow;
    public TutorialRuleEntry Rule;
    public TutorialEventContext Event;

    public ITutorialBattleFlowHost Host => Flow != null ? Flow.Host : null;
    public int CurrentStep => Flow != null ? Flow.CurrentStep : 0;
    public string ScopeId => Rule != null ? Rule.ruleId : null;
}
