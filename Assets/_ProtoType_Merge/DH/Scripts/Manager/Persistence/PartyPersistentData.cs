using System;
using System.Collections.Generic;

[Serializable]
public class PartyPersistentData
{
    public string PartyId => partyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;

    [UnityEngine.SerializeField] private string partyId;
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();

    public PartyPersistentData(string partyId, IReadOnlyList<int> unitIndices)
    {
        this.partyId = partyId ?? string.Empty;
        SetUnitIndices(unitIndices);
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            unitIndices.Add(source[i]);
    }
}
