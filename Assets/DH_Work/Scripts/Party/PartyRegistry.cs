using System;
using UnityEngine;

public class PartyRegistry : MonoBehaviour
{
    [SerializeField] private PartyGridMover[] partyMovers;

    public PartyGridMover[] PartyMovers => partyMovers ?? Array.Empty<PartyGridMover>();

    public bool TryGetPartyById(string partyId, out PartyGridMover partyMover)
    {
        partyMover = null;

        if (string.IsNullOrWhiteSpace(partyId))
            return false;

        PartyGridMover[] movers = PartyMovers;
        for (int i = 0; i < movers.Length; i++)
        {
            PartyGridMover candidate = movers[i];
            if (candidate == null)
                continue;

            PartyIdentity identity = candidate.GetComponent<PartyIdentity>();
            if (identity == null)
                continue;

            if (!string.Equals(identity.PartyId, partyId, StringComparison.Ordinal))
                continue;

            partyMover = candidate;
            return true;
        }

        return false;
    }

    private void OnValidate()
    {
        PartyGridMover[] movers = PartyMovers;
        for (int i = 0; i < movers.Length; i++)
        {
            PartyGridMover current = movers[i];
            if (current == null)
                continue;

            PartyIdentity currentIdentity = current.GetComponent<PartyIdentity>();
            if (currentIdentity == null || string.IsNullOrWhiteSpace(currentIdentity.PartyId))
                continue;

            for (int j = i + 1; j < movers.Length; j++)
            {
                PartyGridMover other = movers[j];
                if (other == null)
                    continue;

                PartyIdentity otherIdentity = other.GetComponent<PartyIdentity>();
                if (otherIdentity == null)
                    continue;

                if (!string.Equals(currentIdentity.PartyId, otherIdentity.PartyId, StringComparison.Ordinal))
                    continue;

                Debug.LogWarning(
                    $"PartyRegistry has duplicate partyId '{currentIdentity.PartyId}'. " +
                    $"Only the first matching party will be used for level spawn mapping.",
                    this);
            }
        }
    }
}
