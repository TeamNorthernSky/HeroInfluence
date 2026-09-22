using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TutorialCombatLauncher : MonoBehaviour
{
    private const string DefaultPartyId = "TUTORIAL_PARTY";
    private const string DefaultBattleSceneName = "TutorialBattleScene";
    private const string DefaultReturnSceneName = "TutorialExploreScene";

    [Header("Scene")]
    [SerializeField] private string tutorialBattleSceneName;
    [SerializeField] private string returnSceneName = "TutorialExploreScene";
    [SerializeField] private bool allowBattleSceneLoad = true;

    [Header("Default Party")]
    [SerializeField] private List<string> defaultUnitTemplateKeys = new List<string>
    {
        "10001",
        "10002",
        "10003",
        "10004"
    };

    [Header("Debug")]
    [SerializeField] private string debugEnemyGroupKey;
    [SerializeField, Min(1)] private int debugEnemyLevel = 1;

    public string TutorialBattleSceneName
    {
        get => string.IsNullOrWhiteSpace(tutorialBattleSceneName) ? string.Empty : tutorialBattleSceneName.Trim();
        set => tutorialBattleSceneName = value;
    }

    public string ReturnSceneName
    {
        get => string.IsNullOrWhiteSpace(returnSceneName) ? DefaultReturnSceneName : returnSceneName.Trim();
        set => returnSceneName = value;
    }

    public static TutorialCombatLauncher EnsureSceneLauncher()
    {
        TutorialCombatLauncher launcher = FindFirstObjectByType<TutorialCombatLauncher>();
        if (launcher != null)
            return launcher;

        TutorialCombatLauncher[] launchers = FindObjectsByType<TutorialCombatLauncher>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (launchers != null && launchers.Length > 0)
            return launchers[0];

        GameObject root = new GameObject("[TutorialCombatLauncher]");
        launcher = root.AddComponent<TutorialCombatLauncher>();
        launcher.tutorialBattleSceneName = DefaultBattleSceneName;
        launcher.returnSceneName = DefaultReturnSceneName;
        launcher.allowBattleSceneLoad = true;
        return launcher;
    }

    public bool BeginCombat(string enemyGroupKey, int enemyLevel)
    {
        return BeginCombat(enemyGroupKey, enemyLevel, string.Empty, -1, ReturnSceneName);
    }

    public bool BeginCombat(string enemyGroupKey, int enemyLevel, string tutorialBattleKey, int tutorialZoneId)
    {
        return BeginCombat(enemyGroupKey, enemyLevel, tutorialBattleKey, tutorialZoneId, ReturnSceneName);
    }

    public bool BeginCombat(
        string enemyGroupKey,
        int enemyLevel,
        string tutorialBattleKey,
        int tutorialZoneId,
        string nextReturnSceneName)
    {
        string groupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        if (string.IsNullOrEmpty(groupKey))
        {
            Debug.LogWarning("[TutorialCombatLauncher] EnemyGroupKey is empty.", this);
            return false;
        }

        CombatContext context = CombatContext.EnsureInstance();
        if (context == null)
        {
            Debug.LogWarning("[TutorialCombatLauncher] CombatContext is missing.", this);
            return false;
        }

        if (!BuildDefaultAllies(out List<SimulationAllyRuntimeData> allies, out string buildError))
        {
            Debug.LogWarning($"[TutorialCombatLauncher] Failed to build tutorial allies. {buildError}", this);
            return false;
        }

        if (!TutorialEnemySpawnPlanBuilder.TryBuildFromEnemyGroup(
                groupKey,
                enemyLevel,
                out _,
                out string enemyError))
        {
            Debug.LogWarning($"[TutorialCombatLauncher] Failed to validate tutorial enemy group. {enemyError}", this);
            return false;
        }

        SaveCurrentPartyState();

        if (!context.BeginTutorial(
                DefaultPartyId,
                allies,
                groupKey,
                Mathf.Max(1, enemyLevel),
                string.IsNullOrWhiteSpace(nextReturnSceneName) ? ReturnSceneName : nextReturnSceneName.Trim(),
                tutorialBattleKey,
                tutorialZoneId))
        {
            context.ClearTutorial();
            Debug.LogWarning("[TutorialCombatLauncher] CombatContext.BeginTutorial failed.", this);
            return false;
        }

        string sceneName = TutorialBattleSceneName;
        if (!allowBattleSceneLoad)
        {
            context.ClearTutorial();
            Debug.LogWarning(
                "[TutorialCombatLauncher] Battle scene load is disabled until ASB tutorial battle integration is ready.",
                this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            context.ClearTutorial();
            Debug.LogWarning("[TutorialCombatLauncher] Tutorial battle scene name is empty. Context was cleared.", this);
            return false;
        }

        LoadBattleScene(sceneName);
        return true;
    }

    [ContextMenu("Debug Begin Tutorial Combat")]
    private void DebugBeginTutorialCombat()
    {
        BeginCombat(debugEnemyGroupKey, debugEnemyLevel);
    }

    private bool BuildDefaultAllies(out List<SimulationAllyRuntimeData> allies, out string error)
    {
        allies = new List<SimulationAllyRuntimeData>();
        error = string.Empty;

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        if (repository == null)
        {
            error = "The DontDestroyOnLoad tutorial repository is missing.";
            return false;
        }

        List<string> unitTemplateKeys = repository.GetJoinedUnitTemplateKeys();
        bool useInitialParty = unitTemplateKeys.Count == 0;
        if (useInitialParty)
            unitTemplateKeys = defaultUnitTemplateKeys != null
                ? new List<string>(defaultUnitTemplateKeys)
                : new List<string>();

        if (unitTemplateKeys.Count == 0)
        {
            error = "Tutorial party is empty.";
            return false;
        }

        for (int i = 0; i < unitTemplateKeys.Count; i++)
        {
            string unitKey = unitTemplateKeys[i];
            if (string.IsNullOrWhiteSpace(unitKey))
                continue;

            bool hasStoredState = repository.TryGetUnitState(
                unitKey,
                out TutorialUnitProgressState storedState) &&
                storedState != null &&
                storedState.MaxHp > 0;

            // 행동 불능 유닛은 다음 전투 참가자에서 제외한다. 저장 상태 자체는 유지된다.
            if (hasStoredState && storedState.CurrentHp <= 0)
                continue;

            if (!TutorialCombatUnitFactory.TryCreateRuntimeData(
                    unitKey,
                    allies.Count,
                    out SimulationAllyRuntimeData runtimeData,
                    out _,
                    out string createError))
            {
                error = $"Unit '{unitKey}' failed. {createError}";
                return false;
            }

            UnitPersistentData data = runtimeData.UnitData;
            if (hasStoredState)
            {
                ApplyStoredUnitState(data, storedState);
            }
            else
            {
                repository.SetUnitJoined(data.UnitTemplateKey, true);
                repository.SetUnitStats(
                    data.UnitTemplateKey,
                    Mathf.RoundToInt(data.CurrentHp),
                    Mathf.RoundToInt(data.IngameStats.HP),
                    Mathf.RoundToInt(data.CurrentInfluence),
                    Mathf.RoundToInt(data.IngameStats.Influence),
                    Mathf.RoundToInt(data.IngameStats.Atk),
                    data.Level,
                    data.Exp,
                    data.MaxExp);
            }

            allies.Add(runtimeData);
        }

        if (allies.Count == 0)
        {
            error = "No valid tutorial allies were created.";
            return false;
        }

        return true;
    }

    private static void SaveCurrentPartyState()
    {
        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        if (repository == null)
            return;

        TutorialPartyRuntime party = FindFirstObjectByType<TutorialPartyRuntime>();
        PartyGridMover mover = party != null ? party.GridMover : null;
        if (mover == null)
            return;

        repository.SetPartyGrid(mover.GetCurrentGrid());
        repository.SetRemainingMovePoints(mover.RemainingMovePoints);
    }

    private static void ApplyStoredUnitState(
        UnitPersistentData data,
        TutorialUnitProgressState storedState)
    {
        StatBlock ingameStats = data.IngameStats;
        ingameStats.HP = Mathf.Max(1, storedState.MaxHp);
        ingameStats.Atk = Mathf.Max(0, storedState.Atk);
        ingameStats.Influence = Mathf.Max(0, storedState.MaxIp);

        float currentHp = Mathf.Clamp(storedState.CurrentHp, 0f, ingameStats.HP);
        float currentInfluence = Mathf.Clamp(storedState.CurrentIp, 0f, ingameStats.Influence);
        int level = Mathf.Max(1, storedState.Level);
        int maxExp = storedState.MaxExp > 0 ? storedState.MaxExp : data.MaxExp;
        int exp = maxExp > 0
            ? Mathf.Clamp(storedState.Exp, 0, maxExp)
            : Mathf.Max(0, storedState.Exp);

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            level,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponKey,
            data.CurrentWeaponIndex,
            data.CurrentWeaponStats,
            ingameStats,
            currentHp,
            exp,
            maxExp,
            data.SkillLevel,
            data.EquippedWeaponInstanceIndex,
            currentInfluence,
            currentHp <= 0f);
    }

    private static void LoadBattleScene(string sceneName)
    {
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }
}
