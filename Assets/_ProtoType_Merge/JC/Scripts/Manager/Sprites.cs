using UnityEngine;

/// <summary>
/// [JC 260627] 게임 2D 그래픽 통합 접근 facade. UI는 이 클래스만 호출한다.
/// 백엔드 = <see cref="SpriteLibrary"/> 단일 SO(Resources/SpriteLibrary). 에셋이 없으면 기본
/// 인스턴스로 폴백(레거시 경로/CSV 규약으로 동작 유지). 호출부는 경로·캐시·CSV를 알 필요 없다.
///
/// 도메인별 그룹으로 노출한다 — 구 EntityPortraits/HeroIcons/UIIcons 3종을 대체:
///   Sprites.Portrait : 히어로/빌런/적 포트레이트
///   Sprites.Icon     : 프로필/클래스스킬/무기/무기스킬 아이콘
///   Sprites.UI       : 자원/스테이터스/건물 아이콘
/// 미래에 소유권이 캐릭터/스킬/무기로 이전되어도 본 facade 구현만 바꾸면 된다(호출부 무변경).
/// </summary>
public static class Sprites
{
    // Assets/**/Resources/SpriteLibrary.asset (폴더명이 Resources이면 위치 무관)
    private const string ResourcePath = "SpriteLibrary";

    private static SpriteLibrary _lib;
    private static SpriteLibrary _fallback;

    public static SpriteLibrary Library
    {
        get
        {
            if (_lib != null) return _lib;
            _lib = Resources.Load<SpriteLibrary>(ResourcePath);
            if (_lib != null) return _lib;

            if (_fallback == null)
            {
                _fallback = ScriptableObject.CreateInstance<SpriteLibrary>();
                Debug.LogWarning($"[Sprites] Resources/'{ResourcePath}' 미발견 — 기본 인스턴스 폴백(레거시 경로/CSV 사용). " +
                                 "Assets/**/Resources/ 아래에 SpriteLibrary 에셋 생성 권장.");
            }
            return _fallback;
        }
    }

    /// <summary>① 포트레이트(히어로/빌런/적). 식별키 = HeroIndex/UnitTemplateKey 문자열.</summary>
    public static class Portrait
    {
        /// <summary>미선택/기본 포트레이트.</summary>
        public static Sprite Unselected => Library.Unselected;

        /// <summary>HeroIndex(문자열)로 히어로 포트레이트.</summary>
        public static Sprite Hero(string heroKey) => Library.GetHeroPortrait(heroKey);

        /// <summary>적 포트레이트(키=적 UnitTemplateKey). 미수록/키없음 시 범용 기본(zako).</summary>
        public static Sprite Enemy(string enemyKey) => Library.GetEnemyPortrait(enemyKey);

        /// <summary>빌런(명명 적) 포트레이트(키=인덱스 문자열=UnitTemplateKey). 미수록/키없음 시 범용 기본(zako).</summary>
        public static Sprite Villain(string villainKey) => Library.GetVillainPortrait(villainKey);

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

    /// <summary>② 히어로 아이콘(프로필/클래스스킬/무기/무기스킬).</summary>
    public static class Icon
    {
        public static Sprite DefaultProfile => Library.DefaultProfile;

        /// <summary>영웅명으로 프로필.</summary>
        public static Sprite Character(string heroName) => Library.GetCharacterProfile(heroName);

        /// <summary>unitIndex로 영웅명을 해석해 프로필. 해석 실패 시 기본 프로필.</summary>
        public static Sprite CharacterByUnit(int unitIndex)
        {
            var repo = PersistentUnitRepository.Instance;
            var catalog = DHCsvTemplateCatalog.Instance;
            if (repo != null && catalog != null
                && repo.TryGetUnit(unitIndex, out var unit) && unit != null
                && catalog.TryGetPlayerUnitTemplate(unit.UnitTemplateKey, out var template) && template != null)
            {
                return Library.GetCharacterProfile(template.UnitName);
            }
            return Library.DefaultProfile;
        }

        public static Sprite ClassSkill(int skillIndex, int level, int fallbackOrder = -1)
            => Library.GetClassSkillIcon(skillIndex, level, fallbackOrder);

        public static Sprite Weapon(int weaponIndex, int level)
            => Library.GetWeaponIcon(weaponIndex, level);

        public static Sprite WeaponSkill(int weaponSkillIndex, int level)
            => Library.GetWeaponSkillIcon(weaponSkillIndex, level);
    }

    /// <summary>③ UI 공용 아이콘(자원/스테이터스/건물).</summary>
    public static class UI
    {
        /// <summary>자원 아이콘(키=ResourceType).</summary>
        public static Sprite Resource(ResourceType type) => Library.GetResourceIcon(type);

        /// <summary>스테이터스 아이콘(키=UIStatusIconType).</summary>
        public static Sprite Status(UIStatusIconType type) => Library.GetStatusIcon(type);

        /// <summary>특수 건물(거점) 아이콘(키=UIBuildingIconType).</summary>
        public static Sprite Building(UIBuildingIconType type) => Library.GetBuildingIcon(type);
    }
}
