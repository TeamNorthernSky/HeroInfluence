using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>트레이닝 강화 대상 스탯.</summary>
public enum TrainingStat
{
    Attack = 0, // 칼 아이콘 — 공격력
    Health = 1, // 하트 아이콘 — 최대 체력
}

/// <summary>
/// 트레이닝 시스템 영속 매니저. GameManager 영속 자식.
/// 영웅별·스탯별 강화 레벨(0~3)을 추적하고, 강화 시 실제 영웅의 BaseStats(공격력/체력)를 영구 가산한다.
/// 스탯 원천은 DH PersistentUnitRepository.BaseStats(전투·HeroInfo 공통). DH 코어는 공개 API만 사용(비침습).
/// 강화 레벨은 PublicityManager 패턴을 따라 int unitIndex 키 + List+Dictionary lookup으로 보관.
/// 현재 in-memory(세션 내 영속). 앱 재시작 시 BaseStats(CSV 재생성)·레벨 모두 리셋되어 일관됨.
/// 추후 UnitPersistentData/GameSession 영속 트랙에 단순 transfer로 합류 가능하도록 설계.
/// </summary>
[DisallowMultipleComponent]
public class TrainingManager : MonoBehaviour
{
    public event Action OnStateChanged;

    [Header("영웅별·스탯별 강화 레벨 (영속)")]
    [SerializeField] private List<TrainingEntry> entries = new List<TrainingEntry>();

    // 단계별 수치 — 기획서(옛 트레이닝 'DB' 시트) 기준. [JC 260617] 인스펙터 편집 가능하도록 직렬화 필드로 전환
    //   (기획자 확인 후 인스펙터에서 바로 수정). 트레이닝 레벨 L(0→1, 1→2, 2→3) 진행 시 인덱스 L 사용.
    [Header("단계별 수치 (인스펙터 편집 — 레벨 1/2/3 도달 기준)")]
    [Tooltip("공격력 강화 증가량 (레벨 1/2/3 도달 시 가산분)")]
    [SerializeField] private float[] atkGainPerLevel = { 5f, 10f, 15f };
    [Tooltip("체력 강화 증가량 (레벨 1/2/3 도달 시 가산분)")]
    [SerializeField] private float[] hpGainPerLevel = { 5f, 10f, 15f };
    [Tooltip("1회 강화 비용 — 자금 (레벨 1/2/3 도달 시)")]
    [SerializeField] private int[] costPerLevel = { 1000, 1200, 1400 };
    public const int MaxTrainingLevel = 3;

    // 외부 읽기 접근(표시/툴팁용). 편집은 인스펙터에서.
    public IReadOnlyList<float> AtkGainPerLevel => atkGainPerLevel;
    public IReadOnlyList<float> HpGainPerLevel => hpGainPerLevel;
    public IReadOnlyList<int> CostPerLevel => costPerLevel;

    [Serializable]
    public class TrainingEntry
    {
        public int unitIndex;
        public int atkLevel; // 공격력 강화 레벨 0~MaxTrainingLevel
        public int hpLevel;  // 체력 강화 레벨 0~MaxTrainingLevel
    }

    private readonly Dictionary<int, TrainingEntry> lookup = new Dictionary<int, TrainingEntry>();

    public void Initialize()
    {
        RebuildLookup();
    }

    /// <summary>[JC 260617] 새 게임 초기화 — 영웅별 강화 레벨 전체 제거(첫 실행=빈 상태).</summary>
    public void Reset()
    {
        entries.Clear();
        lookup.Clear();
        OnStateChanged?.Invoke();
    }

