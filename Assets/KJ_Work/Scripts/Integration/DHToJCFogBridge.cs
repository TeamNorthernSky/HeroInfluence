using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DHToJCFogBridge : MonoBehaviour
{
    [SerializeField] private FogSceneReferences references;
    [SerializeField] private KJ_DHCompatibleFogManager fogManager;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PartyGridMover[] partyMovers;
    [SerializeField] private int sightRadiusCells = 3;
    [SerializeField] private bool revealOnStart = true;
    [SerializeField] private bool refreshOnPathUpdated = true;
    [SerializeField] private bool followFocusedParty = true;
    [SerializeField] private bool logResolutionState = true;

    private PartyGridMover activeMover;
    private Vector2Int lastFogGrid = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int[] lastPartyGrids;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveActiveMover();

        if (logResolutionState)
            LogResolutionState("Start");

        if (revealOnStart)
            RefreshFog();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        ResolveActiveMover();
        RefreshFogIfNeeded();
    }

    [ContextMenu("Resolve References")]
    public void ResolveReferences()
    {
        if (references == null)
            references = GetComponent<FogSceneReferences>();

        if (references == null)
            references = FindFirstObjectByType<FogSceneReferences>();

        references?.CollectReferences();

        if (fogManager == null && references != null)
            fogManager = references.FogManager;

        if (turnManager == null && references != null)
            turnManager = references.TurnManager;

        if ((partyMovers == null || partyMovers.Length == 0) && references != null)
            partyMovers = references.PartyMovers;

        if (fogManager == null)
            fogManager = FindFirstObjectByType<KJ_DHCompatibleFogManager>(FindObjectsInactive.Include);

        if (partyMovers == null || partyMovers.Length == 0)
            partyMovers = FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);

        if (partyMovers != null && (lastPartyGrids == null || lastPartyGrids.Length != partyMovers.Length))
        {
            lastPartyGrids = new Vector2Int[partyMovers.Length];
            for (int i = 0; i < lastPartyGrids.Length; i++)
                lastPartyGrids[i] = new Vector2Int(int.MinValue, int.MinValue);
        }
    }

    [ContextMenu("Refresh Fog From Party Positions")]
    public void RefreshAllPartyVision()
    {
        ResolveReferences();

        if (fogManager == null || partyMovers == null)
            return;

        fogManager.ClearCurrentVisibility();

        for (int i = 0; i < partyMovers.Length; i++)
        {
            var mover = partyMovers[i];
            if (mover == null)
                continue;

            Vector2Int grid = mover.GetCurrentGrid();
            fogManager.UpdatePlayerVisibility(grid, sightRadiusCells);

            if (lastPartyGrids != null && i < lastPartyGrids.Length)
                lastPartyGrids[i] = grid;
        }
    }

    [ContextMenu("Refresh Focused Party Fog")]
    public void RefreshFog()
    {
        ResolveReferences();

        ResolveActiveMover();

        if (fogManager == null || partyMovers == null || partyMovers.Length == 0)
        {
            if (logResolutionState)
                LogResolutionState("RefreshFog-MissingReference");
            return;
        }

        RefreshAllPartyVision();

        if (activeMover != null)
            lastFogGrid = activeMover.GetCurrentGrid();

        if (logResolutionState)
            Debug.Log($"[DHToJCFogBridge] RefreshFog activeMover={(activeMover != null ? activeMover.name : "null")} parties={partyMovers.Length} radius={sightRadiusCells}", this);
    }

    private void Subscribe()
    {
        ResolveReferences();

        if (partyMovers == null)
            return;

        for (int i = 0; i < partyMovers.Length; i++)
        {
            var mover = partyMovers[i];
            if (mover != null)
                mover.PathUpdated += OnPathUpdated;
        }

        if (turnManager != null)
            turnManager.DayAdvanced += OnDayAdvanced;
    }

    private void Unsubscribe()
    {
        if (partyMovers == null)
            return;

        for (int i = 0; i < partyMovers.Length; i++)
        {
            var mover = partyMovers[i];
            if (mover != null)
                mover.PathUpdated -= OnPathUpdated;
        }

        if (turnManager != null)
            turnManager.DayAdvanced -= OnDayAdvanced;
    }

    private void OnPathUpdated(List<Vector2Int> path)
    {
        if (!refreshOnPathUpdated)
            return;

        RefreshFog();
    }

    private void OnDayAdvanced(int day)
    {
        RefreshFog();
    }

    private void RefreshFogIfNeeded()
    {
        if (partyMovers == null || partyMovers.Length == 0)
            return;

        for (int i = 0; i < partyMovers.Length; i++)
        {
            var mover = partyMovers[i];
            if (mover == null)
                continue;

            var currentGrid = mover.GetCurrentGrid();
            if (lastPartyGrids == null || i >= lastPartyGrids.Length || currentGrid != lastPartyGrids[i])
            {
                RefreshFog();
                return;
            }
        }
    }

    private PartyGridMover ResolveActiveMover()
    {
        ResolveReferences();

        if (partyMovers == null || partyMovers.Length == 0)
        {
            activeMover = null;
            return null;
        }

        if (!followFocusedParty)
        {
            activeMover = partyMovers[0];
            return activeMover;
        }

        var mainCamera = references != null ? references.MainCamera : Camera.main;
        if (mainCamera == null)
        {
            activeMover = partyMovers[0];
            return activeMover;
        }

        float bestDistance = float.PositiveInfinity;
        PartyGridMover bestMover = null;

        for (int i = 0; i < partyMovers.Length; i++)
        {
            var mover = partyMovers[i];
            if (mover == null)
                continue;

            Vector3 delta = mover.transform.position - mainCamera.transform.position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                bestMover = mover;
            }
        }

        activeMover = bestMover;
        return activeMover;
    }

    private void LogResolutionState(string context)
    {
        string fogName = fogManager == null ? "null" : fogManager.name;
        string moverName = activeMover == null ? "null" : activeMover.name;
        int moverCount = partyMovers == null ? 0 : partyMovers.Length;
        Debug.Log($"[DHToJCFogBridge] {context} fogManager={fogName} activeMover={moverName} partyMovers={moverCount}", this);
    }
}
