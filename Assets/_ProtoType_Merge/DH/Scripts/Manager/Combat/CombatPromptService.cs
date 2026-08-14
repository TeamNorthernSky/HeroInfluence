using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CombatPromptService : MonoBehaviour
{
    private readonly struct CombatPromptPreview
    {
        public CombatPromptPreview(
            CombatAdvantageEvaluation evaluation,
            IReadOnlyList<int> heroUnitIndices,
            IReadOnlyList<string> enemyPortraitKeys)
        {
            Evaluation = evaluation;
            HeroUnitIndices = heroUnitIndices;
            EnemyPortraitKeys = enemyPortraitKeys;
        }

        public CombatAdvantageEvaluation Evaluation { get; }
        public IReadOnlyList<int> HeroUnitIndices { get; }
        public IReadOnlyList<string> EnemyPortraitKeys { get; }
    }

    [Header("References")]
    [SerializeField] private CombatPromptPanelController promptPrefab;
    [SerializeField] private Transform promptRoot;
    [SerializeField] private BattleResultPanel victoryResultPrefab;
    [SerializeField] private BattleResultPanel defeatResultPrefab;
    [SerializeField] private Transform resultRoot;

    [Header("Modal Backdrop")]
    [SerializeField] private Image combatPanelModal;
    [SerializeField] private Color modalColor = new Color(0f, 0f, 0f, 0.6f);

    private CombatPromptPanelController promptInstance;
    private BattleResultPanel resultInstance;
    private Action pendingStartBattle;
    private Action pendingFlee;
    private Action pendingSkipBattle;
    private Action<bool> pendingClosed;
    private Coroutine pendingFleeCoroutine;
    private Coroutine pendingSkipCoroutine;
    private GraphicRaycaster promptRaycaster;
    private bool capturedPromptRaycasterState;
    private bool previousPromptRaycasterEnabled;

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
        CombatPromptPreview preview = BuildPromptPreview(prepareContext, combatEncounterManager);
        EnablePromptRaycaster();

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
            preview.Evaluation.State,
            preview.HeroUnitIndices,
            preview.EnemyPortraitKeys);
        ShowModalBackdrop();
        promptInstance.transform.SetAsLastSibling();
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
        CombatPromptPreview preview = BuildPromptPreview(prepareContext, combatEncounterManager);
        EnablePromptRaycaster();

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
            preview.Evaluation.State,
            preview.HeroUnitIndices,
            preview.EnemyPortraitKeys);
        ShowModalBackdrop();
        promptInstance.transform.SetAsLastSibling();
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
        CombatPromptPreview preview = BuildPromptPreview(prepareContext, combatEncounterManager);
        EnablePromptRaycaster();

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
            preview.Evaluation.State,
            preview.HeroUnitIndices,
            preview.EnemyPortraitKeys);
        ShowModalBackdrop();
        promptInstance.transform.SetAsLastSibling();
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

    private CombatPromptPreview BuildPromptPreview(
        Func<bool> prepareCombatContext,
        CombatEncounterManager combatEncounterManager)
    {
        if (prepareCombatContext == null || combatEncounterManager == null)
            return CreateEmptyPreview();

        bool prepared = prepareCombatContext.Invoke();
        if (!prepared)
            return CreateEmptyPreview();

        CombatContext context = CombatContext.Instance;
        CombatAdvantageEvaluation evaluation = CombatSkipCalculator.EvaluateAdvantage(context);
        // [JC 260628] 전력평가 아군 = 전열→후열 압축(PartyFormation 공용 규약). 포트레이트 4칸에
        // 희소 슬롯배열을 그대로 쓰면 후열 유닛이 잘려 2명 누락되던 문제 교정.
        int[] heroUnitIndices = CopyUnitIndices(context != null ? context.CombatParty?.UnitIndices : null);
        string[] enemyPortraitKeys = BuildEnemyPortraitKeys(context);
        context?.Clear();
        combatEncounterManager.ClearCombatState();
        return new CombatPromptPreview(evaluation, heroUnitIndices, enemyPortraitKeys);
    }

    private static CombatPromptPreview CreateEmptyPreview()
    {
        return new CombatPromptPreview(
            new CombatAdvantageEvaluation(CombatAdvantageState.Close, 0f, 0f),
            Array.Empty<int>(),
            Array.Empty<string>());
    }

    private static string[] BuildEnemyPortraitKeys(CombatContext context)
    {
        if (!CombatEnemyTemplatePreviewBuilder.TryBuildFromContext(
                context,
                out IReadOnlyList<CombatEnemyTemplatePreviewUnit> units))
        {
            return Array.Empty<string>();
        }

        return CombatEnemyTemplatePreviewBuilder.BuildPortraitKeys(units);
    }

    private static int[] CopyUnitIndices(IReadOnlyList<int> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<int>();

        int[] copy = new int[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = Mathf.Max(0, source[i]);

        return copy;
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
        ShowModalBackdrop();
        resultInstance.transform.SetAsLastSibling();
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
        ShowModalBackdrop();
        resultInstance.transform.SetAsLastSibling();
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
        HideModalBackdrop();
        RestorePromptRaycaster();
        Action<bool> closed = pendingClosed;
        pendingClosed = null;
        pendingStartBattle = null;
        pendingFlee = null;
        pendingSkipBattle = null;
        closed?.Invoke(keepInputLocked);
    }

    private void ShowModalBackdrop()
    {
        Image modal = ResolveModalBackdrop();
        if (modal == null)
            return;

        modal.color = modalColor;
        modal.raycastTarget = true;
        modal.gameObject.SetActive(true);
        modal.transform.SetAsLastSibling();
    }

    private void HideModalBackdrop()
    {
        if (combatPanelModal == null)
            return;

        combatPanelModal.gameObject.SetActive(false);
    }

    private Image ResolveModalBackdrop()
    {
        if (combatPanelModal != null)
            return combatPanelModal;

        GameObject modalObject = GameObject.Find("CombatPanelModal");
        if (modalObject != null)
        {
            combatPanelModal = modalObject.GetComponent<Image>();
            return combatPanelModal;
        }

        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.name == "CombatPanelModal")
            {
                combatPanelModal = image;
                return combatPanelModal;
            }
        }

        return null;
    }

    private void EnablePromptRaycaster()
    {
        GraphicRaycaster raycaster = ResolvePromptRaycaster();
        if (raycaster == null)
            return;

        if (!capturedPromptRaycasterState || promptRaycaster != raycaster)
        {
            promptRaycaster = raycaster;
            previousPromptRaycasterEnabled = raycaster.enabled;
            capturedPromptRaycasterState = true;
        }

        raycaster.enabled = true;
    }

    private void RestorePromptRaycaster()
    {
        if (!capturedPromptRaycasterState)
            return;

        if (promptRaycaster != null)
            promptRaycaster.enabled = previousPromptRaycasterEnabled;

        promptRaycaster = null;
        previousPromptRaycasterEnabled = false;
        capturedPromptRaycasterState = false;
    }

    private GraphicRaycaster ResolvePromptRaycaster()
    {
        Transform root = promptRoot != null ? promptRoot : transform;
        Canvas canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
        if (canvas == null && promptInstance != null)
            canvas = promptInstance.GetComponentInParent<Canvas>();

        return canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
    }
}
