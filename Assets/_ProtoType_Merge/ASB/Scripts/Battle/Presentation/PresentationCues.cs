/// <summary>
/// 연출 Cue 어휘(프리셋). 클립 AniEvent_PresentationCue 인자 + CueBinding.CueName이 이 목록에서 선택되거나 커스텀 입력된다.
/// 자주 늘어나면 ScriptableObject로 승격 고려. 매칭은 항상 trim+소문자 정규화 기준.
/// </summary>
public static class PresentationCues
{
    public static readonly string[] Presets =
    {
        "fire",     // 발사(투사체/손발사)
        "impact",   // 착탄/명중
        "cast",     // 시전/차징
        "hit_fx"    // 타격 연출(데미지 타이밍 OnHit과는 별개)
    };
}
