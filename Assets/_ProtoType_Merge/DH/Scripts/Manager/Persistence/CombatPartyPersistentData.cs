using System;
using System.Collections.Generic;

[Serializable]
public class CombatPartyPersistentData
{
    public string PartyId => partyId;
    public IReadOnlyList<int> UnitIndices => unitIndices ?? (unitIndices = new List<int>());

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
        EnsureInitialized();
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] > 0)
                unitIndices.Add(source[i]);
        }
    }

    private void EnsureInitialized()
    {
        if (unitIndices == null)
            unitIndices = new List<int>();
    }
}
