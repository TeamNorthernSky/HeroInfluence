/// <summary>
/// 이펙트 "재료"의 공통 계약. 재료 프리팹이 이 인터페이스를 구현해 자기 동작을 스스로 수행한다.
/// 시퀀서/프리젠터는 재료 종류를 모르고 Play(ctx)만 호출한다.
/// </summary>
public interface ISkillEffectBehaviour
{
    void Play(SkillEffectContext ctx);
}

/// <summary>
/// 지연 발사(손에 들고 있다가 발사 등) 재료가 구현하는 선택적 계약.
/// UnitEffectPresenter가 Fire 이벤트 수신 시 해당 슬롯 인스턴스에만 호출한다.
/// </summary>
public interface IFireableEffect
{
    void Fire(SkillEffectContext ctx);
}
