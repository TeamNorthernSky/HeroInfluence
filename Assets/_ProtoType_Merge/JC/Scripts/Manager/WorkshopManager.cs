using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공방(Workshop) 시스템 영속 매니저. GameManager 영속 자식. TrainingManager 패턴 복제.
/// 무기(코어) 제작 / 강화 / 장착을 다룬다.
///   1) 무기 장착 — CurrentWeaponIndex + CurrentWeaponStats(레벨 반영) writeback + ingame 재계산 (실결선/비침습).
///   2) 무기 스탯 강화 — DHCsvTemplateCatalog.TryGetWeaponBonusAtLevel로 레벨별 스탯 적용 (실결선).
///   3) 무기 제작 — 영웅별 보유 무기 집합에 추가 (JC측 in-memory).
///
/// ※ seam 정책([[feedback_seam_interface_policy]]): 무기 "스킬 계수"의 전투 반영은 ASB가 단일 WeaponData를
///   로드하므로 보류(더미). 무기 스탯(HP/ATK/DEF 등)은 currentWeaponStats 경유로 실제 반영된다.
///
/// 비용·조건 출처: H.I 자원 데이터 테이블 V1.4 '협회-공방(제작)'/'협회-공방(강화)' 시트.
/// 무기 인덱스 체계: 310000 + classIndex*100 + tier (tier 1 하급 / 2 중급 / 3 상급).
/// </summary>
[DisallowMultipleComponent]
public class WorkshopManager : MonoBehaviour
{
    public event Action OnStateChanged;

    public const int BaseWeaponLevel = 1;
    public const int MaxWeaponLevel = 5;
    public const int WeaponIndexBase = 310000;

    private static readonly Dictionary<string, int> ClassNameToIndex = new Dictionary<string, int>
    {
        { "가디언", 1 }, { "블래스터", 2 }, { "스트라이커", 3 }, { "서포터", 4 }, { "파이터", 5 },
    };

    // 협회-공방(제작): tier(2 중급 / 3 상급)별 (필요 공방레벨, 자금, 수정). tier1 하급은 기본 보유.
    private struct CraftCost { public int reqLevel, money, crystal; public CraftCost(int r,int m,int c){reqLevel=r;money=m;crystal=c;} }
    private static readonly Dictionary<int, CraftCost> CraftTable = new Dictionary<int, CraftCost>
    {
        { 2, new CraftCost(2, 1000,  50) },
        { 3, new CraftCost(3, 2000, 100) },
    };

    // 협회-공방(강화): [tier-1][toLevel-2] → (필요 공방레벨, 자금, 수정).
    private static readonly int[,,] EnhanceTable =
    {
        // 하급(tier1): toLv2/3/4/5
        { {1,1000,30}, {2,1500,60}, {3,2000, 90}, {4,2500,120} },
        // 중급(tier2)
        { {2,1500,45}, {2,2000,75}, {3,2500,105}, {4,3000,135} },
        // 상급(tier3)
        { {3,2000,60}, {3,2500,90}, {3,3000,120}, {4,3500,150} },
    };

    [Serializable]
    public class WeaponEntry
    {
        public int unitIndex;
        public int weaponIndex;
        public int level = BaseWeaponLevel; // 강화 레벨 1~5
    }

    [Header("영웅별 보유 무기 + 강화 레벨 (영속)")]
    [SerializeField] private List<WeaponEntry> entries = new List<WeaponEntry>();

    private readonly Dictionary<long, WeaponEntry> lookup = new Dictionary<long, WeaponEntry>();

    public void Initialize() => RebuildLookup();

    private static long Key(int unitIndex, int weaponIndex) => ((long)unitIndex << 32) | (uint)weaponIndex;

    public static int TierOf(int weaponIndex) => weaponIndex % 100; // 1/2/3
    public static int WeaponIndexOf(int classIndex, int tier) => WeaponIndexBase + classIndex * 100 + tier;

