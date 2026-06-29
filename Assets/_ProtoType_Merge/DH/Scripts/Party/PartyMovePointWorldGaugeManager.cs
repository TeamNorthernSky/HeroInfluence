using System.Collections.Generic;
using UnityEngine;

public class PartyMovePointWorldGaugeManager : MonoBehaviour
{
    public static PartyMovePointWorldGaugeManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PartyMovePointWorldBillboardGauge gaugePrefab;
    [SerializeField] private Transform gaugeRoot;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private Camera targetCamera;

    private readonly Dictionary<PartyGridMover, PartyMovePointWorldBillboardGauge> gauges =
        new Dictionary<PartyGridMover, PartyMovePointWorldBillboardGauge>();

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

        RegisterParty(ResolveExistingParty());
    }

    public void RegisterParty(PartyGridMover party)
    {
        if (party == null)
            return;

        ResolveReferences();
        if (gaugePrefab == null)
            return;

        if (gauges.TryGetValue(party, out PartyMovePointWorldBillboardGauge existingGauge))
        {
            if (existingGauge != null)
                existingGauge.Bind(party, targetCamera);
            return;
        }

        PartyMovePointWorldBillboardGauge gauge = Instantiate(gaugePrefab, gaugeRoot);
        gauge.Bind(party, targetCamera);
        gauges[party] = gauge;
    }

    public void UnregisterParty(PartyGridMover party)
    {
        if (party == null)
            return;

        if (!gauges.TryGetValue(party, out PartyMovePointWorldBillboardGauge gauge))
            return;

        gauges.Remove(party);
        if (gauge != null)
            Destroy(gauge.gameObject);
    }

    private void RemoveMissingParties()
    {
        List<PartyGridMover> missingParties = null;
        foreach (KeyValuePair<PartyGridMover, PartyMovePointWorldBillboardGauge> pair in gauges)
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

    private PartyGridMover ResolveExistingParty()
    {
        return partyRegistry != null ? partyRegistry.PlayerParty : null;
    }

    private void ResolveReferences()
    {
        if (gaugeRoot == null)
            gaugeRoot = transform;

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }
}
