using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연구소(Research) 시스템 영속 매니저. GameManager 영속 자식. TrainingManager 패턴 복제.
/// 두 가지 기능을 다룬다:
///   1) 스킬 장착 변경 — 영웅의 CurrentSkillIndex를 DH PersistentUnitRepository.UpdateUnitRuntimeState로 writeback (실결선/비침습).
///   2) 스킬 강화 레벨(1~5) — 영웅별·스킬별 강화 레벨을 JC측 in-memory로 보관.
///
/// ※ seam 정책([[feedback_seam_interface_policy]]): 스킬 강화 레벨의 "전투 대미지 계수 반영"은
///   ASB BattleCharactor.LoadPersistentEquipment가 스킬 레벨 인자를 받지 않아 현재 비침습 결선 불가.
///   → 레벨 저장·표시·비용차감까지는 실제 동작하되, 전투 반영은 보류(더미). 데이터 원천은
///   DHCsvTemplateCatalog.GetClassSkillValueAtLevel(skillIndex, level)로 이미 준비되어 있어,
///   추후 ASB가 유닛별 스킬 레벨을 받는 seam만 열리면 즉시 결선 가능.
///
/// 비용·조건 출처: H.I 자원 데이터 테이블 V1.4 '협회-연구소' 시트.
/// </summary>
[DisallowMultipleComponent]
public class LabManager : MonoBehaviour
{
    public event Action OnStateChanged;

    public const int BaseSkillLevel = 1;
    public const int MaxSkillLevel = 5;

    // 협회-연구소 시트: to_skill_level 2/3/4/5 도달 시 비용. 인덱스 = toLevel - 2.
    // 소모 자원: 자금(Money) + 히어로 메달(Chip).
    public static readonly int[] UpgradeCostMoney = { 1000, 1500, 2000, 2500 };
    public static readonly int[] UpgradeCostChip  = {   30,   60,   90,  120 };

    // 클래스명 → 클래스 인덱스(스킬 인덱스 첫 자리 체계와 동일).
    private static readonly Dictionary<string, int> ClassNameToIndex = new Dictionary<string, int>
    {
        { "가디언", 1 }, { "블래스터", 2 }, { "스트라이커", 3 }, { "서포터", 4 }, { "파이터", 5 },
    };

    [Serializable]
    public class SkillLevelEntry
    {
        public int unitIndex;
        public int skillIndex;
        public int level = BaseSkillLevel;
    }

    [Header("영웅별·스킬별 강화 레벨 (영속)")]
    [SerializeField] private List<SkillLevelEntry> entries = new List<SkillLevelEntry>();

    // (unitIndex, skillIndex) → entry
    private readonly Dictionary<long, SkillLevelEntry> lookup = new Dictionary<long, SkillLevelEntry>();

    public void Initialize() => RebuildLookup();

    private static long Key(int unitIndex, int skillIndex) => ((long)unitIndex << 32) | (uint)skillIndex;

