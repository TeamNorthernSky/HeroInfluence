using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공방(Workshop) 시스템 영속 매니저. GameManager 영속 자식.
///
/// [JC 260616] 무기 보유/강화를 DH 신규 WeaponPersistentRepository "인스턴스" 모델로 결선(레거시 CurrentWeaponStats 제거).
///   - 무기 인스턴스(레벨·스탯)는 WeaponPersistentRepository가 보유. 유닛은 EquippedWeaponInstanceIndex로 장착 참조.
///   - 무기 소유는 "히어로 종속"(같은 클래스라도 공유 안 함): (unitIndex, weaponTemplate)→instanceIndex 임시 매핑.
///     ※ 임시 — 추후 DH 영속(PersistentUnitRepository 계열)으로 소유권 이관 예정.
///   - tier1(기본 제공)은 DH EnsureDefaultWeaponInstance가 부여한 인스턴스를 채택(없으면 fallback 생성).
///   - 제작 = WeaponPersistentRepository.CreateWeapon + 자동 장착. 강화 = TryEnhanceWeapon(장착 유닛 ingame 자동 갱신).
///
/// [JC 260617 확인] 전투 실반영 결선 완료: 장착/제작 시 EquipWeaponInstance가 CurrentWeaponIndex(템플릿)+
///   EquippedWeaponInstanceIndex+IngameStats(레벨별 스탯)를 갱신하고, 전투 진입(CharactorScript)이
///   LoadPersistentEquipment(…, CurrentWeaponIndex, …, EquippedWeaponInstanceIndex)로 넘기면 ASB가
///   ResolveWeaponLevel+GetWeaponSkillValueAtLevel로 스킬 계수까지 레벨별 적용한다. (별도 JC 작업 불필요)
///
/// 비용·조건 출처: H.I 자원 밸런스 데이터 테이블 V1.0 '협회-공방(제작)'/'협회-공방(강화)'.
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
    // 출처: 자원 밸런스 V1.0 '협회-공방(제작)'.
    private struct CraftCost { public int reqLevel, money, crystal; public CraftCost(int r,int m,int c){reqLevel=r;money=m;crystal=c;} }
    private static readonly Dictionary<int, CraftCost> CraftTable = new Dictionary<int, CraftCost>
    {
        { 2, new CraftCost(2,  800, 10) },
        { 3, new CraftCost(3, 1200, 15) },
    };

    // 협회-공방(강화): [tier-1][toLevel-2] → (필요 공방레벨, 자금, 수정). 출처: 자원 밸런스 V1.0 '협회-공방(강화)'.
    private static readonly int[,,] EnhanceTable =
    {
        // 하급(tier1): toLv2/3/4/5
        { {1,400,5}, {2,600,6}, {3,800, 8}, {4,1000,10} },
        // 중급(tier2)
        { {2,600,5}, {2,800,6}, {3,1000,8}, {4,1200,10} },
        // 상급(tier3)
        { {3,800,5}, {3,1000,6}, {3,1200,8}, {4,1400,10} },
    };

    [Serializable]
    public class WeaponEntry
    {
        public int unitIndex;
        public int weaponIndex;    // 무기 템플릿 인덱스(310101 등)
        public int instanceIndex;  // WeaponPersistentRepository 인스턴스 인덱스
    }

    [Header("영웅별 보유 무기 → 인스턴스 매핑 (임시, 세션 영속)")]
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
    /// <summary>하급(tier1)은 기본 보유(DH 기본 무기 인스턴스). 그 외는 제작해야 보유.</summary>
    public bool IsOwned(int unitIndex, int weaponIndex)
    {
        if (TierOf(weaponIndex) == 1) { EnsureTier1Registered(unitIndex); return true; }
        return lookup.ContainsKey(Key(unitIndex, weaponIndex));
    }

    /// <summary>해당 (영웅,무기템플릿)의 무기 인스턴스 인덱스. 미보유면 0.</summary>
    private int GetInstanceIndex(int unitIndex, int weaponIndex)
    {
        if (TierOf(weaponIndex) == 1) EnsureTier1Registered(unitIndex);
        return lookup.TryGetValue(Key(unitIndex, weaponIndex), out var e) ? e.instanceIndex : 0;
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

    /// <summary>무기 제작: 인스턴스 생성 + 보유 등록 + 자동 장착(기획서). 자원 차감은 호출자 별도.</summary>
    public bool TryCraft(int unitIndex, int weaponIndex)
    {
        if (!CanCraft(unitIndex, weaponIndex)) return false;
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (weaponRepo == null) return false;

        int inst = weaponRepo.CreateWeapon(weaponIndex);
        if (inst <= 0) return false;

        RegisterEntry(unitIndex, weaponIndex, inst);
        EquipWeapon(unitIndex, weaponIndex); // 기획서: 제작한 무기 자동 장착
        OnStateChanged?.Invoke();
        return true;
    }

    // ─── 강화 ──────────────────────────────────────────────────
    public int GetWeaponLevel(int unitIndex, int weaponIndex)
    {
        int inst = GetInstanceIndex(unitIndex, weaponIndex);
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (inst > 0 && weaponRepo != null && weaponRepo.TryGetWeapon(inst, out var data) && data != null)
            return data.Level;
        return TierOf(weaponIndex) == 1 ? BaseWeaponLevel : 0; // tier1은 기본 보유, 그 외 미보유=0
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

    /// <summary>무기 강화 한 단계: 인스턴스 레벨 +1. 장착 유닛 ingame은 WeaponPersistentRepository가 자동 갱신.</summary>
    public bool TryEnhance(int unitIndex, int weaponIndex)
    {
        if (!CanEnhance(unitIndex, weaponIndex)) return false;
        int inst = GetInstanceIndex(unitIndex, weaponIndex);
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (inst <= 0 || weaponRepo == null) return false;

        if (!weaponRepo.TryEnhanceWeapon(inst)) return false;
        OnStateChanged?.Invoke();
        return true;
    }

    // ─── 장착 ──────────────────────────────────────────────────
    /// <summary>현재 장착 무기의 "템플릿 인덱스"(UI 강조용). 미장착/불명이면 0.</summary>
    public int GetEquippedWeaponIndex(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (repo == null || weaponRepo == null) return 0;
        if (!repo.TryGetUnit(unitIndex, out var d) || d == null) return 0;
        int inst = d.EquippedWeaponInstanceIndex;
        if (inst > 0 && weaponRepo.TryGetWeaponTemplateKey(inst, out int templateKey))
            return templateKey;
        return 0;
    }

    /// <summary>장착 무기 변경 — 해당 무기 인스턴스를 유닛에 장착(ingame은 DH가 재계산).</summary>
    public bool EquipWeapon(int unitIndex, int weaponIndex)
    {
        if (!IsOwned(unitIndex, weaponIndex)) return false;
        int inst = GetInstanceIndex(unitIndex, weaponIndex);
        if (inst <= 0) return false;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;

        bool ok = repo.EquipWeaponInstance(unitIndex, inst);
        if (ok) OnStateChanged?.Invoke();
        return ok;
    }

    /// <summary>장착 무기가 없으면 기본(tier1)을 장착 — 진입 즉시 장착표시가 뜨도록. (연구소 EnsureDefaultEquipped 대응)</summary>
    public void EnsureDefaultEquipped(int unitIndex)
    {
        if (!TryResolveClass(unitIndex, out _, out int classIndex)) return;
        int tier1 = WeaponIndexOf(classIndex, 1);
        EnsureTier1Registered(unitIndex);
        if (GetEquippedWeaponIndex(unitIndex) == 0)
            EquipWeapon(unitIndex, tier1);
    }

    // ─── 내부 ──────────────────────────────────────────────────
    /// <summary>tier1(기본 무기) 인스턴스를 보유 매핑에 등록. DH 기본 인스턴스를 채택, 없으면 생성.</summary>
    private void EnsureTier1Registered(int unitIndex)
    {
        if (!TryResolveClass(unitIndex, out _, out int classIndex)) return;
        int tier1 = WeaponIndexOf(classIndex, 1);
        if (lookup.ContainsKey(Key(unitIndex, tier1))) return;

        var repo = PersistentUnitRepository.Instance;
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (repo == null || weaponRepo == null) return;

        repo.EnsureDefaultWeaponInstance(unitIndex); // 기본 무기 인스턴스 보장(idempotent)
        if (repo.TryGetUnit(unitIndex, out var d) && d != null)
        {
            int equipped = d.EquippedWeaponInstanceIndex;
            if (equipped > 0 && weaponRepo.TryGetWeaponTemplateKey(equipped, out int tk) && tk == tier1)
            {
                RegisterEntry(unitIndex, tier1, equipped); // DH 기본 인스턴스 채택
                return;
            }
        }

        // fallback: 기본 인스턴스를 못 찾으면(이미 다른 무기 장착 등) tier1 인스턴스 생성
        int created = weaponRepo.CreateWeapon(tier1);
        if (created > 0) RegisterEntry(unitIndex, tier1, created);
    }

    private WeaponEntry RegisterEntry(int unitIndex, int weaponIndex, int instanceIndex)
    {
        long k = Key(unitIndex, weaponIndex);
        if (lookup.TryGetValue(k, out var e)) { e.instanceIndex = instanceIndex; return e; }
        e = new WeaponEntry { unitIndex = unitIndex, weaponIndex = weaponIndex, instanceIndex = instanceIndex };
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
