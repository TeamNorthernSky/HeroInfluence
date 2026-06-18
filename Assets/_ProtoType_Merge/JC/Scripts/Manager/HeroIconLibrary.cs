using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 260618] 캐릭터 프로필 / 클래스 스킬 아이콘 / 무기 아이콘의 단일 보관·해석 객체(ScriptableObject).
///
/// 기존엔 각 UI 컨트롤러가 Resources 경로를 직접 조립(캐릭터=이름, 스킬=모달 행 순서, 무기=tier)했다.
/// 미래에 캐릭터/클래스/스킬/무기 객체가 자기 스프라이트를 "소유"하도록 이전하기 쉽게,
/// 정체성 키(영웅명·skillIndex·weaponIndex) 기반 매핑 테이블 + 레거시 경로 폴백으로 한곳에 모았다.
/// UI는 <see cref="HeroIcons"/> facade를 통해서만 접근하며 더 이상 경로를 알지 못한다.
///
/// ▶ 이전 경로(미래·타파트 영역): 캐릭터/클래스/스킬/무기가 자기 스프라이트를 갖게 되면
///   본 SO의 각 Entry를 해당 객체로 떼어 옮기고, 조회를 "객체 우선 → 본 테이블 폴백"으로 바꾼다.
///   그때 변경 지점은 HeroIcons 한 곳뿐(메서드 시그니처·UI 호출부 무변경).
///
/// 테이블을 비워두면 폴백(레거시 Resources 규약)으로 동작 = 현 화면과 동일. 디자이너가 채우면 자동 전환.
/// </summary>
[CreateAssetMenu(fileName = "HeroIconLibrary", menuName = "HeroInfluence/Hero Icon Library")]
public class HeroIconLibrary : ScriptableObject
{
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

    private readonly Dictionary<string, Sprite> _resCache = new Dictionary<string, Sprite>();
    private Dictionary<string, Sprite> _charLut;
    private Dictionary<int, Sprite[]> _skillLut;
    private Dictionary<int, Sprite[]> _weaponLut;

    private Sprite LoadRes(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!_resCache.TryGetValue(path, out var s)) { s = Resources.Load<Sprite>(path); _resCache[path] = s; }
        return s;
    }

    private static Sprite AtLevel(Sprite[] arr, int level)
    {
        if (arr == null || arr.Length == 0) return null;
        int i = Mathf.Clamp(level, 1, arr.Length) - 1;
        return arr[i];
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
    /// <summary>스킬 아이콘. 테이블 미지정 시 레거시 경로 폴백.
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
        int variants = Mathf.Max(1, skillIconVariants);
        int basis = fallbackOrder >= 0 ? fallbackOrder : Mathf.Max(0, skillIndex);
        int v = (basis % variants + variants) % variants + 1;
        int lv = Mathf.Clamp(level, 1, 5);
        return LoadRes(string.Format(skillIconPathFormat, v, lv));
    }

    // ── Weapon icon (key = weaponIndex) ───────────────────────
    /// <summary>무기 아이콘. 테이블 미지정 시 tier 기반 레거시 경로 폴백.</summary>
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
        int tier = Mathf.Clamp(WorkshopManager.TierOf(weaponIndex), 1, 3);
        int lv = Mathf.Clamp(level < 1 ? 1 : level, 1, 5);
        return LoadRes(string.Format(weaponIconPathFormat, tier, lv));
    }
}
