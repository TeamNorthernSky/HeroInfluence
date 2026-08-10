using System;
using System.Collections.Generic;

public enum CombatEnemySourceType
{
    None,
    // 필드에 배치되었거나 런타임 생성된 일반 적 파티.
    Field,
    // Outpost 점령전 defender. 현재는 일반 전투 경로를 유지하지만 그룹키 이관용으로 구분한다.
    OutpostDefender,
    // VillainUnion 점령전 defender. 현재는 일반 전투 경로를 유지하지만 그룹키 이관용으로 구분한다.
    VillainUnionDefender,
    // EventBattle은 CombatEnemyPersistentData가 아니라 CombatContext.EventBattle을 사용한다.
    Event
}

[Serializable]
public class CombatEnemyPersistentData
{
    public string EnemyId => enemyId;
    // MapProgress/PersistentEnemyRepository에서 필드 적 개체를 다시 찾기 위한 키.
    public string PlacementKey => placementKey;
    // ASB가 일반 전투도 그룹키 기반으로 전환할 때 우선 읽어야 하는 키.
    // 기존 호환을 위해 UnitIndices도 유지하지만, 장기적으로는 이 값이 전투 적 구성을 대표한다.
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    // 같은 EnemyGroupKey라도 필드 적/거점/빌런연합 defender 후처리가 다르므로 출처를 함께 넘긴다.
    public CombatEnemySourceType SourceType => sourceType;
    // 기존 ASB 전투 진입 호환용 적 유닛 인덱스. 그룹키 기반 전환 전까지 유지한다.
    public IReadOnlyList<int> UnitIndices => unitIndices ?? (unitIndices = new List<int>());

    [UnityEngine.SerializeField] private string enemyId;
    [UnityEngine.SerializeField] private string placementKey;
    [UnityEngine.SerializeField] private string enemyGroupKey;
    [UnityEngine.SerializeField] private CombatEnemySourceType sourceType;
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();

    public CombatEnemyPersistentData(string enemyId, IReadOnlyList<int> unitIndices)
        : this(enemyId, string.Empty, string.Empty, CombatEnemySourceType.None, unitIndices)
    {
    }

    public CombatEnemyPersistentData(string enemyId, string placementKey, IReadOnlyList<int> unitIndices)
        : this(enemyId, placementKey, string.Empty, CombatEnemySourceType.None, unitIndices)
    {
    }

    public CombatEnemyPersistentData(
        string enemyId,
        string placementKey,
        string enemyGroupKey,
        CombatEnemySourceType sourceType,
        IReadOnlyList<int> unitIndices)
    {
        this.enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
        this.placementKey = string.IsNullOrWhiteSpace(placementKey) ? string.Empty : placementKey;
        this.enemyGroupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        this.sourceType = sourceType;
        SetUnitIndices(unitIndices);
    }

    public void SetEnemyId(string nextEnemyId)
    {
        enemyId = string.IsNullOrWhiteSpace(nextEnemyId) ? string.Empty : nextEnemyId;
    }

    public void SetPlacementKey(string nextPlacementKey)
    {
        placementKey = string.IsNullOrWhiteSpace(nextPlacementKey) ? string.Empty : nextPlacementKey;
    }

    public void SetEnemyGroupKey(string nextEnemyGroupKey)
    {
        enemyGroupKey = string.IsNullOrWhiteSpace(nextEnemyGroupKey) ? string.Empty : nextEnemyGroupKey.Trim();
    }

    public void SetSourceType(CombatEnemySourceType nextSourceType)
    {
        sourceType = nextSourceType;
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        EnsureInitialized();
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            unitIndices.Add(Math.Max(0, source[i]));
    }

    private void EnsureInitialized()
    {
        if (unitIndices == null)
            unitIndices = new List<int>();
    }
}
