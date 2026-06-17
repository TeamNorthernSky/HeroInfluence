using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 연구소(Research) 시스템 영속 매니저. GameManager 영속 자식. TrainingManager 패턴 복제.
/// 두 가지 기능을 다룬다:
///   1) 스킬 장착 변경 — 영웅의 CurrentSkillIndex를 DH PersistentUnitRepository.UpdateUnitRuntimeState로 writeback (실결선/비침습).
///   2) 스킬 강화 레벨(1~5) — 영웅별·스킬별 강화 레벨을 JC측 in-memory로 보관.
///
/// ※ [JC 260616] seam 열림: ASB BattleCharactor.LoadPersistentEquipment가 SkillLevel 인자를 받아
///   전투 위력에 반영함(GetClassSkillValueAtLevel). → 강화/장착 시 "장착 스킬"의 강화 레벨을
///   DH PersistentUnitRepository.SetSkillLevel(유닛당 단일 SkillLevel)로 writeback해 전투에 반영한다.
///   LabManager는 (unit,skill)별 레벨을 보관하되, DH 동기는 현재 장착 스킬에 한정(모델 정합).
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
    // 소모 자원: 자금(Money) + 히어로 메달(Chip). [JC 260617] 인스펙터 편집 가능하도록 직렬화.
    [Header("스킬 강화 비용 (인스펙터 편집 — 레벨 2/3/4/5 도달 기준)")]
    // [JC 260617] V1.0 프로토타입 자원 밸런스 '협회-연구소' 시트 기준.
    [Tooltip("필요 자금 (레벨 2/3/4/5 도달)")]
    [SerializeField] private int[] upgradeCostMoney = { 400, 600, 800, 1000 };
    [Tooltip("필요 히어로 메달 (레벨 2/3/4/5 도달)")]
    [SerializeField] private int[] upgradeCostChip  = {   5,   6,   8,  10 };
    public IReadOnlyList<int> UpgradeCostMoney => upgradeCostMoney;
    public IReadOnlyList<int> UpgradeCostChip => upgradeCostChip;

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

    /// <summary>[JC 260617] 새 게임 초기화 — (영웅,스킬) 강화 레벨 전체 제거(첫 실행=빈 상태).</summary>
    public void Reset()
    {
        entries.Clear();
        lookup.Clear();
        OnStateChanged?.Invoke();
    }

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

    /// <summary>[JC 260617] 해당 영웅의 클래스 스킬 전체(미습득 포함). 그리드 행 표시용.</summary>
    public List<SkillData> GetClassSkills(int unitIndex)
    {
        var result = new List<SkillData>();
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null) return result;
        if (!TryResolveClass(unitIndex, out var className, out _)) return result;
        foreach (var s in catalog.GetSkillsByClass(className))
            if (s != null) result.Add(s);
        return result;
    }

    /// <summary>스킬 습득 여부(acquireLevel ≤ 영웅 레벨).</summary>
    public bool IsSkillLearned(int unitIndex, SkillData skill)
    {
        if (skill == null) return false;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(unitIndex, out var unit) || unit == null) return false;
        return skill.acquireLevel <= Mathf.Max(1, unit.Level);
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
        if (idx < 0 || idx >= upgradeCostMoney.Length) return false;
        money = upgradeCostMoney[idx];
        chip = idx < upgradeCostChip.Length ? upgradeCostChip[idx] : 0;
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
        SyncEquippedSkillLevel(unitIndex); // [JC 260616] 장착 스킬이면 DH로 writeback해 전투 반영
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
            unitIndex, d.UnitTemplateKey, d.Level,
            d.BaseStats, d.LevelupStats, skillIndex, d.CurrentWeaponIndex,
            d.CurrentWeaponStats, d.IngameStats, d.CurrentHp, d.Exp, d.MaxExp);

        if (ok)
        {
            SyncEquippedSkillLevel(unitIndex); // [JC 260616] 새 장착 스킬의 강화 레벨을 DH로 writeback
            OnStateChanged?.Invoke();
        }
        return ok;
    }

    /// <summary>현재 장착 스킬(CurrentSkillIndex)의 강화 레벨을 DH UnitPersistentData.SkillLevel로 동기.
    /// DH는 유닛당 단일 SkillLevel이므로 장착 스킬에 한정해 push한다(전투 LoadPersistentEquipment가 소비).</summary>
    private void SyncEquippedSkillLevel(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return;
        int equipped = GetEquippedSkillIndex(unitIndex);
        if (equipped <= 0) return;
        repo.SetSkillLevel(unitIndex, GetSkillLevel(unitIndex, equipped));
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
