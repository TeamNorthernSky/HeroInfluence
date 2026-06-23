using UnityEngine;

/// <summary>
/// 영웅 포트레이트 조회(재사용 유틸). 협회 모달·영웅 선택·탐사 박스 등 공용.
///
/// [JC 260621] 포트레이트 해석을 <see cref="PortraitLibrary"/>(+<see cref="EntityPortraits"/> facade)로 이관.
/// 라이브 히어로(PlayerUnitDataTable SO) 기준, 키=HeroIndex(UnitTemplateKey, 예 "10001").
/// 기존 호출부 호환을 위해 얇은 위임 래퍼로 유지. 신규 코드는 EntityPortraits 직접 호출 권장.
/// </summary>
public static class HeroProfileCatalog
{
    /// <summary>기본(미선택) 포트레이트.</summary>
    public static Sprite Default => EntityPortraits.Unselected;

    /// <summary>unitIndex로 히어로 포트레이트. 해석 실패 시 기본.</summary>
    public static Sprite GetByUnitIndex(int unitIndex) => EntityPortraits.HeroByUnit(unitIndex);

    /// <summary>[레거시] 영웅명 기반 조회. 현 포트레이트 체계는 unitIndex(=HeroIndex) 기준 →
    /// GetByUnitIndex 사용 권장. 호환 위해 구 아이콘 경로(HeroIcons)로 폴백 유지.</summary>
    public static Sprite GetByName(string heroName) => HeroIcons.GetCharacterProfile(heroName);
}