    // ─── 부서(연구소) 상태 ─────────────────────────────────────
    public int GetDepartmentLevel()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Research) : 0;
    }

    public bool IsUnlocked() => GetDepartmentLevel() >= 1;

    /// <summary>스킬 레벨 N(2~5) 강화에 필요한 연구소 레벨 = N - 1.</summary>
    public static int RequiredLabLevelFor(int toSkillLevel) => Mathf.Max(1, toSkillLevel - 1);

    // ─── 영웅 클래스 해석 ──────────────────────────────────────
    public bool TryResolveClass(int unitIndex, out string className, out int classIndex)
    {
        className = null; classIndex = 0;
        var repo = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (repo == null || catalog == null) return false;
        if (!repo.TryGetUnit(unitIndex, out var unit) || unit == null) return false;
        if (!catalog.TryGetPlayerTemplate(unit.UnitTemplateKey, out var template) || template == null) return false;
        className = template.UnitType;
        return !string.IsNullOrWhiteSpace(className)
               && ClassNameToIndex.TryGetValue(className.Trim(), out classIndex);
    }

    /// <summary>해당 영웅이 보유(습득)한 직업 스킬 목록. acquireLevel ≤ 유닛 레벨인 것만.</summary>
    public List<SkillData> GetLearnedSkills(int unitIndex)
    {
        var result = new List<SkillData>();
        var repo = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (repo == null || catalog == null) return result;
        if (!repo.TryGetUnit(unitIndex, out var unit) || unit == null) return result;
        if (!TryResolveClass(unitIndex, out var className, out _)) return result;

        foreach (var s in catalog.GetSkillsByClass(className))
        {
            if (s != null && s.acquireLevel <= Mathf.Max(1, unit.Level))
                result.Add(s);
        }
        return result;
    }

    // ─── 스킬 강화 레벨 조회 ───────────────────────────────────
    public int GetSkillLevel(int unitIndex, int skillIndex)
    {
        return lookup.TryGetValue(Key(unitIndex, skillIndex), out var e) ? e.level : BaseSkillLevel;
    }

    /// <summary>다음 강화 단계(현재+1)에 필요한 (자금, 메달). 더 못 올리면 (-1,-1).</summary>
    public bool GetNextUpgradeCost(int unitIndex, int skillIndex, out int money, out int chip)
    {
        money = -1; chip = -1;
        int level = GetSkillLevel(unitIndex, skillIndex);
        if (level >= MaxSkillLevel) return false;
        int idx = level - 1; // 현재 level→level+1 비용 인덱스 (level1→idx0 = toLevel2)
        if (idx < 0 || idx >= UpgradeCostMoney.Length) return false;
        money = UpgradeCostMoney[idx];
        chip = UpgradeCostChip[idx];
        return true;
    }

    /// <summary>강화 가능 여부(해금 + 연구소 레벨 선행 게이트). 자원 충족은 호출자 확인.</summary>
    public bool CanUpgradeSkill(int unitIndex, int skillIndex)
    {
        if (!IsUnlocked()) return false;
        int level = GetSkillLevel(unitIndex, skillIndex);
        if (level >= MaxSkillLevel) return false;
        int toLevel = level + 1;
        return GetDepartmentLevel() >= RequiredLabLevelFor(toLevel);
    }

    /// <summary>스킬 강화 한 단계: 레벨 +1. 자원 차감은 호출자(LabModalController)가 별도 처리.
    /// 전투 계수 반영은 현재 보류(더미) — 레벨만 영속 보관.</summary>
    public bool TryUpgradeSkill(int unitIndex, int skillIndex)
    {
        if (!CanUpgradeSkill(unitIndex, skillIndex)) return false;
        var e = GetOrCreateEntry(unitIndex, skillIndex);
        e.level = Mathf.Min(MaxSkillLevel, e.level + 1);
        OnStateChanged?.Invoke();
        return true;
    }

    /// <summary>장착 스킬 변경 — CurrentSkillIndex를 영속 writeback(실결선). 스탯 불변이므로 ingame 유지.</summary>
    public bool EquipSkill(int unitIndex, int skillIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;
        if (!repo.TryGetUnit(unitIndex, out var d) || d == null) return false;

        bool ok = repo.UpdateUnitRuntimeState(
            unitIndex, d.UnitTemplateKey, d.Level, d.Favorability,
            d.BaseStats, d.LevelupStats, skillIndex, d.CurrentWeaponIndex,
            d.CurrentWeaponStats, d.IngameStats, d.CurrentHp, d.Exp, d.MaxExp);

        if (ok) OnStateChanged?.Invoke();
        return ok;
    }

    public int GetEquippedSkillIndex(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo != null && repo.TryGetUnit(unitIndex, out var d) && d != null) return d.CurrentSkillIndex;
        return 0;
    }

    // ─── 내부 ──────────────────────────────────────────────────
    private SkillLevelEntry GetOrCreateEntry(int unitIndex, int skillIndex)
    {
        long k = Key(unitIndex, skillIndex);
        if (lookup.TryGetValue(k, out var e)) return e;
        e = new SkillLevelEntry { unitIndex = unitIndex, skillIndex = skillIndex, level = BaseSkillLevel };
        entries.Add(e);
        lookup[k] = e;
        return e;
    }

    private void RebuildLookup()
    {
        lookup.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.unitIndex <= 0 || e.skillIndex <= 0) continue;
            long k = Key(e.unitIndex, e.skillIndex);
            if (lookup.ContainsKey(k)) { Debug.LogWarning($"[LabManager] dup ({e.unitIndex},{e.skillIndex})", this); continue; }
            lookup.Add(k, e);
        }
    }
}
