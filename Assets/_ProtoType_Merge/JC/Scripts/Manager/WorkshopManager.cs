using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공방(Workshop) 시스템 영속 매니저. GameManager 영속 자식.
///
/// [KJ 260729] 2단계 — 계정 공유 모델로 전환. 기획 변경(HC001~HC005, 티어 폐지, 전 클래스 공용 5종) 대응.
///   - 소유는 계정 단위: entries/lookup의 키가 weaponIndex(1~5) 하나다. 유닛 축이 없다.
///   - 장착은 유닛 단위: PersistentUnitRepository.EquippedWeaponInstanceIndex가 유일한 출처이므로
///     장착 API(GetEquippedWeaponIndex/EquipWeapon/EnsureDefaultEquipped)의 unitIndex는 그대로 유지된다.
///   - 템플릿당 인스턴스 1개 → 코어를 강화하면 장착한 모든 영웅에 동시 반영(기획 의도).
///   - 제작 = CreateWeapon + 소유 등록까지. 자동 장착은 하지 않는다(장착은 모달의 「장착」 버튼 전담).
///   - 자원 차감은 호출자(WorkshopModalController) 담당. 매니저는 상태만 바꾼다.
///
/// [JC 260617 확인] 전투 실반영 경로는 그대로다: EquipWeaponInstance가 CurrentWeaponIndex(템플릿)+
///   EquippedWeaponInstanceIndex+IngameStats를 갱신하고, 전투 진입(CharactorScript)이 LoadPersistentEquipment로
///   넘기면 ASB가 ResolveWeaponLevel+GetWeaponSkillValueAtLevel로 스킬 계수까지 레벨별 적용한다.
///
/// 비용 수치는 기획 미확정 임시값이다(구 테이블 이식 + 등차 외삽). 확정 시 CraftTable/EnhanceTable만 교체한다.
/// 설계: docs/superpowers/specs/2026-07-29-workshop-phase2-design.md
/// </summary>
[DisallowMultipleComponent]
public class WorkshopManager : MonoBehaviour
{
    public event Action OnStateChanged;

    public const int BaseWeaponLevel = 1;
    public const int MaxWeaponLevel = 5;

    /// <summary>코어 무기 템플릿 키 범위. DHCsvTemplateCatalog가 HC001~HC005를 정수 1~5로 노출한다.</summary>
    public const int MinWeaponIndex = 1;
    public const int MaxWeaponIndex = 5;

    /// <summary>기본 보유 코어(HC001). 제작 대상이 아니다.</summary>
    public const int DefaultWeaponIndex = 1;
    public const string DefaultWeaponKey = "HC001";

    private struct CraftCost
    {
        public int reqLevel, money, crystal;
        public CraftCost(int r, int m, int c) { reqLevel = r; money = m; crystal = c; }
    }

    // [KJ 260729] 필요 공방레벨 상한은 3이다. HQStateManager.defaultMaxLevel=3이고 GameManager.prefab의
    //   maxLevels 오버라이드에 Workshop(2) 항목이 없어 공방은 Lv.3을 넘지 못한다. 4를 요구하면 영구 도달 불가.
    //   기획이 상한을 올리면 이 테이블과 함께 부서 maxLevel도 조정해야 한다.
    private const int MaxDepartmentLevel = 3;

    // 제작: weaponIndex(2~5) → (필요 공방레벨, 자금, 수정). 코어 1은 기본 보유라 항목이 없다.
    // 임시값 — 구 CraftTable[tier2]/[tier3]을 이식하고 코어 4·5는 +400/+5 등차로 외삽. 기획 확정 시 교체.
    private static readonly Dictionary<int, CraftCost> CraftTable = new Dictionary<int, CraftCost>
    {
        { 2, new CraftCost(2,  800, 10) },
        { 3, new CraftCost(3, 1200, 15) },
        { 4, new CraftCost(MaxDepartmentLevel, 1600, 20) },
        { 5, new CraftCost(MaxDepartmentLevel, 2000, 25) },
    };

    // 강화: [toLevel-2] → (필요 공방레벨, 자금, 수정). 티어 폐지로 전 코어 공통 4행.
    // 임시값 — 구 EnhanceTable 하급(tier1) 행 채택. 기획 확정 시 교체.
    private static readonly int[,] EnhanceTable =
    {
        { 1,                    400,  5 },   // toLv2
        { 2,                    600,  6 },   // toLv3
        { 3,                    800,  8 },   // toLv4
        { MaxDepartmentLevel,  1000, 10 },   // toLv5 — 구 테이블은 4를 요구했으나 도달 불가라 3으로 낮춤
    };

