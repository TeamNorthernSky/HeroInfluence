using UnityEngine;

/// <summary>
/// [JC 260621] <see cref="PortraitLibrary"/> 접근용 얇은 static facade. UI는 이 클래스만 호출한다.
/// SO 에셋(Resources/PortraitLibrary)이 있으면 로드, 없으면 기본 인스턴스로 폴백(CSV 경로표로 동작).
///
/// 히어로 식별키 = HeroIndex(UnitTemplateKey, 예 "10001"). unitIndex는 영속 레포에서 템플릿키로 해석한다.
/// 빌런·적 포트레이트가 추가되면 Enemy(...)/Villain(...) 메서드만 늘리면 된다(호출부 무변경).
/// </summary>
public static class EntityPortraits
{
    private const string ResourcePath = "PortraitLibrary";

    private static PortraitLibrary _lib;
    private static PortraitLibrary _fallback;

    public static PortraitLibrary Library
    {
        get
        {
            if (_lib != null) return _lib;
            _lib = Resources.Load<PortraitLibrary>(ResourcePath);
            if (_lib != null) return _lib;
            if (_fallback == null) _fallback = ScriptableObject.CreateInstance<PortraitLibrary>();
            return _fallback;
        }
    }

    /// <summary>미선택/기본 포트레이트.</summary>
    public static Sprite Unselected => Library.Unselected;

    /// <summary>HeroIndex(문자열)로 히어로 포트레이트.</summary>
    public static Sprite Hero(string heroKey) => Library.GetHeroPortrait(heroKey);

    /// <summary>런타임 unitIndex로 히어로 포트레이트. 해석 실패 시 Unselected.</summary>
    public static Sprite HeroByUnit(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo != null && repo.TryGetUnit(unitIndex, out var unit) && unit != null
            && !string.IsNullOrWhiteSpace(unit.UnitTemplateKey))
        {
            return Library.GetHeroPortrait(unit.UnitTemplateKey);
        }
        return Library.Unselected;
    }
}
