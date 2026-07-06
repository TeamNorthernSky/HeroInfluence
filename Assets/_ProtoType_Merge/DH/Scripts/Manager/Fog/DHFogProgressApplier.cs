using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DHFogProgressApplier : MonoBehaviour
{
    public static DHFogProgressApplier Instance { get; private set; }

    private readonly List<FogGridManager.FogCellSnapshot> snapshotBuffer = new List<FogGridManager.FogCellSnapshot>();
    private FogGridManager currentFogGridManager;
    private Coroutine initializeCoroutine;
    private bool isRestoring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        var go = new GameObject("[DHFogProgressApplier]");
        go.AddComponent<DHFogProgressApplier>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        QueueInitialize();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

        if (initializeCoroutine != null)
        {
            StopCoroutine(initializeCoroutine);
            initializeCoroutine = null;
        }

        UnsubscribeCurrentFogGrid();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueInitialize();
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        QueueInitialize();
    }

    private void QueueInitialize()
    {
        if (!isActiveAndEnabled)
            return;

        if (initializeCoroutine != null)
            StopCoroutine(initializeCoroutine);

        initializeCoroutine = StartCoroutine(InitializeNextFrame());
    }

    private IEnumerator InitializeNextFrame()
    {
        yield return null;
        initializeCoroutine = null;
        InitializeLoadedFogScene();
    }

    private void InitializeLoadedFogScene()
    {
        FogGridManager fogGridManager = FindFirstObjectByType<FogGridManager>();
        if (fogGridManager == null)
        {
            UnsubscribeCurrentFogGrid();
            return;
        }

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        UnsubscribeCurrentFogGrid();
        currentFogGridManager = fogGridManager;

        SyncCurrentDay(fogGridManager);

        if (repository.HasFogProgress())
        {
            BuildSnapshotBuffer(repository.FogCells);
            isRestoring = true;
            fogGridManager.ApplySnapshot(snapshotBuffer);
            isRestoring = false;
        }

        currentFogGridManager.FogChanged -= HandleFogChanged;
        currentFogGridManager.FogChanged += HandleFogChanged;

        RevealCurrentContext();
        SaveFogProgress();
    }

    private void HandleFogChanged()
    {
        if (isRestoring)
            return;

        SaveFogProgress();
    }

    private void SaveFogProgress()
    {
        if (currentFogGridManager == null)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        repository.ReplaceFogCells(currentFogGridManager.EnumerateKnownCells());
    }

    private void BuildSnapshotBuffer(IReadOnlyList<FogProgressCell> progressCells)
    {
        snapshotBuffer.Clear();

        if (progressCells == null)
            return;

        for (int i = 0; i < progressCells.Count; i++)
        {
            FogProgressCell cell = progressCells[i];
            if (cell == null || cell.Visibility == FogVisibilityState.Unexplored)
                continue;

            snapshotBuffer.Add(new FogGridManager.FogCellSnapshot(
                cell.Grid,
                cell.Visibility,
                cell.LastRevealedDay));
        }
    }

    private static void SyncCurrentDay(FogGridManager fogGridManager)
    {
        if (fogGridManager == null || GameManager.Instance == null)
            return;

        fogGridManager.SetCurrentDay(GameManager.Instance.CurrentDay);
    }

    private static void RevealCurrentContext()
    {
        PartyFogRevealer partyFogRevealer = FindFirstObjectByType<PartyFogRevealer>();
        partyFogRevealer?.RevealAllCurrentPartyPositions();

        OutpostFogRevealer outpostFogRevealer = FindFirstObjectByType<OutpostFogRevealer>();
        outpostFogRevealer?.RevealAllClaimedOutposts();

        HeroUnionFogRevealer heroUnionFogRevealer = FindFirstObjectByType<HeroUnionFogRevealer>();
        heroUnionFogRevealer?.RevealAllHeroUnions();
    }

    private void UnsubscribeCurrentFogGrid()
    {
        if (currentFogGridManager != null)
            currentFogGridManager.FogChanged -= HandleFogChanged;

        currentFogGridManager = null;
    }
}
