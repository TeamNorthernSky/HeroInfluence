using System;
using System.Collections;
using UnityEngine;

public class CombatPromptService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatPromptPanelController promptPrefab;
    [SerializeField] private Transform promptRoot;
    [SerializeField] private BattleResultPanel defeatResultPrefab;
    [SerializeField] private Transform resultRoot;

    private CombatPromptPanelController promptInstance;
    private BattleResultPanel resultInstance;
    private Action pendingStartBattle;
    private Action pendingFlee;
    private Action<bool> pendingClosed;
    private Coroutine pendingFleeCoroutine;

    public bool IsOpen =>
        promptInstance != null && promptInstance.gameObject.activeInHierarchy ||
        resultInstance != null && resultInstance.gameObject.activeInHierarchy ||
        pendingFleeCoroutine != null;

    public bool TryOpenEnemyCombatPrompt(
        PartyGridMover party,
        EnemyGridMover enemy,
        CombatEncounterManager combatEncounterManager,
        Action<bool> onClosed)
    {
        if (party == null || enemy == null || combatEncounterManager == null)
            return false;

        if (!EnsurePromptInstance())
            return false;

        if (IsOpen)
            return true;

        pendingClosed = onClosed;
        pendingStartBattle = () =>
        {
            bool combatStarted = combatEncounterManager.BeginCombat(party, enemy);
            ClosePrompt(combatStarted);
        };
        pendingFlee = () =>
        {
            BeginFleeDefeat(() => combatEncounterManager.PrepareEnemyCombatContext(party, enemy), combatEncounterManager);
        };

        promptInstance.Open(HandleStartBattleClicked, HandleFleeClicked);
        return true;
    }

    public bool TryOpenOutpostDefenderCombatPrompt(
        PartyGridMover party,
        Outpost outpost,
        CombatEncounterManager combatEncounterManager,
        Action<bool> onClosed)
    {
        if (party == null || outpost == null || combatEncounterManager == null)
            return false;

        if (!EnsurePromptInstance())
            return false;

        if (IsOpen)
            return true;

        pendingClosed = onClosed;
        pendingStartBattle = () =>
        {
            bool combatStarted = combatEncounterManager.BeginOutpostDefenderCombat(party, outpost);
            ClosePrompt(combatStarted);
        };
        pendingFlee = () =>
        {
            BeginFleeDefeat(() => combatEncounterManager.PrepareOutpostDefenderCombatContext(party, outpost), combatEncounterManager);
        };

        promptInstance.Open(HandleStartBattleClicked, HandleFleeClicked);
        return true;
    }

    public bool TryOpenVillainUnionDefenderCombatPrompt(
        PartyGridMover party,
        VillainUnionBase villainUnionBase,
        CombatEncounterManager combatEncounterManager,
        Action<bool> onClosed)
    {
        if (party == null || villainUnionBase == null || combatEncounterManager == null)
            return false;

        if (!EnsurePromptInstance())
            return false;

        if (IsOpen)
            return true;

        pendingClosed = onClosed;
        pendingStartBattle = () =>
        {
            bool combatStarted = combatEncounterManager.BeginVillainUnionDefenderCombat(party, villainUnionBase);
            ClosePrompt(combatStarted);
        };
        pendingFlee = () =>
        {
            BeginFleeDefeat(() => combatEncounterManager.PrepareVillainUnionDefenderCombatContext(party, villainUnionBase), combatEncounterManager);
        };

        promptInstance.Open(HandleStartBattleClicked, HandleFleeClicked);
        return true;
    }

    private bool EnsurePromptInstance()
    {
        if (promptInstance != null)
            return true;

        if (promptPrefab == null)
            return false;

        Transform parent = promptRoot != null ? promptRoot : transform;
        promptInstance = Instantiate(promptPrefab, parent);
        promptInstance.gameObject.SetActive(false);
        return true;
    }

    private void HandleStartBattleClicked()
    {
        Action startBattle = pendingStartBattle;
        pendingStartBattle = null;
        startBattle?.Invoke();
    }

    private void HandleFleeClicked()
    {
        Action flee = pendingFlee;
        pendingFlee = null;
        flee?.Invoke();
    }

    private void BeginFleeDefeat(Func<bool> prepareCombatContext, CombatEncounterManager combatEncounterManager)
    {
        if (pendingFleeCoroutine != null)
            return;

        if (promptInstance != null)
            promptInstance.Close();

        if (prepareCombatContext == null || combatEncounterManager == null || defeatResultPrefab == null)
        {
            ClosePrompt(false);
            return;
        }

        bool prepared = prepareCombatContext.Invoke();
        if (!prepared)
        {
            ClosePrompt(false);
            return;
        }

        pendingFleeCoroutine = StartCoroutine(RunFleeDefeatSequence(combatEncounterManager));
    }

    private IEnumerator RunFleeDefeatSequence(CombatEncounterManager combatEncounterManager)
    {
        CombatContext context = CombatContext.Instance;
        BattleRewardPlan plan = ExplorationDefeatResultHandler.BuildDefeatPlan(context);
        Transform root = ResolveResultRoot();
        if (root != null)
            root.gameObject.SetActive(true);

        resultInstance = Instantiate(defeatResultPrefab, root, false);
        PrepareResultPanelInteraction(resultInstance);
        bool accepted = false;
        resultInstance.OnAccepted += () => accepted = true;
        resultInstance.Show(BattleResult.Defeat, plan);

        yield return new WaitUntil(() => accepted);

        ExplorationDefeatResultHandler.CommitDefeat(context);
        if (context != null)
            context.SetCombatResult(CombatResult.Defeat);

        combatEncounterManager.ApplyCurrentCombatResultAndClear();

        if (resultInstance != null)
            Destroy(resultInstance.gameObject);

        resultInstance = null;
        pendingFleeCoroutine = null;
        FinishPrompt(false);
    }

    private Transform ResolveResultRoot()
    {
        if (resultRoot != null)
            return resultRoot;

        if (promptRoot != null)
            return promptRoot;

        return transform;
    }

    private static void PrepareResultPanelInteraction(BattleResultPanel panel)
    {
        if (panel == null)
            return;

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.ignoreParentGroups = true;
    }

    private void ClosePrompt(bool startedCombat)
    {
        if (promptInstance != null)
            promptInstance.Close();

        FinishPrompt(startedCombat);
    }

    private void FinishPrompt(bool keepInputLocked)
    {
        Action<bool> closed = pendingClosed;
        pendingClosed = null;
        pendingStartBattle = null;
        pendingFlee = null;
        closed?.Invoke(keepInputLocked);
    }
}
