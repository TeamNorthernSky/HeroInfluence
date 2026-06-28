using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>UI 스테이터스 아이콘 종류(키). 파일명 규약 UI_icon_{ATK|DEF|EXE|HP|IP}.</summary>
public enum UIStatusIconType { ATK, DEF, EXE, HP, IP }

/// <summary>UI 특수 건물(거점) 아이콘 종류(키). 파일명 규약 UI_icon_{bank|blockStore|jewelryStore|library}.</summary>
public enum UIBuildingIconType { Bank, BlockStore, JewelryStore, Library }

/// <summary>
/// [JC 260627] 게임 내 2D 그래픽 리소스를 종합 관리하는 단일 객체(ScriptableObject).
///
/// 기존 PortraitLibrary / HeroIconLibrary / UIIconLibrary 3종을 하나로 통합했다.
/// 세 도메인이 공유하던 동일한 리소스 캐시(LoadRes)를 한 벌로 합치고, 각 도메인의
/// CSV/테이블/레거시 3단계 폴백·미래 소유권 이전 설계는 그대로 보존한다(동작 동일).
///   ① 포트레이트 : 히어로/빌런/적 (CSV, 키=Category:Id)
///   ② 히어로 아이콘 : 프로필/클래스스킬/무기/무기스킬 (테이블 → CSV → 레거시 폴백)
///   ③ UI 아이콘 : 자원/스테이터스/건물 (enum 키 직렬화, 비우면 경로 폴백)
/// UI는 <see cref="Sprites"/> facade(Portrait/Icon/UI 그룹)를 통해서만 접근한다.
/// </summary>
[CreateAssetMenu(fileName = "SpriteLibrary", menuName = "HeroInfluence/Sprite Library")]
public class SpriteLibrary : ScriptableObject
{
    // ════════════════════ 공통 리소스 캐시 ════════════════════
    private readonly Dictionary<string, Sprite> _resCache = new Dictionary<string, Sprite>();

