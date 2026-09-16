using System;

/// <summary>
/// 코드 Flow가 러너(Director)에게 요청하는 host API. Flow는 구체 Director를 직접 참조하지 않고
/// 이 인터페이스로만 효과 추적·정리·완료·유닛 조회를 요청한다.
/// </summary>
public interface ITutorialBattleFlowHost
{
    BattleFlowManager FlowManager { get; }
    BattleManager BattleManager { get; }
    EnemySpawner EnemySpawner { get; }
    PlayerSpawner PlayerSpawner { get; }
    TutorialBattleUI UI { get; }
    string BattleKey { get; }
    int ZoneId { get; }

    /// <summary>안정 템플릿 ID로 참가자 조회(§UnitMatcher).</summary>
    BattleCharactor FindUnit(TutorialUnitSide side, string templateId, UnitMatchMode mode = UnitMatchMode.First);

    /// <summary>IDisposable 효과를 수명별로 추적. 러너가 수명 시점에 dispose.</summary>
    void Track(IDisposable handle, TutorialEffectLifetime lifetime);

    /// <summary>현재 스텝(Step 수명) 효과 해제.</summary>
    void ReleaseStepEffects();

    /// <summary>현재 flow 완료 통지.</summary>
    void CompleteFlow();

    /// <summary>행동 도중 감지(HP/사망)를 다음 안전 경계에서 1회 실행하도록 예약.</summary>
    void RequestBoundaryIntervention(Action intervention);

    /// <summary>UI 시트의 key로 튜토리얼 UI 표시. 표시 성공=true, 시트/키/참조 없음=false(잠금 미획득).</summary>
    bool ShowUi(string key);

    /// <summary>동적 문구용. string.Format 실패 시 경고 후 false.</summary>
    bool ShowUi(string key, params object[] formatArgs);

    /// <summary>현재 튜토리얼 UI 숨김.</summary>
    void HideUi();
}
