/// <summary>
/// 튜토리얼 런타임 효과(핸들)의 수명. 러너가 이 수명별로 dispose 시점을 관리한다.
/// - Step: 스텝 종료 시 해제
/// - Battle: 전투 종료(OnBattleEnded) 시 해제
/// - Manual: 명시적 해제 전까지 유지
/// - ResultPhase: 전투 종료 후 결과창 단계까지 유지, flow 완전 종료 시 해제
/// </summary>
public enum TutorialEffectLifetime
{
    Step,
    Battle,
    Manual,
    ResultPhase
}
