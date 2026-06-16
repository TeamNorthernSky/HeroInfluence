using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영웅 고유명 → 임시 프로필 스프라이트 매핑(재사용 유틸). 협회 모달·영웅 선택 등 공용.
/// 매핑 미지정/빈칸/해석 실패 시 기본(sample 00) 사용.
/// 영웅명은 UnitData.Name(= PlayerUnitData.UnitName) 기준.
/// </summary>
public static class HeroProfileCatalog
{
    private const string Folder = "UI_Sprite/UI_Icon/CharacterProfile_temp/";
    private const string DefaultFile = "character icon sample 00";

    // 영웅 고유명 → 파일명(확장자 제외). 정확한 원문 이름은 런타임 unit.Name과 대조해 조정.
    private static readonly Dictionary<string, string> NameToFile = new Dictionary<string, string>
    {
        { "아이언 걸", "character icon sample 01" },
        { "뱅", "character icon sample 04" },
        { "스콜피온 아이즈", "character icon sample 03" },
        { "아쿠아 블루", "character icon sample 02" },
    };

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>영웅명으로 프로필 스프라이트. 미지정/빈칸이면 기본(00).</summary>
    public static Sprite GetByName(string heroName)
    {
        string file = (!string.IsNullOrWhiteSpace(heroName) && NameToFile.TryGetValue(heroName.Trim(), out var f))
            ? f : DefaultFile;
        if (!Cache.TryGetValue(file, out var s)) { s = Resources.Load<Sprite>(Folder + file); Cache[file] = s; }
        return s;
    }

    /// <summary>기본(미지정) 프로필.</summary>
    public static Sprite Default => GetByName(null);

    /// <summary>unitIndex로 영웅명을 해석해 프로필 스프라이트. 해석 실패 시 기본(00).</summary>
    public static Sprite GetByUnitIndex(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (repo != null && catalog != null
            && repo.TryGetUnit(unitIndex, out var unit) && unit != null
            && catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out var template) && template != null)
        {
            return GetByName(template.Name);
        }
        return Default;
    }
}
