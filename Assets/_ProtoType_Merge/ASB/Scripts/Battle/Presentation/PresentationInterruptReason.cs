/// <summary>
/// 진행 중인 스킬 연출(Timeline Rail)을 강제로 중단해야 하는 사유.
/// 피격/사망 반응이 Animator 소유권을 가져가기 전에, 재생 중인 PlayableDirector를 먼저 멈추기 위해 사용한다.
/// </summary>
public enum PresentationInterruptReason
{
    /// <summary>인터럽트 없음(정상 재생/완료).</summary>
    None,
    /// <summary>비치명 피격 — Hit 반응이 Timeline 포즈를 덮기 전에 director를 멈춰야 한다.</summary>
    Hit,
    /// <summary>사망 — Dead 반응이 Timeline 포즈를 덮기 전에 director를 멈춰야 한다.</summary>
    Dead
}

/// <summary>
/// Timeline Rail 재생 결과 전달용 홀더. <c>IEnumerator</c>는 값을 반환할 수 없으므로,
/// 호출자(예: 근거리 핸드오프 라우틴)가 인터럽트 여부를 구분할 수 있도록 이 홀더로 결과를 넘긴다.
/// </summary>
public sealed class TimelineRailPlaybackResult
{
    /// <summary>재생이 어떤 사유로 끝났는지. None이면 정상 완료.</summary>
    public PresentationInterruptReason Interrupt = PresentationInterruptReason.None;

    /// <summary>피격/사망 인터럽트로 끊겼는지.</summary>
    public bool Interrupted => Interrupt != PresentationInterruptReason.None;
}
