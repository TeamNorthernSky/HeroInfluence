using System;
using UnityEngine;

public enum PartyPlacementSource
{
    Scene,
    Runtime
}

[DisallowMultipleComponent]
public class PartyIdentity : MonoBehaviour
{
    [SerializeField] private string partyId = "party_001";
    [SerializeField] private string placementKey;
    [SerializeField] private PartyPlacementSource placementSource = PartyPlacementSource.Scene;

    public string PartyId => partyId;
    public string PlacementKey => placementKey;
    public PartyPlacementSource PlacementSource => placementSource;

    public void SetPartyIdIfEmpty(string nextPartyId)
    {
        if (string.IsNullOrWhiteSpace(nextPartyId))
            return;

        if (!string.IsNullOrWhiteSpace(partyId) &&
            !string.Equals(partyId, "party_001", StringComparison.Ordinal))
            return;

        partyId = nextPartyId;
    }

    public void SetPlacementKey(string nextPlacementKey)
    {
        if (string.IsNullOrWhiteSpace(nextPlacementKey))
            return;

        placementKey = nextPlacementKey;
    }

    public void SetPlacementSource(PartyPlacementSource nextPlacementSource)
    {
        placementSource = nextPlacementSource;
    }
}
