using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TutorialCombatResultProcessor : MonoBehaviour
{
    private const string RuntimeObjectName = "[DH_TutorialCombatResultProcessor]";

    private static TutorialCombatResultProcessor instance;

    private Coroutine pendingProcess;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(root);
        root.AddComponent<TutorialCombatResultProcessor>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        QueueProcess();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (pendingProcess == null)
            return;

        StopCoroutine(pendingProcess);
        pendingProcess = null;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueProcess();
    }

    private void QueueProcess()
    {
        if (pendingProcess != null)
            StopCoroutine(pendingProcess);

        pendingProcess = StartCoroutine(ProcessAfterSceneReady());
    }

    private IEnumerator ProcessAfterSceneReady()
    {
        yield return null;
        yield return null;
        pendingProcess = null;

        CombatContext context = CombatContext.Instance;
        if (context == null || !context.IsTutorial || context.Result == CombatResult.None)
            yield break;

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();

        repository.SetLastCombatResult(context.Result);
        repository.ApplyPendingCombatResult(context.Result);
        ApplyInactiveObjectStates(repository);
        ApplyOutpostStates(repository);
        context.ClearTutorial();
    }

    private static void ApplyInactiveObjectStates(TutorialProgressRepository repository)
    {
        if (repository == null)
            return;

        TutorialEnemyObject[] enemies = FindObjectsByType<TutorialEnemyObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            TutorialEnemyObject enemy = enemies[i];
            if (enemy == null || !repository.IsObjectInactive(enemy.ObjectKey))
                continue;

            enemy.gameObject.SetActive(false);
        }
    }

    private static void ApplyOutpostStates(TutorialProgressRepository repository)
    {
        if (repository == null)
            return;

        TutorialOutpostObject[] outposts = FindObjectsByType<TutorialOutpostObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < outposts.Length; i++)
        {
            TutorialOutpostObject outpost = outposts[i];
            if (outpost == null)
                continue;

            outpost.RefreshStateVisuals();
        }
    }

    public static bool TryPersistAllies(
        IReadOnlyList<BattleCharactor> playerUnits,
        out string error)
    {
        error = string.Empty;

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        if (repository == null)
        {
            error = "The DontDestroyOnLoad tutorial repository is missing.";
            return false;
        }

        if (playerUnits == null || playerUnits.Count == 0)
        {
            error = "No tutorial allies were supplied.";
            return false;
        }

        int savedCount = 0;
        for (int i = 0; i < playerUnits.Count; i++)
        {
            BattleCharactor player = playerUnits[i];
            UnitPersistentData source = player != null ? player.SourceData : null;
            if (player == null || source == null || string.IsNullOrWhiteSpace(source.UnitTemplateKey))
            {
                error = $"Tutorial ally at index {i} has no persistent source identity.";
                return false;
            }

            string unitTemplateKey = source.UnitTemplateKey.Trim();
            int maxHp = Mathf.Max(1, Mathf.CeilToInt(player.MaxHp));
            int currentHp = player.IsDead
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(player.CurrentHp), 1, maxHp);
            int maxIp = Mathf.Max(0, Mathf.RoundToInt(player.MaxInfluence));
            int currentIp = Mathf.Clamp(Mathf.RoundToInt(player.CurrentInfluence), 0, maxIp);
            int atk = Mathf.Max(0, Mathf.RoundToInt(source.IngameStats.Atk));
            int level = Mathf.Max(1, source.Level);
            int maxExp = Mathf.Max(0, source.MaxExp);
            int exp = maxExp > 0
                ? Mathf.Clamp(source.Exp, 0, maxExp)
                : Mathf.Max(0, source.Exp);

            repository.SetUnitJoined(unitTemplateKey, true);
            repository.SetUnitStats(unitTemplateKey, currentHp, maxHp, currentIp, maxIp, atk, level, exp, maxExp);
            savedCount++;
        }

        if (savedCount == 0)
        {
            error = "No tutorial ally state was saved.";
            return false;
        }

        return true;
    }
}