    [Serializable]
    public class WeaponEntry
    {
        public int weaponIndex;    // UI slot and legacy bridge index (1~5)
        public string weaponKey;   // DataTable template key (HC001~HC005)
        public int instanceIndex;  // WeaponPersistentRepository 인스턴스 인덱스
    }

    [Header("계정 보유 코어 → 인스턴스 매핑 (세션 영속)")]
    [SerializeField] private List<WeaponEntry> entries = new List<WeaponEntry>();

    // [KJ 260706] 저장 기능(GameSaveService)용 읽기 노출 — 보유 매핑이 없으면 로드 시 제작 코어가 미보유로 보인다.
    public IReadOnlyList<WeaponEntry> Entries => entries;

    private readonly Dictionary<int, WeaponEntry> lookup = new Dictionary<int, WeaponEntry>();

    public void Initialize() => RebuildLookup();

    /// <summary>새 게임 초기화 — 계정 보유 매핑 전체 제거(첫 진입 시 코어 1만 기본 보유).</summary>
    public void Reset()
    {
        entries.Clear();
        lookup.Clear();
        OnStateChanged?.Invoke();
    }

    private static bool IsValidWeaponIndex(int weaponIndex)
        => weaponIndex >= MinWeaponIndex && weaponIndex <= MaxWeaponIndex;

