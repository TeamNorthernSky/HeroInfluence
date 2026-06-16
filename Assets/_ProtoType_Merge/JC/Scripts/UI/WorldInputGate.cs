// [JC 신설 260513] World 입력 차단 일원화 게이트.
// 모달 활성 + 미래 확장(시네마틱, 외부 일시정지 등)을 통합 관리.
// World 입력 핸들러(ClickSelectionController 등)는 매 frame 본 게이트 확인.
public static class WorldInputGate
{
    // [JC 260615] 턴 전환 시퀀스(턴종료 → 적 턴 → 다음 턴 income 모달 확인) 동안 월드 입력 차단.
    // TurnManager.EndPlayerTurn에서 set, TurnIncomeModalController.Close(확인)에서 clear.
    // 적 턴이 아닌 짧은 틈(씬 전환 직후·모달 표시 전)에도 탐사 인터랙션이 새는 버그 방지.
    public static bool IsTurnResolving { get; set; }

    public static bool IsBlocked => ModalRegistry.HasAny || IsTurnResolving;
    // 미래 확장: || IsCinematicPlaying || IsExternalPauseActive ...
}
