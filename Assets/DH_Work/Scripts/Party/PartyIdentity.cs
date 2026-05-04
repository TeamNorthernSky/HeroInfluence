using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PartyIdentity : MonoBehaviour
{
    [SerializeField] private string partyId = "party_001";

    public string PartyId => partyId;

    public void SetPartyIdIfEmpty(string nextPartyId)
    {
        if (string.IsNullOrWhiteSpace(nextPartyId))
            return;

        if (!string.IsNullOrWhiteSpace(partyId) &&
            !string.Equals(partyId, "party_001", StringComparison.Ordinal))
            return;

        partyId = nextPartyId;
    }
}