    // ─── 부서(공방) 상태 ───────────────────────────────────────
    public int GetDepartmentLevel()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Workshop) : 0;
    }

    public bool IsUnlocked() => GetDepartmentLevel() >= 1;

    // ─── 목록 ─────────────────────────────────────────────────
    /// <summary>코어 1~5 전체. 전 클래스 공용이므로 인자를 받지 않는다.</summary>
    public List<int> GetAllWeapons()
    {
        var result = new List<int>(MaxWeaponIndex - MinWeaponIndex + 1);
        for (int w = MinWeaponIndex; w <= MaxWeaponIndex; w++) result.Add(w);
        return result;
    }

    // ─── 소유 (계정 단위) ──────────────────────────────────────
    /// <summary>코어 1은 기본 보유(인스턴스를 보장한다). 그 외는 제작해야 보유.</summary>
    public bool IsOwned(int weaponIndex)
    {
        if (!IsValidWeaponIndex(weaponIndex)) return false;
        if (weaponIndex == DefaultWeaponIndex) EnsureDefaultOwned();
        return lookup.ContainsKey(weaponIndex);
    }

    /// <summary>해당 코어의 무기 인스턴스 인덱스. 미보유면 0.</summary>
    private int GetInstanceIndex(int weaponIndex)
    {
        if (!IsValidWeaponIndex(weaponIndex)) return 0;
        if (weaponIndex == DefaultWeaponIndex) EnsureDefaultOwned();
        return lookup.TryGetValue(weaponIndex, out var e) ? e.instanceIndex : 0;
    }

    // ─── 제작 ──────────────────────────────────────────────────
    public bool GetCraftCost(int weaponIndex, out int reqLevel, out int money, out int crystal)
    {
        reqLevel = -1; money = -1; crystal = -1;
        if (!CraftTable.TryGetValue(weaponIndex, out var c)) return false;
        reqLevel = c.reqLevel; money = c.money; crystal = c.crystal;
        return true;
    }

    public bool CanCraft(int weaponIndex)
    {
        if (!IsUnlocked()) return false;
        if (!IsValidWeaponIndex(weaponIndex)) return false;
        if (IsOwned(weaponIndex)) return false;
        if (!GetCraftCost(weaponIndex, out int reqLevel, out _, out _)) return false;
        return GetDepartmentLevel() >= reqLevel;
    }

    /// <summary>코어 제작: 인스턴스 생성 + 계정 소유 등록. 자원 차감·장착은 호출자 담당.</summary>
    public bool TryCraft(int weaponIndex)
    {
        if (!CanCraft(weaponIndex)) return false;
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (weaponRepo == null) return false;

        string weaponKey = ResolveWeaponKey(weaponIndex);
        int inst = weaponRepo.CreateWeapon(weaponKey);
        if (inst <= 0) return false;

        RegisterEntry(weaponIndex, weaponKey, inst);
        OnStateChanged?.Invoke();
        return true;
    }

    // ─── 강화 ──────────────────────────────────────────────────
    /// <summary>해당 코어의 현재 강화 레벨. 미보유면 0.</summary>
    public int GetWeaponLevel(int weaponIndex)
    {
        int inst = GetInstanceIndex(weaponIndex);
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (inst > 0 && weaponRepo != null && weaponRepo.TryGetWeapon(inst, out var data) && data != null)
            return data.Level;
        return 0;
    }

    /// <summary>currentLevel → currentLevel+1 강화 비용. 티어 폐지로 전 코어 공통이다.</summary>
    public bool GetEnhanceCost(int weaponIndex, int currentLevel, out int reqLevel, out int money, out int crystal)
    {
        reqLevel = -1; money = -1; crystal = -1;
        if (!IsValidWeaponIndex(weaponIndex)) return false;
        if (currentLevel < BaseWeaponLevel || currentLevel >= MaxWeaponLevel) return false;
        int row = currentLevel - BaseWeaponLevel;   // current1 → row0(=toLv2)
        reqLevel = EnhanceTable[row, 0];
        money    = EnhanceTable[row, 1];
        crystal  = EnhanceTable[row, 2];
        return true;
    }

    public bool CanEnhance(int weaponIndex)
    {
        if (!IsUnlocked()) return false;
        if (!IsOwned(weaponIndex)) return false;
        int level = GetWeaponLevel(weaponIndex);
        if (level < BaseWeaponLevel || level >= MaxWeaponLevel) return false;
        if (!GetEnhanceCost(weaponIndex, level, out int reqLevel, out _, out _)) return false;
        return GetDepartmentLevel() >= reqLevel;
    }

    /// <summary>코어 강화 한 단계. 인스턴스가 계정 공유이므로 장착한 모든 영웅에 동시 반영된다.</summary>
    public bool TryEnhance(int weaponIndex)
    {
        if (!CanEnhance(weaponIndex)) return false;
        int inst = GetInstanceIndex(weaponIndex);
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (inst <= 0 || weaponRepo == null) return false;

        if (!weaponRepo.TryEnhanceWeapon(inst)) return false;
        OnStateChanged?.Invoke();
        return true;
    }

    // ─── 장착 (유닛 단위 — unitIndex 유지) ─────────────────────
    /// <summary>현재 장착 코어의 템플릿 키(UI 강조용). 미장착/불명이면 0.</summary>
    public int GetEquippedWeaponIndex(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (repo == null || weaponRepo == null) return 0;
        if (!repo.TryGetUnit(unitIndex, out var d) || d == null) return 0;
        int inst = d.EquippedWeaponInstanceIndex;
        if (inst > 0 && weaponRepo.TryGetWeaponTemplateKey(inst, out string templateKey))
            return ResolveWeaponIndex(templateKey);
        return 0;
    }

    /// <summary>장착 코어 변경 — 계정 인스턴스를 유닛에 장착(ingame은 DH가 재계산).</summary>
    public bool EquipWeapon(int unitIndex, int weaponIndex)
    {
        if (!IsOwned(weaponIndex)) return false;
        int inst = GetInstanceIndex(weaponIndex);
        if (inst <= 0) return false;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;

        bool ok = repo.EquipWeaponInstance(unitIndex, inst);
        if (ok) OnStateChanged?.Invoke();
        return ok;
    }

    /// <summary>[KJ 260729] 장착 인스턴스가 계정 소유 집합에 없으면 코어 1(계정 인스턴스)로 정정한다.
    /// DH PersistentUnitRepository.EnsureDefaultWeaponInstance()가 유닛별로 따로 만든 기본 인스턴스를
    /// 그대로 두면 "템플릿당 인스턴스 1개" 불변식이 깨져 강화 공유가 반영되지 않는다.</summary>
    public void EnsureDefaultEquipped(int unitIndex)
    {
        EnsureDefaultOwned();
        if (HasValidEquippedWeapon(unitIndex))
        {
            Debug.Log($"[DHWeaponInit] Workshop EnsureDefaultEquipped keeps valid equipped weapon. unitIndex={unitIndex}", this);
            return;
        }

        if (IsAccountInstanceEquipped(unitIndex))
        {
            Debug.Log($"[DHWeaponInit] Workshop EnsureDefaultEquipped keeps account equipped weapon. unitIndex={unitIndex}", this);
            return;
        }

        Debug.Log($"[DHWeaponInit] Workshop EnsureDefaultEquipped falls back to '{DefaultWeaponKey}'. unitIndex={unitIndex}", this);
        EquipWeapon(unitIndex, DefaultWeaponIndex);
    }

    // ─── 내부 ──────────────────────────────────────────────────
    /// <summary>코어 1(기본 보유)의 계정 인스턴스를 보장(멱등).</summary>
    public void EnsureDefaultOwned()
    {
        if (lookup.ContainsKey(DefaultWeaponIndex)) return;
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (weaponRepo == null) return;

        int inst = weaponRepo.CreateWeapon(DefaultWeaponKey);
        if (inst > 0) RegisterEntry(DefaultWeaponIndex, DefaultWeaponKey, inst);
    }

    /// <summary>유닛이 장착한 인스턴스가 계정 소유 인스턴스 중 하나인지.</summary>
    private bool IsAccountInstanceEquipped(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;
        if (!repo.TryGetUnit(unitIndex, out var d) || d == null) return false;
        int equipped = d.EquippedWeaponInstanceIndex;
        if (equipped <= 0) return false;

        foreach (var kv in lookup)
            if (kv.Value != null && kv.Value.instanceIndex == equipped) return true;
        return false;
    }

    private bool HasValidEquippedWeapon(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        var weaponRepo = WeaponPersistentRepository.Instance;
        if (repo == null || weaponRepo == null) return false;
        if (!repo.TryGetUnit(unitIndex, out var d) || d == null) return false;

        int equipped = d.EquippedWeaponInstanceIndex;
        if (equipped <= 0) return false;
        if (!weaponRepo.TryGetWeaponTemplateKey(equipped, out string templateKey)) return false;

        int weaponIndex = ResolveWeaponIndex(templateKey);
        if (!IsValidWeaponIndex(weaponIndex)) return true;

        RegisterEntry(weaponIndex, templateKey, equipped);
        return true;
    }

    private WeaponEntry RegisterEntry(int weaponIndex, string weaponKey, int instanceIndex)
    {
        weaponKey = NormalizeWeaponKey(weaponKey);
        if (lookup.TryGetValue(weaponIndex, out var e))
        {
            e.weaponKey = weaponKey;
            e.instanceIndex = instanceIndex;
            return e;
        }

        e = new WeaponEntry { weaponIndex = weaponIndex, weaponKey = weaponKey, instanceIndex = instanceIndex };
        entries.Add(e);
        lookup[weaponIndex] = e;
        return e;
    }

    private void RebuildLookup()
    {
        lookup.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || !IsValidWeaponIndex(e.weaponIndex)) continue;
            if (string.IsNullOrWhiteSpace(e.weaponKey))
                e.weaponKey = ResolveWeaponKey(e.weaponIndex);
            if (lookup.ContainsKey(e.weaponIndex))
            {
                Debug.LogWarning($"[WorkshopManager] 중복 보유 항목 무시 (weaponIndex={e.weaponIndex})", this);
                continue;
            }
            lookup.Add(e.weaponIndex, e);
        }
    }

    private static string ResolveWeaponKey(int weaponIndex)
    {
        if (!IsValidWeaponIndex(weaponIndex))
            return string.Empty;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null &&
            catalog.TryGetWeaponTemplate(weaponIndex, out DHWeaponTemplate template) &&
            template != null &&
            !string.IsNullOrWhiteSpace(template.WeaponKey))
        {
            return template.WeaponKey.Trim();
        }

        return $"HC{weaponIndex:000}";
    }

    private static int ResolveWeaponIndex(string weaponKey)
    {
        weaponKey = NormalizeWeaponKey(weaponKey);
        if (string.IsNullOrEmpty(weaponKey))
            return 0;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null &&
            catalog.TryGetWeaponTemplate(weaponKey, out DHWeaponTemplate template) &&
            template != null)
        {
            return template.NumericWeaponId;
        }

        int start = -1;
        for (int i = 0; i < weaponKey.Length; i++)
        {
            if (char.IsDigit(weaponKey[i]))
            {
                start = i;
                break;
            }
        }

        return start >= 0 && int.TryParse(weaponKey.Substring(start), out int parsed) ? parsed : 0;
    }

    private static string NormalizeWeaponKey(string weaponKey)
    {
        return string.IsNullOrWhiteSpace(weaponKey) ? string.Empty : weaponKey.Trim();
    }
}
