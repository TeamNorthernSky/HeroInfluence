using System;
using System.Collections.Generic;

[Serializable]
public class PartyPersistentData
{
    public string PartyId => partyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;
    public IReadOnlyList<int> UnitSlots => unitSlots;

    [UnityEngine.SerializeField] private string partyId;
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();
    [UnityEngine.SerializeField] private List<int> unitSlots = new List<int>();

    // [JC 추가 260511] 위치 영속화. DHScene Single 재로드 시 transform.position이 씬 시작값으로 복귀하는 문제 해결용
    [UnityEngine.SerializeField] private UnityEngine.Vector2Int lastGrid;
    [UnityEngine.SerializeField] private bool hasLastGrid;
    [UnityEngine.SerializeField] private int remainingMovePoints;
    [UnityEngine.SerializeField] private bool hasRemainingMovePoints;
    public UnityEngine.Vector2Int LastGrid => lastGrid;
    public bool HasLastGrid => hasLastGrid;
    public int RemainingMovePoints => remainingMovePoints;
    public bool HasRemainingMovePoints => hasRemainingMovePoints;

    public void SetLastGrid(UnityEngine.Vector2Int grid)
    {
        lastGrid = grid;
        hasLastGrid = true;
    }

    public void ClearLastGrid()
    {
        lastGrid = default;
        hasLastGrid = false;
    }

    public void SetRemainingMovePoints(int value)
    {
        remainingMovePoints = Math.Max(0, value);
        hasRemainingMovePoints = true;
    }

    public void ClearRemainingMovePoints()
    {
        remainingMovePoints = 0;
        hasRemainingMovePoints = false;
    }

    public PartyPersistentData(string partyId, IReadOnlyList<int> unitIndices)
        : this(partyId, unitIndices, null)
    {
    }

    public PartyPersistentData(string partyId, IReadOnlyList<int> unitIndices, IReadOnlyList<int> unitSlots)
    {
        this.partyId = partyId ?? string.Empty;
        SetUnitIndices(unitIndices);
        SetUnitSlots(unitSlots);
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        // [JC 수정 260511] Unity 직렬화 사이클로 필드가 null로 복원될 수 있어 가드
        if (unitIndices == null) unitIndices = new List<int>();
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            unitIndices.Add(source[i]);
    }

    public void SetUnitSlots(IReadOnlyList<int> source)
    {
        if (unitSlots == null) unitSlots = new List<int>();
        unitSlots.Clear();

        if (unitIndices == null)
            unitIndices = new List<int>();

        if (source != null)
        {
            for (int i = 0; i < source.Count && i < unitIndices.Count; i++)
                unitSlots.Add(Math.Max(1, source[i]));
        }

        for (int i = unitSlots.Count; i < unitIndices.Count; i++)
            unitSlots.Add(i + 1);
    }
}
