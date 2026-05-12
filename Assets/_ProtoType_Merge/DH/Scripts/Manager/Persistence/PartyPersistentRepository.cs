using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PartyPersistentRepository : MonoBehaviour
{
    public static PartyPersistentRepository Instance { get; private set; }

    [Header("Persistent Parties")]
    [SerializeField] private List<PartyPersistentData> parties = new List<PartyPersistentData>();

    private readonly Dictionary<string, PartyPersistentData> partyLookup = new Dictionary<string, PartyPersistentData>();

    public IReadOnlyList<PartyPersistentData> Parties => parties;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public void RegisterOrUpdateParty(string partyId, IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(partyId))
            return;

        if (partyLookup.TryGetValue(partyId, out PartyPersistentData existingData))
        {
            existingData.SetUnitIndices(unitIndices);
            return;
        }

        PartyPersistentData newData = new PartyPersistentData(partyId, unitIndices);
        parties.Add(newData);
        partyLookup[partyId] = newData;
    }

    public bool ContainsParty(string partyId)
    {
        return !string.IsNullOrWhiteSpace(partyId) && partyLookup.ContainsKey(partyId);
    }

    public bool TryGetParty(string partyId, out PartyPersistentData data)
    {
        if (string.IsNullOrWhiteSpace(partyId))
        {
            data = null;
            return false;
        }

        return partyLookup.TryGetValue(partyId, out data);
    }

    public bool RemoveParty(string partyId)
    {
        if (string.IsNullOrWhiteSpace(partyId) || !partyLookup.TryGetValue(partyId, out PartyPersistentData data))
            return false;

        partyLookup.Remove(partyId);
        parties.Remove(data);
        return true;
    }

    public void ClearAllParties()
    {
        parties.Clear();
        partyLookup.Clear();
    }

    private void RebuildLookup()
    {
        partyLookup.Clear();

        for (int i = 0; i < parties.Count; i++)
        {
            PartyPersistentData data = parties[i];
            if (data == null || string.IsNullOrWhiteSpace(data.PartyId))
                continue;

            if (partyLookup.ContainsKey(data.PartyId))
            {
                Debug.LogWarning($"PartyPersistentRepository has duplicate partyId '{data.PartyId}'.", this);
                continue;
            }

            partyLookup.Add(data.PartyId, data);
        }
    }
}
