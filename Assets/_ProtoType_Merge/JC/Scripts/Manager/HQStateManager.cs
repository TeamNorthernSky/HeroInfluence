using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class HQStateManager : MonoBehaviour
{
    public event Action OnStateChanged;

    [Header("단계 상한 (부서별. List에 없으면 defaultMaxLevel 적용)")]
    [SerializeField] private int defaultMaxLevel = 3;
    [SerializeField] private List<MaxLevelEntry> maxLevels = new List<MaxLevelEntry>();

    [Header("초기 단계 — 본부만 1, 나머지 0(미해금)")]
    [SerializeField] private int hqInitialLevel = 1;

    [Header("업그레이드 비용 폴백 (List 엔트리 없는 부서·단계에 적용)")]
    [SerializeField] private int defaultCostMoney = 1000;
    [SerializeField] private int defaultCostChip;
    [SerializeField] private int defaultCostCrystal;
    [SerializeField] private int defaultCostSupply;

    [Header("부서·단계별 업그레이드 비용 (추후 CSV 로드 갈아끼움)")]
    [SerializeField] private List<UpgradeCostEntry> upgradeCosts = new List<UpgradeCostEntry>();

    [Header("부서별 선행 조건 (부서 N이 1단계 되려면 의존 부서들이 모두 충족)")]
    [SerializeField] private List<PrerequisiteEntry> prerequisites = new List<PrerequisiteEntry>();

    [Header("본부 매턴 자금 (단계별, 1단계부터). 추후 CSV 가능")]
    [SerializeField] private int[] hqTurnIncome = new[] { 1000, 1500, 2000 };

    [Serializable]
    public class MaxLevelEntry
    {
        public HQDepartment department;
        public int maxLevel = 3;
    }

    [Serializable]
    public class UpgradeCostEntry
    {
        public HQDepartment department;
        [Tooltip("이 단계에서 다음 단계로 강화할 때의 비용. 0이면 0→1 해금")]
        public int fromLevel;
        public int money;
        public int chip;
        public int crystal;
        public int supply;
    }

    [Serializable]
    public class PrerequisiteEntry
    {
        public HQDepartment department;
        public List<RequirementItem> requirements = new List<RequirementItem>();
    }

    [Serializable]
    public class RequirementItem
    {
        public HQDepartment dependsOn;
        public int requiredLevel = 1;
    }

    private readonly Dictionary<HQDepartment, int> levels = new Dictionary<HQDepartment, int>();

    /// <summary>[KJ 260811] 턴당 1회 제한 폐지 — 항상 false. 시그니처를 남기는 이유는
    /// DHGlobalSnapshotSections.CaptureFromRuntime이 직접 참조해 지우면 컴파일 에러가 나기 때문.
    /// DH 영역 정리 협의 후 이 프로퍼티와 GameSaveData.hqUpgradedThisTurn을 함께 제거할 것.</summary>
    //public bool UpgradedThisTurn => false;

    public void Initialize()
    {
        levels.Clear();
        foreach (HQDepartment d in Enum.GetValues(typeof(HQDepartment)))
        {
            levels[d] = (d == HQDepartment.Headquarters || d == HQDepartment.Infirmary) ? hqInitialLevel : 0;
            //Debug.Log($"{d}는 {levels[d]}");
        }
    }

    /// <summary>[JC 260617] 새 게임 초기화 — 첫 실행과 동일(본부=초기레벨, 나머지 0).</summary>
    public void Reset()
    {
        Initialize();
        OnStateChanged?.Invoke();
    }

    public int GetLevel(HQDepartment d) => levels.TryGetValue(d, out int v) ? v : 0;

    public int GetMaxLevel(HQDepartment d)
    {
        if (maxLevels != null)
        {
            for (int i = 0; i < maxLevels.Count; i++)
            {
                var e = maxLevels[i];
                if (e != null && e.department == d) return Mathf.Max(1, e.maxLevel);
            }
        }
        return defaultMaxLevel;
    }

    public int GetTurnIncome(HQDepartment d) => GetTurnIncomeAt(d, GetLevel(d));

    public int GetTurnIncomeAt(HQDepartment d, int level)
    {
        if (d != HQDepartment.Headquarters) return 0;
        if (level <= 0 || hqTurnIncome == null) return 0;
        int idx = Mathf.Clamp(level - 1, 0, hqTurnIncome.Length - 1);
        return hqTurnIncome[idx];
    }

    public IReadOnlyDictionary<ResourceType, int> GetUpgradeCost(HQDepartment d, int currentLevel)
    {
        var entry = FindCostEntry(d, currentLevel);
        return new Dictionary<ResourceType, int>
        {
            { ResourceType.Money, entry != null ? entry.money : defaultCostMoney },
            { ResourceType.Chip, entry != null ? entry.chip : defaultCostChip },
            { ResourceType.Crystal, entry != null ? entry.crystal : defaultCostCrystal },
            { ResourceType.Supply, entry != null ? entry.supply : defaultCostSupply },
        };
    }

    private UpgradeCostEntry FindCostEntry(HQDepartment d, int fromLevel)
    {
        if (upgradeCosts == null) return null;
        for (int i = 0; i < upgradeCosts.Count; i++)
        {
            var e = upgradeCosts[i];
            if (e != null && e.department == d && e.fromLevel == fromLevel) return e;
        }
        return null;
    }

    public bool ArePrerequisitesMet(HQDepartment d)
    {
        var entry = FindPrerequisiteEntry(d);
        if (entry == null || entry.requirements == null) return true;
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            var req = entry.requirements[i];
            if (req == null) continue;
            if (GetLevel(req.dependsOn) < req.requiredLevel) return false;
        }
        return true;
    }

    /// <summary>
    /// 미충족 사유 텍스트. 예: "홍보 1단계 필요", "공방 1단계, 연구소 1단계 필요".
    /// 모두 충족 또는 항목 없음이면 빈 문자열.
    /// </summary>
    public string GetUnmetReasonText(HQDepartment d)
    {
        var entry = FindPrerequisiteEntry(d);
        if (entry == null || entry.requirements == null) return string.Empty;
        var sb = new StringBuilder();
        for (int i = 0; i < entry.requirements.Count; i++)
        {
            var req = entry.requirements[i];
            if (req == null) continue;
            if (GetLevel(req.dependsOn) >= req.requiredLevel) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(GetDepartmentKoreanName(req.dependsOn));
            sb.Append(' ');
            sb.Append(req.requiredLevel);
            sb.Append("단계");
        }
        if (sb.Length == 0) return string.Empty;
        sb.Append(" 필요");
        return sb.ToString();
    }

    private PrerequisiteEntry FindPrerequisiteEntry(HQDepartment d)
    {
        if (prerequisites == null) return null;
        for (int i = 0; i < prerequisites.Count; i++)
        {
            var e = prerequisites[i];
            if (e != null && e.department == d) return e;
        }
        return null;
    }

    public static string GetDepartmentKoreanName(HQDepartment d)
    {
        switch (d)
        {
            case HQDepartment.Headquarters: return "본부";
            case HQDepartment.Publicity:    return "홍보부";
            case HQDepartment.Workshop:     return "공방";
            case HQDepartment.Research:     return "연구소";
            case HQDepartment.Training:     return "훈련실";
            case HQDepartment.Infirmary:    return "의무실";
            case HQDepartment.Exchange:     return "교환소";
            default: return d.ToString();
        }
    }

    public bool CanUpgrade(HQDepartment d)
    {
        if (GetLevel(d) >= GetMaxLevel(d)) return false;
        if (!ArePrerequisitesMet(d)) return false;
        return true;
    }

    public bool TryUpgrade(HQDepartment d, out int beforeLevel, out int afterLevel)
    {
        beforeLevel = GetLevel(d);
        afterLevel = beforeLevel;
        if (!CanUpgrade(d)) return false;
        afterLevel = beforeLevel + 1;
        levels[d] = afterLevel;
        OnStateChanged?.Invoke();
        return true;
    }

    /// <summary>[JC 260617] 디버그 치트: 본부를 제외한 모든 시설(부서)을 최소 1레벨로 즉시 해금.
    /// 비용·선행조건을 모두 무시한다. 신규로 해금된 부서 수를 반환.</summary>
    public int DebugUnlockAllFacilities()
    {
        int changed = 0;
        foreach (HQDepartment d in Enum.GetValues(typeof(HQDepartment)))
        {
            if (d == HQDepartment.Headquarters) continue;
            if (GetLevel(d) < 1) { levels[d] = 1; changed++; }
        }
        if (changed > 0) OnStateChanged?.Invoke();
        return changed;
    }
}
