using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HQStateManager : MonoBehaviour
{
    public event Action OnStateChanged;

    [Header("단계 상한 (본부 기준 3. 추후 CSV 가능)")]
    [SerializeField] private int maxLevel = 3;

    [Header("초기 단계 — 본부만 1, 나머지 0(미해금)")]
    [SerializeField] private int hqInitialLevel = 1;

    [Header("업그레이드 비용 폴백 (List 엔트리 없는 부서·단계에 적용)")]
    [SerializeField] private int defaultCostMoney = 1000;
    [SerializeField] private int defaultCostChip = 1000;
    [SerializeField] private int defaultCostCrystal = 1000;
    [SerializeField] private int defaultCostSupply = 1000;

    [Header("부서·단계별 업그레이드 비용 (추후 CSV 로드 갈아끼움)")]
    [SerializeField] private List<UpgradeCostEntry> upgradeCosts = new List<UpgradeCostEntry>();

    [Header("본부 매턴 자금 (단계별, 1단계부터). 추후 CSV 가능")]
    [SerializeField] private int[] hqTurnIncome = new[] { 1000, 1500, 3000 };

    [Serializable]
    public class UpgradeCostEntry
    {
        public HQDepartment department;
        [Tooltip("이 단계에서 다음 단계로 강화할 때의 비용. 1이면 1→2 강화")]
        public int fromLevel = 1;
        public int money;
        public int chip;
        public int crystal;
        public int supply;
    }

    private readonly Dictionary<HQDepartment, int> levels = new Dictionary<HQDepartment, int>();
    private bool upgradedThisTurn;

    public int MaxLevel => maxLevel;
    public bool UpgradedThisTurn => upgradedThisTurn;

    public void Initialize()
    {
        levels.Clear();
        foreach (HQDepartment d in Enum.GetValues(typeof(HQDepartment)))
        {
            levels[d] = (d == HQDepartment.Headquarters) ? hqInitialLevel : 0;
        }
        upgradedThisTurn = false;
    }

    public int GetLevel(HQDepartment d) => levels.TryGetValue(d, out int v) ? v : 0;

    /// <summary>
    /// 매턴 시작 시 자동 적립되는 income. 현 단계: 본부만 자금 income.
    /// level=0(미해금)일 땐 0 반환. 단계 인덱스 [1..maxLevel].
    /// 추후 CSV 로드 시 본 메서드만 갈아끼우면 됨.
    /// </summary>
    public int GetTurnIncome(HQDepartment d)
    {
        return GetTurnIncomeAt(d, GetLevel(d));
    }

    public int GetTurnIncomeAt(HQDepartment d, int level)
    {
        if (d != HQDepartment.Headquarters) return 0;
        if (level <= 0 || hqTurnIncome == null) return 0;
        int idx = Mathf.Clamp(level - 1, 0, hqTurnIncome.Length - 1);
        return hqTurnIncome[idx];
    }

    /// <summary>
    /// 업그레이드 비용. upgradeCosts List를 (부서, fromLevel=currentLevel)로 검색.
    /// 엔트리 없으면 폴백(defaultCost*) 적용. 추후 CSV 로더가 List를 채우는 형태로 확장 가능.
    /// </summary>
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

    public bool CanUpgrade(HQDepartment d)
    {
        if (upgradedThisTurn) return false;
        if (GetLevel(d) >= maxLevel) return false;
        return true;
    }

    /// <summary>
    /// 비용 차감은 호출자(HQUpgradeFlowController) 책임. 본 메서드는 단계 증가 + 턴 플래그만 처리.
    /// </summary>
    public bool TryUpgrade(HQDepartment d, out int beforeLevel, out int afterLevel)
    {
        beforeLevel = GetLevel(d);
        afterLevel = beforeLevel;
        if (!CanUpgrade(d)) return false;
        afterLevel = beforeLevel + 1;
        levels[d] = afterLevel;
        upgradedThisTurn = true;
        OnStateChanged?.Invoke();
        return true;
    }

    public void OnTurnAdvanced()
    {
        if (!upgradedThisTurn) return;
        upgradedThisTurn = false;
        OnStateChanged?.Invoke();
    }
}
