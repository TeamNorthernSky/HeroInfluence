/// <summary>이펙트 재료의 공통 계약. 재료 프리팹이 자기 동작을 수행한다.</summary>
public interface ISkillEffectBehaviour
{
    void Play(SkillEffectContext ctx);
}

/// <summary>유지형 이펙트가 후속 Cue의 Signal/Stop을 받을 때 구현하는 선택 계약.</summary>
public interface ISkillEffectHandle
{
    /// <returns>true면 Handle 수명을 종료하고 이후 오브젝트 수명은 재료가 책임진다.</returns>
    bool Signal(SkillEffectContext ctx);
    void Stop();
}

/// <summary>
/// 재료가 자기 수명(자연 종료, 정지 후 정리)을 스스로 책임진다는 표시.
/// <see cref="EffectFallbackRelease"/>가 고정 수명 상한을 덧붙이지 않는다.
/// </summary>
public interface ISelfManagedEffectLifetime
{
}