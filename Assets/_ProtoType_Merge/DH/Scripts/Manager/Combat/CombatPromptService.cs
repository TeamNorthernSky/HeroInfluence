using System;
using System.Collections;
using UnityEngine;

public class CombatPromptService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CombatPromptPanelController promptPrefab;
    [SerializeField] private Transform promptRoot;
    [SerializeField] private BattleResultPanel victoryResultPrefab;
    [SerializeField] private BattleResultPanel defeatResultPrefab;
    [SerializeField] private Transform resultRoot;

    private CombatPromptPanelController promptInstance;
    private BattleResultPanel resultInstance;
    private Action pendingStartBattle;
    private Action pendingFlee;
    private Action pendingSkipBattle;
    private Action<bool> pendingClosed;
    private Coroutine pendingFleeCoroutine;
    private Coroutine pendingSkipCoroutine;

    public bool IsOpen =>
        promptInstance != null && promptInstance.gameObject.activeInHierarchy ||
        resultInstance != null && resultInstance.gameObject.activeInHierarchy ||
        pendingFleeCoroutine != null ||
        pendingSkipCoroutine != null;

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

        Func<bool> prepareContext = () => combatEncounterManager.PrepareEnemyCombatContext(party, enemy);
        CombatAdvantageEvaluation evaluation = PreviewAdvantage(prepareContext, combatEncounterManager);

        pendingClosed = onClosed;
        pendingStartBattle = () =>
        {
            bool combatStarted = combatEncounterManager.BeginCombat(party, enemy);
            ClosePrompt(combatStarted);
        };
        pendingFlee = () =>
        {
            BeginFleeDefeat(prepareContext, combatEncounterManager);
        };
        pendingSkipBattle = () =>
        {
            BeginSkipCombat(prepareContext, combatEncounterManager);
        };

        promptInstance.Open(
            HandleStartBattleClicked,
            HandleFleeClicked,
            HandleSkipBattleClicked,
            CombatSkipCalculator.GetDisplayText(evaluation.State));
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

        Func<bool> prepareContext = () => combatEncounterManager.PrepareOutpostDefenderCombatContext(party, outpost);
        CombatAdvantageEvaluation evaluation = PreviewAdvantage(prepareContext, combatEncounterManager);

        pendingClosed = onClosed;
        pendingStartBattle = () =>
        {
            bool combatStarted = combatEncounterManager.BeginOutpostDefenderCombat(party, outpost);
            ClosePrompt(combatStarted);
        };
        pendingFlee = () =>
        {
            BeginFleeDefeat(prepareContext, combatEncounterManager);
        };
        pendingSkipBattle = () =>
        {
            BeginSkipCombat(prepareContext, combatEncounterManager);
        };

        promptInstance.Open(
            HandleStartBattleClicked,
            HandleFleeClicked,
            HandleSkipBattleClicked,
            CombatSkipCalculator.GetDisplayText(evaluation.State));
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

        Func<bool> prepareContext = () => combatEncounterManager.PrepareVillainUnionDefenderCombatContext(party, villainUnionBase);
        CombatAdvantageEvaluation evaluation = PreviewAdvantage(prepareContext, combatEncounterManager);

        pendingClosed = onClosed;
        pendingStartBattle = () =>
        {
            bool combatStarted = combatEncounterManager.BeginVillainUnionDefenderCombat(party, villainUnionBase);
            ClosePrompt(combatStarted);
        };
        pendingFlee = () =>
        {
            BeginFleeDefeat(prepareContext, combatEncounterManager);
        };
        pendingSkipBattle = () =>
        {
            BeginSkipCombat(prepareContext, combatEncounterManager);
        };

        promptInstance.Open(
            HandleStartBattleClicked,
            HandleFleeClicked,
            HandleSkipBattleClicked,
            CombatSkipCalculator.GetDisplayText(evaluation.State));
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

    private void HandleSkipBattleClicked()
    {
        Action skipBattle = pendingSkipBattle;
        pendingSkipBattle = null;
        skipBattle?.Invoke();
    }

    private CombatAdvantageEvaluation PreviewAdvantage(
        Func<bool> prepareCombatContext,
        CombatEncounterManager combatEncounterManager)
    {
        if (prepareCombatContext == null || combatEncounterManager == null)
            return new CombatAdvantageEvaluation(CombatAdvantageState.Close, 0f, 0f);

        bool prepared = prepareCombatContext.Invoke();
        if (!prepared)
            return new CombatAdvantageEvaluation(CombatAdvantageState.Close, 0f, 0f);

        CombatContext context = CombatContext.Instance;
        CombatAdvantageEvaluation evaluation = CombatSkipCalculator.EvaluateAdvantage(context);
        context?.Clear();
        combatEncounterManager.ClearCombatState();
        return evaluation;
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

    private void BeginSkipCombat(Func<bool> prepareCombatContext, CombatEncounterManager combatEncounterManager)
    {
        if (pendingSkipCoroutine != null)
            return;

        if (promptInstance != null)
            promptInstance.Close();

        if (prepareCombatContext == null || combatEncounterManager == null)
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

        CombatAdvantageEvaluation evaluation = CombatSkipCalculator.EvaluateAdvantage(CombatContext.Instance);
        CombatSkipDecision decision = CombatSkipCalculator.ResolveDecision(evaluation.State);
        CombatSkipHpResult hpResult = CombatSkipCalculator.CalculateHpResult(CombatContext.Instance, decision);
        pendingSkipCoroutine = StartCoroutine(RunSkipCombatSequence(decision, hpResult, combatEncounterManager));
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

    private IEnumerator RunSkipCombatSequence(
        CombatSkipDecision decision,
        CombatSkipHpResult hpResult,
        CombatEncounterManager combatEncounterManager)
    {
        CombatContext context = CombatContext.Instance;
        BattleResultPanel prefab = decision == CombatSkipDecision.Victory
            ? victoryResultPrefab
            : defeatResultPrefab;

        if (prefab == null)
        {
            context?.Clear();
            combatEncounterManager.ClearCombatState();
            pendingSkipCoroutine = null;
            FinishPrompt(false);
            yield break;
        }

        BattleRewardPlan plan = decision == CombatSkipDecision.Victory
            ? ExplorationCombatSkipResultHandler.BuildVictoryPlan(context)
            : ExplorationDefeatResultHandler.BuildDefeatPlan(context);

        Transform root = ResolveResultRoot();
        if (root != null)
            root.gameObject.SetActive(true);

        resultInstance = Instantiate(prefab, root, false);
        PrepareResultPanelInteraction(resultInstance);
        bool accepted = false;
        resultInstance.OnAccepted += () => accepted = true;
        BattleResult battleResult = decision == CombatSkipDecision.Victory
            ? BattleResult.Victory
            : BattleResult.Defeat;
        resultInstance.Show(battleResult, plan);

        yield return new WaitUntil(() => accepted);

        if (decision == CombatSkipDecision.Victory)
        {
            ExplorationCombatSkipResultHandler.CommitVictory(
                context,
                plan,
                resultInstance.GetSkillResults(),
                hpResult);
            context?.SetCombatResult(CombatResult.Victory);
        }
        else
        {
            ExplorationCombatSkipResultHandler.CommitDefeat(context, hpResult);
            context?.SetCombatResult(CombatResult.Defeat);
        }

        combatEncounterManager.ApplyCurrentCombatResultAndClear();

        if (resultInstance != null)
            Destroy(resultInstance.gameObject);

        resultInstance = null;
        pendingSkipCoroutine = null;
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
        pendingSkipBattle = null;
        closed?.Invoke(keepInputLocked);
    }
}
