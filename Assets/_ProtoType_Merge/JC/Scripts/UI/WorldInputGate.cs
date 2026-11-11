// [JC 신설 260513] World 입력 차단 일원화 게이트.
// 모달 활성 + 미래 확장(시네마틱, 외부 일시정지 등)을 통합 관리.
// World 입력 핸들러(ClickSelectionController 등)는 매 frame 본 게이트 확인.
public static class WorldInputGate
{
    // [JC 260615] 턴 전환 시퀀스(턴종료 → 적 턴 → 다음 턴 시작) 동안 월드 입력 차단.
    // TurnManager.EndPlayerTurn에서 set, GameManager.CurrentDay(턴 진행 완료)에서 clear. [JC 260629 인컴모달 제거로 clear 위치 이전]
    // 적 턴이 아닌 짧은 틈(씬 전환 직후)에도 탐사 인터랙션이 새는 버그 방지.
    public static bool IsTurnResolving { get; set; }

    // JC | 2026-11-10 | 씬 페이드는 독립 잠금 조건으로 추가하여 기존 모달·턴 잠금의 소유권을 유지합니다.
    public static bool IsBlocked => SceneFadeController.IsInputBlocked || ModalManager.HasAny || IsTurnResolving || GateCameraFocusController.IsBusy || JC.Environment.JcDayNightController.IsTransitioning
        || !JcPointerInput.CanControl || !JcPointerInput.Inside;
    // 미래 확장: || IsCinematicPlaying || IsExternalPauseActive ...
}
