using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TutorialFogProgressApplier : MonoBehaviour
{
    public static TutorialFogProgressApplier Instance { get; private set; }

    private readonly List<FogGridManager.FogCellSnapshot> snapshotBuffer = new List<FogGridManager.FogCellSnapshot>();
    private FogGridManager currentFogGridManager;
    private Coroutine initializeCoroutine;
    private bool isRestoring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject root = new GameObject("[TutorialFogProgressApplier]");
        root.AddComponent<TutorialFogProgressApplier>();
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
        InitializeLoadedTutorialFogScene();
    }

    private void InitializeLoadedTutorialFogScene()
    {
        if (!IsTutorialExploreScene(SceneManager.GetActiveScene()))
        {
            UnsubscribeCurrentFogGrid();
            return;
        }

        FogGridManager fogGridManager = FindFirstObjectByType<FogGridManager>();
        if (fogGridManager == null)
        {
            UnsubscribeCurrentFogGrid();
            return;
        }

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        if (repository == null)
            return;

        UnsubscribeCurrentFogGrid();
        currentFogGridManager = fogGridManager;
        currentFogGridManager.SetCurrentDay(repository.CurrentTurn);

        if (repository.HasFogProgress)
        {
            BuildSnapshotBuffer(repository.FogCells);
            isRestoring = true;
            fogGridManager.ApplySnapshot(snapshotBuffer);
            isRestoring = false;
        }

        currentFogGridManager.FogChanged -= HandleFogChanged;
        currentFogGridManager.FogChanged += HandleFogChanged;

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

        TutorialProgressRepository repository = TutorialProgressRepository.Instance;
        if (repository == null)
            return;

        repository.ReplaceFogCells(currentFogGridManager.EnumerateKnownCells());
    }

    private void BuildSnapshotBuffer(IReadOnlyList<TutorialFogProgressCell> progressCells)
    {
        snapshotBuffer.Clear();

        if (progressCells == null)
            return;

        for (int i = 0; i < progressCells.Count; i++)
        {
            TutorialFogProgressCell cell = progressCells[i];
            if (cell == null || cell.Visibility == FogVisibilityState.Unexplored)
                continue;

            snapshotBuffer.Add(new FogGridManager.FogCellSnapshot(
                cell.Grid,
                cell.Visibility,
                cell.LastRevealedDay));
        }
    }

    private void UnsubscribeCurrentFogGrid()
    {
        if (currentFogGridManager != null)
            currentFogGridManager.FogChanged -= HandleFogChanged;

        currentFogGridManager = null;
    }

    private static bool IsTutorialExploreScene(Scene scene)
    {
        return scene.IsValid() && scene.name == "TutorialExploreScene";
    }
}
