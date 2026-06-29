using System;
using UnityEngine;

public class PartyRegistry : MonoBehaviour
{
    [SerializeField] private PartyGridMover playerParty;

    private PartyGridMover[] cachedPartyMovers = Array.Empty<PartyGridMover>();

    public PartyGridMover PlayerParty => playerParty;

    public PartyGridMover[] PartyMovers
    {
        get
        {
            if (playerParty == null)
                return Array.Empty<PartyGridMover>();

            if (cachedPartyMovers.Length != 1 || cachedPartyMovers[0] != playerParty)
                cachedPartyMovers = new[] { playerParty };

            return cachedPartyMovers;
        }
    }

    public bool TryGetPartyById(string partyId, out PartyGridMover partyMover)
    {
        partyMover = null;

        if (string.IsNullOrWhiteSpace(partyId) || playerParty == null)
            return false;

        PartyIdentity identity = playerParty.GetComponent<PartyIdentity>();
        if (identity == null || !string.Equals(identity.PartyId, partyId, StringComparison.Ordinal))
            return false;

        partyMover = playerParty;
        return true;
    }

    private void OnValidate()
    {
        cachedPartyMovers = Array.Empty<PartyGridMover>();
    }
}
