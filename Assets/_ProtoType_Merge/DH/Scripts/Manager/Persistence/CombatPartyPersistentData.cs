using System;
using System.Collections.Generic;

[Serializable]
public class CombatPartyPersistentData
{
    public string PartyId => partyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;

    [UnityEngine.SerializeField] private string partyId;
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();

    public CombatPartyPersistentData(string partyId, IReadOnlyList<int> unitIndices)
    {
        this.partyId = partyId ?? string.Empty;
        SetUnitIndices(unitIndices);
    }

    public void SetPartyId(string nextPartyId)
    {
        partyId = nextPartyId ?? string.Empty;
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        // [JC 수정 260511] Unity 직렬화 사이클로 필드가 null로 복원될 수 있어 가드
        if (unitIndices == null) unitIndices = new List<int>();
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] > 0)
                unitIndices.Add(source[i]);
        }
    }
}
