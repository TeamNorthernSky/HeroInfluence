using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 260621] 캐릭터(히어로)·빌런·적 포트레이트의 단일 보관·해석 객체(ScriptableObject).
///
/// HeroIconLibrary(스킬/무기 "아이콘")와 책임을 분리한 "포트레이트" 전용 라이브러리.
/// 카테고리(Hero/Villain/Enemy) × 식별키(Id) → 스프라이트로 해석한다.
/// UI는 <see cref="EntityPortraits"/> facade를 통해서만 접근한다.
///
/// 현재는 CSV(Resources TextAsset, 컬럼=Category,Id,PortraitResourcePath) 경로표 기반.
/// 라이브 히어로 데이터는 PlayerUnitDataTable(SO) 기준, 식별키 = HeroIndex(예 10001~10005, UnitTemplateKey와 동일).
/// 빌런·적 포트레이트는 같은 CSV에 Category만 달리해 추가하면 된다.
/// </summary>
[CreateAssetMenu(fileName = "PortraitLibrary", menuName = "HeroInfluence/Portrait Library")]
public class PortraitLibrary : ScriptableObject
{
    public enum PortraitCategory { Hero, Villain, Enemy }

    [Header("포트레이트 CSV (Resources TextAsset, 컬럼=Category,Id,PortraitResourcePath)")]
    [SerializeField] private string portraitCsvPath = "Portrait_Hero_Sprite/PortraitSheet";

    [Header("미선택/기본 포트레이트")]
    [SerializeField] private string unselectedPortraitPath = "Portrait_Hero_Sprite/UI_profile_hero_unselected";

    [Header("적 기본 포트레이트 (CSV Enemy/Villain 미수록 시 범용 폴백)")]
    [SerializeField] private string defaultEnemyPortraitPath = "Portrait_Enemy_Sprite/UI_profile_villian_zako";

    private readonly Dictionary<string, Sprite> _resCache = new Dictionary<string, Sprite>();
    private Dictionary<string, string> _csvLut; // "Category:Id" → resource path

    private Sprite LoadRes(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_resCache.TryGetValue(path, out var s)) { s = Resources.Load<Sprite>(path); _resCache[path] = s; }
        return s;
    }

    private static string Key(PortraitCategory cat, string id) => cat + ":" + (id == null ? string.Empty : id.Trim());

    private void EnsureCsvLut()
    {
        if (_csvLut != null) return;
        _csvLut = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(portraitCsvPath)) return;
        var ta = Resources.Load<TextAsset>(portraitCsvPath);
        if (ta == null) return;
        var lines = ta.text.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 0행=헤더
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var c = line.Split(',');
            if (c.Length < 3) continue;
            if (!System.Enum.TryParse(c[0].Trim(), true, out PortraitCategory cat)) continue;
            string id = c[1].Trim();
            string path = c[2].Trim();
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(path)) continue;
            _csvLut[Key(cat, id)] = path;
        }
    }

    /// <summary>미선택/기본 포트레이트.</summary>
    public Sprite Unselected => LoadRes(unselectedPortraitPath);

    /// <summary>카테고리+식별키로 포트레이트. 미수록/실패 시 Unselected.</summary>
    public Sprite GetPortrait(PortraitCategory cat, string id)
    {
        EnsureCsvLut();
        if (!string.IsNullOrWhiteSpace(id) && _csvLut.TryGetValue(Key(cat, id), out var path))
        {
            var sp = LoadRes(path);
            if (sp != null) return sp;
        }
        return Unselected;
    }

    /// <summary>히어로 포트레이트(키=HeroIndex 문자열, 예 "10001").</summary>
    public Sprite GetHeroPortrait(string heroKey) => GetPortrait(PortraitCategory.Hero, heroKey);

    /// <summary>[JC 260622] 적 기본 포트레이트(범용 zako).</summary>
    public Sprite DefaultEnemy => LoadRes(defaultEnemyPortraitPath);

    /// <summary>적 포트레이트(키=적 UnitTemplateKey). CSV Enemy 행 매칭 우선, 없으면 범용 기본(zako), 그것도 없으면 Unselected.</summary>
    public Sprite GetEnemyPortrait(string enemyKey)
    {
        EnsureCsvLut();
        if (!string.IsNullOrWhiteSpace(enemyKey) && _csvLut.TryGetValue(Key(PortraitCategory.Enemy, enemyKey), out var path))
        {
            var sp = LoadRes(path);
            if (sp != null) return sp;
        }
        var d = DefaultEnemy;
        return d != null ? d : Unselected;
    }

    /// <summary>[JC 260625] 빌런(명명 적) 포트레이트(키=인덱스 문자열, Hero/Enemy와 동일한 인덱스=UnitTemplateKey 규약).
    /// CSV Villain 행 매칭 우선, 없으면 범용 기본(zako), 그것도 없으면 Unselected.</summary>
    public Sprite GetVillainPortrait(string villainKey)
    {
        EnsureCsvLut();
        if (!string.IsNullOrWhiteSpace(villainKey) && _csvLut.TryGetValue(Key(PortraitCategory.Villain, villainKey), out var path))
        {
            var sp = LoadRes(path);
            if (sp != null) return sp;
        }
        var d = DefaultEnemy;
        return d != null ? d : Unselected;
    }
}
