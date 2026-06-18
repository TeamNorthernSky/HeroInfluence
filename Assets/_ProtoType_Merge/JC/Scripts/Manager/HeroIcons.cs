using UnityEngine;

/// <summary>
/// [JC 260618] <see cref="HeroIconLibrary"/> 접근용 얇은 static facade. UI는 이 클래스만 호출한다.
/// SO 에셋은 Resources에서 로드(없으면 기본 인스턴스로 폴백 — 레거시 경로 규약으로 동작 유지).
/// unitIndex→영웅명 해석(영속 레포 + CSV 카탈로그)을 여기에 모은다.
///
/// 미래에 소유권이 캐릭터/클래스/스킬/무기로 이전되면, 본 facade 메서드 구현만
/// "객체 우선 → 라이브러리 폴백"으로 바꾸면 된다(호출부 무변경).
/// </summary>
public static class HeroIcons
{
    // Assets/**/Resources/HeroIconLibrary.asset (폴더명이 Resources이면 위치 무관)
    private const string ResourcePath = "HeroIconLibrary";

    private static HeroIconLibrary _lib;
    private static HeroIconLibrary _fallback;

    public static HeroIconLibrary Library
    {
        get
        {
            if (_lib != null) return _lib;
            _lib = Resources.Load<HeroIconLibrary>(ResourcePath);
            if (_lib != null) return _lib;

            if (_fallback == null)
            {
                _fallback = ScriptableObject.CreateInstance<HeroIconLibrary>();
                Debug.LogWarning($"[HeroIcons] Resources/'{ResourcePath}' 미발견 — 기본 인스턴스 폴백(레거시 경로 사용). " +
                                 "Assets/**/Resources/ 아래에 HeroIconLibrary 에셋 생성 권장.");
            }
            return _fallback;
        }
    }

    public static Sprite DefaultProfile => Library.DefaultProfile;

    public static Sprite GetCharacterProfile(string heroName) => Library.GetCharacterProfile(heroName);

    /// <summary>unitIndex로 영웅명을 해석해 프로필. 해석 실패 시 기본 프로필.</summary>
    public static Sprite GetCharacterProfileByUnit(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (repo != null && catalog != null
            && repo.TryGetUnit(unitIndex, out var unit) && unit != null
            && catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out var template) && template != null)
        {
            return Library.GetCharacterProfile(template.Name);
        }
        return Library.DefaultProfile;
    }

    public static Sprite GetClassSkillIcon(int skillIndex, int level, int fallbackOrder = -1)
        => Library.GetClassSkillIcon(skillIndex, level, fallbackOrder);

    public static Sprite GetWeaponIcon(int weaponIndex, int level)
        => Library.GetWeaponIcon(weaponIndex, level);
}