    // ─── 부서(공방) 상태 ───────────────────────────────────────
    public int GetDepartmentLevel()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Workshop) : 0;
    }

    public bool IsUnlocked() => GetDepartmentLevel() >= 1;

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

    /// <summary>해당 영웅 클래스의 전체 무기 인덱스(하급/중급/상급), 카탈로그에 존재하는 것만.</summary>
    public List<int> GetClassWeaponIndices(int unitIndex)
    {
        var result = new List<int>();
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null) return result;
        if (!TryResolveClass(unitIndex, out _, out int classIndex)) return result;
        for (int tier = 1; tier <= 3; tier++)
        {
            int w = WeaponIndexOf(classIndex, tier);
            if (catalog.TryGetWeapon(w, out _)) result.Add(w);
        }
        return result;
    }

    // ─── 보유 여부 ─────────────────────────────────────────────
    /// <summary>하급(tier1)은 기본 보유. 그 외는 제작해야 보유.</summary>
    public bool IsOwned(int unitIndex, int weaponIndex)
    {
        if (TierOf(weaponIndex) == 1) return true;
        return lookup.ContainsKey(Key(unitIndex, weaponIndex));
    }

    // ─── 제작 ──────────────────────────────────────────────────
    public bool GetCraftCost(int weaponIndex, out int reqLevel, out int money, out int crystal)
    {
        reqLevel = -1; money = -1; crystal = -1;
        int tier = TierOf(weaponIndex);
        if (!CraftTable.TryGetValue(tier, out var c)) return false;
        reqLevel = c.reqLevel; money = c.money; crystal = c.crystal;
        return true;
    }

    public bool CanCraft(int unitIndex, int weaponIndex)
    {
        if (!IsUnlocked()) return false;
        if (IsOwned(unitIndex, weaponIndex)) return false;
        if (!GetCraftCost(weaponIndex, out int reqLevel, out _, out _)) return false;
        return GetDepartmentLevel() >= reqLevel;
    }

    /// <summary>무기 제작: 보유 집합에 추가(레벨 1). 자원 차감은 호출자 별도. 제작 후 자동 장착(기획서).</summary>
    public bool TryCraft(int unitIndex, int weaponIndex)
    {
        if (!CanCraft(unitIndex, weaponIndex)) return false;
        GetOrCreateEntry(unitIndex, weaponIndex); // 레벨 1로 보유 등록
        EquipWeapon(unitIndex, weaponIndex);      // 기획서: 제작한 무기 자동 장착
        OnStateChanged?.Invoke();
        return true;
    }

    // ─── 강화 ──────────────────────────────────────────────────
    public int GetWeaponLevel(int unitIndex, int weaponIndex)
    {
        return lookup.TryGetValue(Key(unitIndex, weaponIndex), out var e) ? e.level
             : (TierOf(weaponIndex) == 1 ? BaseWeaponLevel : 0); // 미보유는 0
    }

    public bool GetEnhanceCost(int weaponIndex, int currentLevel, out int reqLevel, out int money, out int crystal)
    {
        reqLevel = -1; money = -1; crystal = -1;
        int tier = TierOf(weaponIndex);
        if (tier < 1 || tier > 3) return false;
        if (currentLevel < 1 || currentLevel >= MaxWeaponLevel) return false;
        int toIdx = currentLevel - 1; // current1→idx0(=toLv2)
        reqLevel = EnhanceTable[tier - 1, toIdx, 0];
        money    = EnhanceTable[tier - 1, toIdx, 1];
        crystal  = EnhanceTable[tier - 1, toIdx, 2];
        return true;
    }

    public bool CanEnhance(int unitIndex, int weaponIndex)
    {
        if (!IsUnlocked()) return false;
        if (!IsOwned(unitIndex, weaponIndex)) return false;
        int level = GetWeaponLevel(unitIndex, weaponIndex);
        if (level < 1 || level >= MaxWeaponLevel) return false;
        if (!GetEnhanceCost(weaponIndex, level, out int reqLevel, out _, out _)) return false;
        return GetDepartmentLevel() >= reqLevel;
    }

    /// <summary>무기 강화 한 단계: 레벨 +1. 장착 중이면 currentWeaponStats 즉시 재적용(실 스탯 반영).</summary>
    public bool TryEnhance(int unitIndex, int weaponIndex)
    {
        if (!CanEnhance(unitIndex, weaponIndex)) return false;
        var e = GetOrCreateEntry(unitIndex, weaponIndex);
        e.level = Mathf.Min(MaxWeaponLevel, e.level + 1);

        if (GetEquippedWeaponIndex(unitIndex) == weaponIndex)
            EquipWeapon(unitIndex, weaponIndex); // 스탯 재적용

        OnStateChanged?.Invoke();
        return true;
    }

    // ─── 장착 ──────────────────────────────────────────────────
    public int GetEquippedWeaponIndex(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo != null && repo.TryGetUnit(unitIndex, out var d) && d != null) return d.CurrentWeaponIndex;
        return 0;
    }

    /// <summary>장착 무기 변경 — currentWeaponIndex + 레벨 반영 currentWeaponStats writeback + ingame 재계산.</summary>
    public bool EquipWeapon(int unitIndex, int weaponIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        var catalog = DHCsvTemplateCatalog.Instance;
        if (repo == null || catalog == null) return false;
        if (!repo.TryGetUnit(unitIndex, out var d) || d == null) return false;
        if (!IsOwned(unitIndex, weaponIndex)) return false;

        int level = Mathf.Max(BaseWeaponLevel, GetWeaponLevel(unitIndex, weaponIndex));
        EquipmentStatBlock weaponStats = ResolveWeaponStats(weaponIndex, level);

        var levelUpTemplates = catalog.GetLevelUpTemplates();
        StatBlock newIngame = UnitStatCalculator.CalculateIngameStats(
            d.BaseStats, d.LevelupStats, d.Level, weaponStats, levelUpTemplates);

        // 최대 체력 변화에 맞춰 현재 HP 클램프(상한만; 협회 방문은 회복 보장).
        float newHp = Mathf.Min(d.CurrentHp, newIngame.HP);

        bool ok = repo.UpdateUnitRuntimeState(
            unitIndex, d.UnitTemplateKey, d.Level, d.Favorability,
            d.BaseStats, d.LevelupStats, d.CurrentSkillIndex, weaponIndex,
            weaponStats, newIngame, newHp, d.Exp, d.MaxExp);

        if (ok) OnStateChanged?.Invoke();
        return ok;
    }

    private EquipmentStatBlock ResolveWeaponStats(int weaponIndex, int level)
    {
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null && catalog.TryGetWeaponBonusAtLevel(weaponIndex, level, out StatBlock b))
        {
            return new EquipmentStatBlock(b.HP, b.Atk, b.DEF, b.CriticalRate, b.CounterRate, b.AvoidRate, b.Speed);
        }
        if (catalog != null && catalog.TryGetWeaponStats(weaponIndex, out EquipmentStatBlock eq))
            return eq;
        return default;
    }

    // ─── 내부 ──────────────────────────────────────────────────
    private WeaponEntry GetOrCreateEntry(int unitIndex, int weaponIndex)
    {
        long k = Key(unitIndex, weaponIndex);
        if (lookup.TryGetValue(k, out var e)) return e;
        e = new WeaponEntry { unitIndex = unitIndex, weaponIndex = weaponIndex, level = BaseWeaponLevel };
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
            if (e == null || e.unitIndex <= 0 || e.weaponIndex <= 0) continue;
            long k = Key(e.unitIndex, e.weaponIndex);
            if (lookup.ContainsKey(k)) { Debug.LogWarning($"[WorkshopManager] dup ({e.unitIndex},{e.weaponIndex})", this); continue; }
            lookup.Add(k, e);
        }
    }
}