    private Sprite LoadRes(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_resCache.TryGetValue(path, out var s)) { s = Resources.Load<Sprite>(path); _resCache[path] = s; }
        return s;
    }

    // ════════════════════ ① 포트레이트 (구 PortraitLibrary) ════════════════════
    public enum PortraitCategory { Hero, Villain, Enemy }

    [Header("포트레이트 CSV (Resources TextAsset, 컬럼=Category,Id,PortraitResourcePath)")]
    [SerializeField] private string portraitCsvPath = "Portrait_Hero_Sprite/PortraitSheet";

    [Header("미선택/기본 포트레이트")]
    [SerializeField] private string unselectedPortraitPath = "Portrait_Hero_Sprite/UI_profile_hero_unselected";

    [Header("적 기본 포트레이트 (CSV Enemy/Villain 미수록 시 범용 폴백)")]
    [SerializeField] private string defaultEnemyPortraitPath = "Portrait_Enemy_Sprite/UI_profile_villian_zako";

    private Dictionary<string, string> _portraitCsvLut; // "Category:Id" → resource path

    private static string PortraitKey(PortraitCategory cat, string id) => cat + ":" + (id == null ? string.Empty : id.Trim());

    private void EnsurePortraitCsvLut()
    {
        if (_portraitCsvLut != null) return;
        _portraitCsvLut = new Dictionary<string, string>();
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
            _portraitCsvLut[PortraitKey(cat, id)] = path;
        }
    }

    /// <summary>미선택/기본 포트레이트.</summary>
    public Sprite Unselected => LoadRes(unselectedPortraitPath);

    /// <summary>카테고리+식별키로 포트레이트. 미수록/실패 시 Unselected.</summary>
    public Sprite GetPortrait(PortraitCategory cat, string id)
    {
        EnsurePortraitCsvLut();
        if (!string.IsNullOrWhiteSpace(id) && _portraitCsvLut.TryGetValue(PortraitKey(cat, id), out var path))
        {
            var sp = LoadRes(path);
            if (sp != null) return sp;
        }
        return Unselected;
    }

    /// <summary>히어로 포트레이트(키=HeroIndex 문자열, 예 "10001").</summary>
    public Sprite GetHeroPortrait(string heroKey) => GetPortrait(PortraitCategory.Hero, heroKey);

    /// <summary>적 기본 포트레이트(범용 zako).</summary>
    public Sprite DefaultEnemy => LoadRes(defaultEnemyPortraitPath);

    /// <summary>적 포트레이트(키=적 UnitTemplateKey). CSV Enemy 행 우선, 없으면 범용 기본(zako), 그것도 없으면 Unselected.</summary>
    public Sprite GetEnemyPortrait(string enemyKey)
    {
        EnsurePortraitCsvLut();
        if (!string.IsNullOrWhiteSpace(enemyKey) && _portraitCsvLut.TryGetValue(PortraitKey(PortraitCategory.Enemy, enemyKey), out var path))
        {
            var sp = LoadRes(path);
            if (sp != null) return sp;
        }
        var d = DefaultEnemy;
        return d != null ? d : Unselected;
    }

    /// <summary>빌런(명명 적) 포트레이트(키=인덱스 문자열=UnitTemplateKey 규약). CSV Villain 행 우선, 없으면 범용 기본(zako), 그것도 없으면 Unselected.</summary>
    public Sprite GetVillainPortrait(string villainKey)
    {
        EnsurePortraitCsvLut();
        if (!string.IsNullOrWhiteSpace(villainKey) && _portraitCsvLut.TryGetValue(PortraitKey(PortraitCategory.Villain, villainKey), out var path))
        {
            var sp = LoadRes(path);
            if (sp != null) return sp;
        }
        var d = DefaultEnemy;
        return d != null ? d : Unselected;
    }

    // ════════════════════ ② 히어로 아이콘 (구 HeroIconLibrary) ════════════════════
    [Serializable] public class CharacterEntry { public string heroName; public Sprite profile; }
    [Serializable] public class SkillEntry { public int skillIndex; public Sprite[] byLevel = new Sprite[5]; }
    [Serializable] public class WeaponEntry { public int weaponIndex; public Sprite[] byLevel = new Sprite[5]; }

    [Header("캐릭터 프로필 (미래: 캐릭터 소유, 키=영웅명) — 비우면 폴백")]
    [SerializeField] private List<CharacterEntry> characters = new List<CharacterEntry>();

    [Header("클래스 스킬 아이콘 (미래: 클래스/스킬 소유, 키=skillIndex, byLevel[0]=Lv1) — 비우면 폴백")]
    [SerializeField] private List<SkillEntry> skills = new List<SkillEntry>();

    [Header("무기 아이콘 (미래: 무기 소유, 키=weaponIndex, byLevel[0]=Lv1) — 비우면 폴백")]
    [SerializeField] private List<WeaponEntry> weapons = new List<WeaponEntry>();

    [Header("폴백: 레거시 Resources 규약 (테이블 미기입 시 — 현 동작 보존)")]
    [SerializeField] private string profileFolder = "UI_Sprite/UI_Icon/CharacterProfile_temp/";
    [SerializeField] private string profileDefaultFile = "character icon sample 00";
    [Tooltip("{0}=종류(01~), {1}=레벨(1~5)")]
    [SerializeField] private string skillIconPathFormat = "UI_Sprite/UI_Icon/ClassSkill_temp/skill {0:00} level {1}";
    [SerializeField] private int skillIconVariants = 2;
    [Tooltip("{0}=tier(01~03), {1}=레벨(1~5)")]
    [SerializeField] private string weaponIconPathFormat = "UI_Sprite/UI_Icon/Weapon_temp/weapon {0:00} level {1}";

    [Header("클래스 스킬 아이콘 CSV (Resources TextAsset, 컬럼=ClassSkillIndex,Level,IconResourcePath)")]
    [Tooltip("직업별 스킬 아이콘 경로표. 테이블(skills) 미지정 시 레거시 폴백보다 우선 적용.")]
    [SerializeField] private string classSkillIconCsvPath = "Icon_Skill_Sprite/ClassSkillIconSheet";

    [Header("무기 아이콘 CSV (컬럼=WeaponIndex,IconResourcePath — 레벨 무관)")]
    [SerializeField] private string weaponIconCsvPath = "Icon_Weapon_Sprite/WeaponIconSheet";

    [Header("무기스킬 아이콘 CSV (컬럼=WeaponSkillIndex,Level,IconResourcePath)")]
    [SerializeField] private string weaponSkillIconCsvPath = "Icon_WeaponSkill_Sprite/WeaponSkillIconSheet";

    private Dictionary<string, Sprite> _charLut;
    private Dictionary<int, Sprite[]> _skillLut;
    private Dictionary<int, Sprite[]> _weaponLut;
    private Dictionary<long, string> _csvPathLut;
    private Dictionary<int, string> _weaponCsvLut;
    private Dictionary<long, string> _weaponSkillCsvLut;

    private static Sprite AtLevel(Sprite[] arr, int level)
    {
        if (arr == null || arr.Length == 0) return null;
        int i = Mathf.Clamp(level, 1, arr.Length) - 1;
        return arr[i];
    }

    // ── CSV 경로표 (ClassSkillIndex,Level → Resources 경로) ───────
    private static long CsvKey(int skillIndex, int level) => ((long)skillIndex << 8) | (uint)Mathf.Clamp(level, 1, 5);

    private void EnsureCsvLut()
    {
        if (_csvPathLut != null) return;
        _csvPathLut = new Dictionary<long, string>();
        if (string.IsNullOrEmpty(classSkillIconCsvPath)) return;
        var ta = Resources.Load<TextAsset>(classSkillIconCsvPath);
        if (ta == null) return;
        var lines = ta.text.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 0행=헤더
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var c = line.Split(',');
            if (c.Length < 3) continue;
            if (!int.TryParse(c[0].Trim(), out int idx)) continue;
            if (!int.TryParse(c[1].Trim(), out int lv)) continue;
            string path = c[2].Trim();
            if (string.IsNullOrEmpty(path)) continue;
            _csvPathLut[CsvKey(idx, lv)] = path;
        }
    }

    private void EnsureWeaponCsvLut()
    {
        if (_weaponCsvLut != null) return;
        _weaponCsvLut = new Dictionary<int, string>();
        if (string.IsNullOrEmpty(weaponIconCsvPath)) return;
        var ta = Resources.Load<TextAsset>(weaponIconCsvPath);
        if (ta == null) return;
        var lines = ta.text.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 0행=헤더 (WeaponIndex,IconResourcePath)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var c = line.Split(',');
            if (c.Length < 2) continue;
            if (!int.TryParse(c[0].Trim(), out int wi)) continue;
            string path = c[1].Trim();
            if (string.IsNullOrEmpty(path)) continue;
            _weaponCsvLut[wi] = path;
        }
    }

    private void EnsureWeaponSkillCsvLut()
    {
        if (_weaponSkillCsvLut != null) return;
        _weaponSkillCsvLut = new Dictionary<long, string>();
        if (string.IsNullOrEmpty(weaponSkillIconCsvPath)) return;
        var ta = Resources.Load<TextAsset>(weaponSkillIconCsvPath);
        if (ta == null) return;
        var lines = ta.text.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 0행=헤더 (WeaponSkillIndex,Level,IconResourcePath)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var c = line.Split(',');
            if (c.Length < 3) continue;
            if (!int.TryParse(c[0].Trim(), out int wsi)) continue;
            if (!int.TryParse(c[1].Trim(), out int lv)) continue;
            string path = c[2].Trim();
            if (string.IsNullOrEmpty(path)) continue;
            _weaponSkillCsvLut[CsvKey(wsi, lv)] = path;
        }
    }

    // ── Character profile ─────────────────────────────────────
    public Sprite DefaultProfile => LoadRes(profileFolder + profileDefaultFile);

    /// <summary>영웅명으로 프로필. 테이블 미지정/빈칸이면 기본 프로필.</summary>
    public Sprite GetCharacterProfile(string heroName)
    {
        if (!string.IsNullOrWhiteSpace(heroName))
        {
            if (_charLut == null)
            {
                _charLut = new Dictionary<string, Sprite>();
                foreach (var e in characters)
                    if (e != null && !string.IsNullOrWhiteSpace(e.heroName) && e.profile != null)
                        _charLut[e.heroName.Trim()] = e.profile;
            }
            if (_charLut.TryGetValue(heroName.Trim(), out var sp) && sp != null) return sp;
        }
        return DefaultProfile;
    }

    // ── Class skill icon (key = skillIndex) ───────────────────
    /// <summary>스킬 아이콘. 우선순위: 직접참조 테이블(skills) → CSV 경로표 → 레거시 경로 폴백.
    /// fallbackOrder(모달 행 순서)가 주어지면 기존 위치 기반과 동일 결과(시각 동등) 유지.</summary>
    public Sprite GetClassSkillIcon(int skillIndex, int level, int fallbackOrder = -1)
    {
        if (_skillLut == null)
        {
            _skillLut = new Dictionary<int, Sprite[]>();
            foreach (var e in skills)
                if (e != null && !_skillLut.ContainsKey(e.skillIndex)) _skillLut[e.skillIndex] = e.byLevel;
        }
        if (_skillLut.TryGetValue(skillIndex, out var arr))
        {
            var sp = AtLevel(arr, level);
            if (sp != null) return sp;
        }

        // CSV 경로표(직업별 스킬 아이콘) — 직접참조 테이블 미지정 시 우선. CSV 미수록(예: 무기스킬)은 아래 레거시 폴백.
        EnsureCsvLut();
        if (_csvPathLut.TryGetValue(CsvKey(skillIndex, level), out var csvPath))
        {
            var csp = LoadRes(csvPath);
            if (csp != null) return csp;
        }

        int variants = Mathf.Max(1, skillIconVariants);
        int basis = fallbackOrder >= 0 ? fallbackOrder : Mathf.Max(0, skillIndex);
        int v = (basis % variants + variants) % variants + 1;
        int lv = Mathf.Clamp(level, 1, 5);
        return LoadRes(string.Format(skillIconPathFormat, v, lv));
    }

    // ── Weapon icon (key = weaponIndex) ───────────────────────
    /// <summary>무기 아이콘. 우선순위: 직접참조 테이블(weapons) → CSV 경로표(직업별, 레벨 무관) → tier 레거시 폴백.</summary>
    public Sprite GetWeaponIcon(int weaponIndex, int level)
    {
        if (_weaponLut == null)
        {
            _weaponLut = new Dictionary<int, Sprite[]>();
            foreach (var e in weapons)
                if (e != null && !_weaponLut.ContainsKey(e.weaponIndex)) _weaponLut[e.weaponIndex] = e.byLevel;
        }
        if (_weaponLut.TryGetValue(weaponIndex, out var arr))
        {
            var sp = AtLevel(arr, level);
            if (sp != null) return sp;
        }

        // CSV 경로표(직업별 무기 아이콘) — 무기 아이콘은 레벨 무관(키=weaponIndex).
        EnsureWeaponCsvLut();
        if (_weaponCsvLut.TryGetValue(weaponIndex, out var wpath))
        {
            var wsp = LoadRes(wpath);
            if (wsp != null) return wsp;
        }

        int tier = Mathf.Clamp(WorkshopManager.TierOf(weaponIndex), 1, 3);
        int lv = Mathf.Clamp(level < 1 ? 1 : level, 1, 5);
        return LoadRes(string.Format(weaponIconPathFormat, tier, lv));
    }

    // ── Weapon skill icon (key = weaponSkillIndex, level) ─────
    /// <summary>무기스킬 아이콘. CSV 경로표 우선, 미수록 시 레거시(클래스스킬 경로 규약) 폴백.</summary>
    public Sprite GetWeaponSkillIcon(int weaponSkillIndex, int level)
    {
        EnsureWeaponSkillCsvLut();
        if (_weaponSkillCsvLut.TryGetValue(CsvKey(weaponSkillIndex, level), out var path))
        {
            var sp = LoadRes(path);
            if (sp != null) return sp;
        }
        // 폴백: 기존 무기스킬 아이콘 해석(클래스스킬 경로 규약 재사용)
        return GetClassSkillIcon(weaponSkillIndex, level);
    }

    // ════════════════════ ③ UI 아이콘 (구 UIIconLibrary) ════════════════════
    [Header("자원 아이콘 (키=ResourceType) — 비우면 폴백")]
    [SerializeField] private Sprite resMoney;    // Money
    [SerializeField] private Sprite resMedal;    // Chip(메달)
    [SerializeField] private Sprite resCrystal;  // Crystal
    [SerializeField] private Sprite resBlock;    // Supply(자재/블록)

    [Header("스테이터스 아이콘 (키=UIStatusIconType) — 비우면 폴백")]
    [SerializeField] private Sprite statATK;
    [SerializeField] private Sprite statDEF;
    [SerializeField] private Sprite statEXE;
    [SerializeField] private Sprite statHP;
    [SerializeField] private Sprite statIP;

    [Header("특수 건물 아이콘 (키=UIBuildingIconType) — 비우면 폴백")]
    [SerializeField] private Sprite bldgBank;
    [SerializeField] private Sprite bldgBlockStore;
    [SerializeField] private Sprite bldgJewelryStore;
    [SerializeField] private Sprite bldgLibrary;

    [Header("폴백: Resources 경로 규약 (인스펙터 슬롯 미설정 시)")]
    [SerializeField] private string resourceFolder = "Icon_UI_Sprite/Icon_UI_Resource_Sprite/";
    [SerializeField] private string statusFolder   = "Icon_UI_Sprite/Icon_UI_StatusIcon_Sprite/";
    [SerializeField] private string buildingFolder = "Icon_UI_Sprite/Icon_UI_SpecialBuilding_Sprite/";

    private Sprite Resolve(Sprite direct, string folder, string file)
        => direct != null ? direct : LoadRes(folder + file);

    /// <summary>자원 아이콘(키=ResourceType, 파일명 규약).</summary>
    public Sprite GetResourceIcon(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money:   return Resolve(resMoney,   resourceFolder, "UI_icon_money");
            case ResourceType.Chip:    return Resolve(resMedal,   resourceFolder, "UI_icon_medal");
            case ResourceType.Crystal: return Resolve(resCrystal, resourceFolder, "UI_icon_crystal");
            case ResourceType.Supply:  return Resolve(resBlock,   resourceFolder, "UI_icon_block");
            default: return null;
        }
    }

    /// <summary>스테이터스 아이콘(키=UIStatusIconType).</summary>
    public Sprite GetStatusIcon(UIStatusIconType type)
    {
        switch (type)
        {
            case UIStatusIconType.ATK: return Resolve(statATK, statusFolder, "UI_icon_ATK");
            case UIStatusIconType.DEF: return Resolve(statDEF, statusFolder, "UI_icon_DEF");
            case UIStatusIconType.EXE: return Resolve(statEXE, statusFolder, "UI_icon_EXE");
            case UIStatusIconType.HP:  return Resolve(statHP,  statusFolder, "UI_icon_HP");
            case UIStatusIconType.IP:  return Resolve(statIP,  statusFolder, "UI_icon_IP");
            default: return null;
        }
    }

    /// <summary>특수 건물(거점) 아이콘(키=UIBuildingIconType).</summary>
    public Sprite GetBuildingIcon(UIBuildingIconType type)
    {
        switch (type)
        {
            case UIBuildingIconType.Bank:         return Resolve(bldgBank,         buildingFolder, "UI_icon_bank");
            case UIBuildingIconType.BlockStore:   return Resolve(bldgBlockStore,   buildingFolder, "UI_icon_blockStore");
            case UIBuildingIconType.JewelryStore: return Resolve(bldgJewelryStore, buildingFolder, "UI_icon_jewelryStore");
            case UIBuildingIconType.Library:      return Resolve(bldgLibrary,      buildingFolder, "UI_icon_library");
            default: return null;
        }
    }
}