    // ─── 부서(트레이닝 센터) 상태 ─────────────────────────────
    public int GetDepartmentLevel()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Training) : 0;
    }

    public bool IsUnlocked() => GetDepartmentLevel() >= 1;

    /// <summary>강화 가능 최대 레벨 = min(부서 레벨, MaxTrainingLevel). 부서 레벨이 선행 게이트.</summary>
    public int GetMaxTrainableLevel() => Mathf.Min(GetDepartmentLevel(), MaxTrainingLevel);

    // ─── 영웅별 레벨 조회 ─────────────────────────────────────
    public int GetLevel(int unitIndex, TrainingStat stat)
    {
        if (!lookup.TryGetValue(unitIndex, out var e)) return 0;
        return stat == TrainingStat.Attack ? e.atkLevel : e.hpLevel;
    }

    /// <summary>다음 단계 진행 비용(자금). 더 못 올리면 -1.</summary>
    public int GetNextCost(int unitIndex, TrainingStat stat)
    {
        int level = GetLevel(unitIndex, stat);
        if (level >= GetMaxTrainableLevel()) return -1;
        if (level >= costPerLevel.Length) return -1;
        return costPerLevel[level];
    }

    /// <summary>다음 단계 진행 시 해당 스탯 증가량. 더 못 올리면 0.</summary>
    public float GetNextGain(int unitIndex, TrainingStat stat)
    {
        int level = GetLevel(unitIndex, stat);
        if (level >= GetMaxTrainableLevel()) return 0f;
        var table = stat == TrainingStat.Attack ? atkGainPerLevel : hpGainPerLevel;
        if (level >= table.Length) return 0f;
        return table[level];
    }

    /// <summary>해당 영웅·스탯을 더 강화할 수 있는지(해금 + 부서 레벨 게이트). 자금 충족은 호출자 별도 확인.</summary>
    public bool CanTrain(int unitIndex, TrainingStat stat)
    {
        if (!IsUnlocked()) return false;
        return GetLevel(unitIndex, stat) < GetMaxTrainableLevel();
    }

    /// <summary>
    /// 한 단계 강화: 레벨 +1 + 영웅 BaseStats에 증가분 영구 가산. 자금 차감은 호출자(TrainingModalController)가 별도 처리.
    /// </summary>
    public bool TryTrain(int unitIndex, TrainingStat stat)
    {
        if (!CanTrain(unitIndex, stat)) return false;

        float atkDelta = 0f, hpDelta = 0f;
        if (stat == TrainingStat.Attack) atkDelta = GetNextGain(unitIndex, stat);
        else hpDelta = GetNextGain(unitIndex, stat);

        if (!ApplyStatBoost(unitIndex, atkDelta, hpDelta)) return false;

        var e = GetOrCreateEntry(unitIndex);
        if (stat == TrainingStat.Attack) e.atkLevel++;
        else e.hpLevel++;

        OnStateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 영웅 BaseStats(공격력/체력)에 증가분을 영구 가산하고 ingameStats를 재계산해 writeback한다.
    /// 전투는 BaseStats를 직접 사용(레벨 스케일링 off)하고 HeroInfo도 BaseStats를 읽으므로 즉시 반영된다.
    /// </summary>
    private bool ApplyStatBoost(int unitIndex, float atkDelta, float hpDelta)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return false;
        if (!repo.TryGetUnit(unitIndex, out var data) || data == null) return false;

        StatBlock newBase = data.BaseStats; // struct 복사본
        newBase.Atk += atkDelta;
        newBase.HP += hpDelta;
        newBase.ClampToMinimumOne();

        // [JC 260618] ingame(무기분 포함) 재계산은 RefreshEquippedWeaponStats에 위임 → CurrentWeaponStats 비의존
        //   (DH가 코어에서 currentWeaponStats 제거 예정. 무기 스탯은 장착 인스턴스에서 레포가 직접 조회).
        //   현재 HP를 hpDelta만큼 미리 올려두면, Refresh의 Clamp(currentHp, 0, 새 최대체력)가
        //   "최대 체력 증가분만큼 현재 체력도 증가"를 자연 충족한다(팀 합의). 공격력 강화 시 hpDelta=0이라 불변.
        float bumpedHp = data.CurrentHp + hpDelta;
        bool ok = repo.UpdateUnitRuntimeState(
            unitIndex, data.UnitTemplateKey, data.Level,
            newBase, data.LevelupStats, data.CurrentSkillIndex, data.CurrentWeaponIndex,
            data.CurrentWeaponStats, data.IngameStats, bumpedHp, data.Exp, data.MaxExp);
        if (!ok) return false;

        // 장착 무기 인스턴스 스탯 기준으로 ingameStats 재계산 + currentHp를 새 최대치로 Clamp.
        return repo.RefreshEquippedWeaponStats(unitIndex);
    }

    private TrainingEntry GetOrCreateEntry(int unitIndex)
    {
        if (lookup.TryGetValue(unitIndex, out var e)) return e;
        e = new TrainingEntry { unitIndex = unitIndex, atkLevel = 0, hpLevel = 0 };
        entries.Add(e);
        lookup[unitIndex] = e;
        return e;
    }

    private void RebuildLookup()
    {
        lookup.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.unitIndex <= 0) continue;
            if (lookup.ContainsKey(e.unitIndex))
            {
                Debug.LogWarning($"[TrainingManager] duplicate unitIndex {e.unitIndex}", this);
                continue;
            }
            lookup.Add(e.unitIndex, e);
        }
    }
}
