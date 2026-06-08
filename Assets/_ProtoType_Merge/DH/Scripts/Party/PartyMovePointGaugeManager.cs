using System.Collections.Generic;
using UnityEngine;

public class PartyMovePointGaugeManager : MonoBehaviour
{
    public static PartyMovePointGaugeManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private PartyMovePointScreenGauge gaugePrefab;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private Camera targetCamera;

    private readonly Dictionary<PartyGridMover, PartyMovePointScreenGauge> gauges =
        new Dictionary<PartyGridMover, PartyMovePointScreenGauge>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void Start()
    {
        RefreshExistingParties();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("Refresh Existing Parties")]
    public void RefreshExistingParties()
    {
        ResolveReferences();
        RemoveMissingParties();

        PartyGridMover[] parties = ResolveExistingParties();
        for (int i = 0; i < parties.Length; i++)
            RegisterParty(parties[i]);
    }

    public void RegisterParty(PartyGridMover party)
    {
        if (party == null)
            return;

        ResolveReferences();
        if (hudCanvas == null || gaugePrefab == null)
            return;

        if (gauges.TryGetValue(party, out PartyMovePointScreenGauge existingGauge))
        {
            if (existingGauge != null)
                existingGauge.Bind(party, targetCamera);
            return;
        }

        PartyMovePointScreenGauge gauge = Instantiate(gaugePrefab, hudCanvas.transform);
        gauge.Bind(party, targetCamera);
        gauges[party] = gauge;
    }

    public void UnregisterParty(PartyGridMover party)
    {
        if (party == null)
            return;

        if (!gauges.TryGetValue(party, out PartyMovePointScreenGauge gauge))
            return;

        gauges.Remove(party);
        if (gauge != null)
            Destroy(gauge.gameObject);
    }

    private void RemoveMissingParties()
    {
        List<PartyGridMover> missingParties = null;
        foreach (KeyValuePair<PartyGridMover, PartyMovePointScreenGauge> pair in gauges)
        {
            if (pair.Key != null && pair.Value != null)
                continue;

            if (missingParties == null)
                missingParties = new List<PartyGridMover>();

            missingParties.Add(pair.Key);
        }

        if (missingParties == null)
            return;

        for (int i = 0; i < missingParties.Count; i++)
            UnregisterParty(missingParties[i]);
    }

    private PartyGridMover[] ResolveExistingParties()
    {
        if (partyRegistry != null && partyRegistry.PartyMovers.Length > 0)
            return partyRegistry.PartyMovers;

        return FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);
    }

    private void ResolveReferences()
    {
        if (hudCanvas == null)
            hudCanvas = GetComponentInParent<Canvas>();

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }
}
