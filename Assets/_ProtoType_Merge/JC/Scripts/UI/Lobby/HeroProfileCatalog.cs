using UnityEngine;

/// <summary>
/// 영웅 고유명 → 프로필 스프라이트 조회(재사용 유틸). 협회 모달·영웅 선택 등 공용.
///
/// [JC 260618] 데이터·해석 책임을 <see cref="HeroIconLibrary"/>(+<see cref="HeroIcons"/> facade)로 이관.
/// 기존 호출부 호환을 위해 얇은 위임 래퍼로 유지한다. 신규 코드는 HeroIcons를 직접 호출 권장.
/// </summary>
public static class HeroProfileCatalog
{
    /// <summary>영웅명으로 프로필 스프라이트. 미지정/빈칸이면 기본.</summary>
    public static Sprite GetByName(string heroName) => HeroIcons.GetCharacterProfile(heroName);

    /// <summary>기본(미지정) 프로필.</summary>
    public static Sprite Default => HeroIcons.DefaultProfile;

    /// <summary>unitIndex로 영웅명을 해석해 프로필 스프라이트. 해석 실패 시 기본.</summary>
    public static Sprite GetByUnitIndex(int unitIndex) => HeroIcons.GetCharacterProfileByUnit(unitIndex);
}
