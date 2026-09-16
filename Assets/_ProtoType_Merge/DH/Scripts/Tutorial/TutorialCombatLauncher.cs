using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TutorialCombatLauncher : MonoBehaviour
{
    private const string DefaultPartyId = "TUTORIAL_PARTY";

    [Header("Scene")]
    [SerializeField] private string tutorialBattleSceneName;
    [SerializeField] private string returnSceneName = "TutorialExploreScene";
    [SerializeField] private bool allowBattleSceneLoad;

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
        get => string.IsNullOrWhiteSpace(returnSceneName) ? "TutorialExploreScene" : returnSceneName.Trim();
        set => returnSceneName = value;
    }

    public bool BeginCombat(string enemyGroupKey, int enemyLevel)
    {
        return BeginCombat(enemyGroupKey, enemyLevel, ReturnSceneName);
    }

    public bool BeginCombat(string enemyGroupKey, int enemyLevel, string nextReturnSceneName)
    {
        string groupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        if (string.IsNullOrEmpty(groupKey))
        {
            Debug.LogWarning("[TutorialCombatLauncher] EnemyGroupKey is empty.", this);
            return false;
        }

        CombatContext context = CombatContext.Instance;
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

        if (!context.BeginTutorial(
                DefaultPartyId,
                allies,
                groupKey,
                Mathf.Max(1, enemyLevel),
                string.IsNullOrWhiteSpace(nextReturnSceneName) ? ReturnSceneName : nextReturnSceneName.Trim()))
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

        if (defaultUnitTemplateKeys == null || defaultUnitTemplateKeys.Count == 0)
        {
            error = "Default unit list is empty.";
            return false;
        }

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        for (int i = 0; i < defaultUnitTemplateKeys.Count; i++)
        {
            string unitKey = defaultUnitTemplateKeys[i];
            if (string.IsNullOrWhiteSpace(unitKey))
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
            repository.SetUnitJoined(data.UnitTemplateKey, true);
            repository.SetUnitStats(
                data.UnitTemplateKey,
                Mathf.RoundToInt(data.CurrentHp),
                Mathf.RoundToInt(data.IngameStats.HP),
                Mathf.RoundToInt(data.CurrentInfluence),
                Mathf.RoundToInt(data.IngameStats.Influence),
                Mathf.RoundToInt(data.IngameStats.Atk));

            allies.Add(runtimeData);
        }

        if (allies.Count == 0)
        {
            error = "No valid tutorial allies were created.";
            return false;
        }

        return true;
    }

    private static void LoadBattleScene(string sceneName)
    {
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }
}
