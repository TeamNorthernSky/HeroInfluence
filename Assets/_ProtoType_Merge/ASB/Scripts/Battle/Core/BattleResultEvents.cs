using System;

/// <summary>
/// 전투 결과 흐름 이벤트 정의.
/// 발생 순서: OnUnitRewardReady → OnSkillSelectionRequired → OnSkillSelectionCompleted → OnBattleResultReady
///
/// [발생 주체] 게임 흐름(BattleSceneManager 등)이 Invoke합니다.
/// [구독 주체] UI가 구독해 화면을 표시합니다.
/// OnSkillSelectionRequired / OnSkillSelectionCompleted 쌍은 흐름이 UI 응답을 대기하는 구간입니다.
/// </summary>
public static class BattleResultEvents
{
    /// <summary>
    /// 유닛 1명의 경험치·레벨업 결과를 UI에 전달합니다.
    /// UI: exp바 채우기, 레벨업 연출 등.
    /// </summary>
    public static event Action<UnitRewardPreview> OnUnitRewardReady;

    /// <summary>
    /// 스킬 선택이 필요한 유닛의 정보를 UI에 전달합니다.
    /// UI: 스킬 교체 선택 화면 표시 후 OnSkillSelectionCompleted를 발생시켜야 합니다.
    /// </summary>
    public static event Action<UnitRewardPreview> OnSkillSelectionRequired;

    /// <summary>
    /// UI가 스킬 선택을 완료했을 때 발생시킵니다.
    /// 흐름이 WaitUntil로 대기 중이므로 반드시 한 번 호출해야 소프트락이 발생하지 않습니다.
    /// 스킵 시 SelectedSkillId = -1로 전달합니다.
    /// </summary>
    public static event Action<SkillSelectionResult> OnSkillSelectionCompleted;

    /// <summary>
    /// 모든 보상 처리가 완료된 뒤 최종 결과 화면을 표시하도록 요청합니다.
    /// UI: 승리/패배 결과 화면 표시.
    /// </summary>
    public static event Action<BattleRewardPlan> OnBattleResultReady;

    // ── Invoke 헬퍼 (흐름 쪽에서 직접 event?.Invoke 대신 사용) ──────────────

    public static void RaiseUnitRewardReady(UnitRewardPreview preview) =>
        OnUnitRewardReady?.Invoke(preview);

    public static void RaiseSkillSelectionRequired(UnitRewardPreview preview) =>
        OnSkillSelectionRequired?.Invoke(preview);

    public static void RaiseSkillSelectionCompleted(SkillSelectionResult result) =>
        OnSkillSelectionCompleted?.Invoke(result);

    public static void RaiseBattleResultReady(BattleRewardPlan plan) =>
        OnBattleResultReady?.Invoke(plan);
}
