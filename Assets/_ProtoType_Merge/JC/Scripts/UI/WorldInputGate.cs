// [JC 신설 260513] World 입력 차단 일원화 게이트.
// 모달 활성 + 미래 확장(시네마틱, 외부 일시정지 등)을 통합 관리.
// World 입력 핸들러(ClickSelectionController 등)는 매 frame 본 게이트 확인.
public static class WorldInputGate
{
    public static bool IsBlocked => ModalRegistry.HasAny;
    // 미래 확장: || IsCinematicPlaying || IsExternalPauseActive ...
}
