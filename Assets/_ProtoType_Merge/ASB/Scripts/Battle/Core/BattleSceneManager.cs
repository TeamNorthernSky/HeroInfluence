using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// 전투 진입 순서: ManualSpawn → SyncGridOccupancy → CollectParticipantsAfterInitialize → BattleFlowManager.Initialize.
/// 각 스포너는 BattleSceneManager가 호출하기 전까지 Start에서 자동 스폰하지 않도록 유지합니다.
/// </summary>
public class BattleSceneManager : MonoBehaviour
{
    private const int EventBattleLogicalSlotCount = 6;
    private const float CueEffectWaitTimeoutSeconds = EffectFallbackRelease.DefaultFallbackSeconds + 0.5f;
    private const float DeathAnimationWaitTimeoutSeconds = 1.2f;
    private const float DeathAnimationEndThreshold = 0.95f;

    [Header("Prototype Boot")]
    [Tooltip("Prototype 전용: 씬에 배치된 BattleCharactor를 그대로 초기화해 전투를 시작합니다.")]
    [SerializeField] private bool includeInactiveUnits = true;
    [SerializeField] private Transform playerPlace;
    [SerializeField] private Transform enemyPlace;
    [SerializeField] private BattleFlowManager battleFlowManager;
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Tutorial Battle")]
    [SerializeField] private TutorialBattleDirector tutorialBattleDirector;
    [SerializeField] private TutorialBattleUI tutorialBattleUI;
    [Tooltip("이벤트 전투가 아닐 때(씬 직접 진입/테스트) 사용할 튜토리얼 BattleKey. 비면 튜토리얼 미적용.")]
    [SerializeField] private string tutorialBattleKeyOverride;
    [SerializeField, Min(-1)] private int tutorialZoneIdOverride = -1;
    private TutorialBattleFlowRegistry tutorialFlowRegistry;

    [Header("UI")]
    [SerializeField] private BattleUIManager battleUIManager;

    [Header("Scene Transition")]
    [Tooltip("Build Settings에 등록된 씬 이름(확장자 제외). 예: DHScene")]
    [SerializeField] private string returnSceneName = "DHScene";
    [SerializeField] private float returnDelay = 3f;
    // [JC 260513] 페이드 시간. fadeIn은 0(=즉시) — 검정 화면 유지 방지용 SceneFadeController 최소값 클램프.
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float fadeInDuration = 0f;

    private Coroutine returnSceneCoroutine;

    private BattleCharactor playerBattleCharactor;
    private readonly List<BattleCharactor> playerBattleCharactors = new List<BattleCharactor>();
    private readonly List<BattleCharactor> enemyBattleCharactors = new List<BattleCharactor>();
    private float victoryEnemyExperienceSnapshot;
    private HostageScenarioController hostageScenarioController;

    public BattleCharactor PlayerBattleCharactor => playerBattleCharactor;

    /// <summary>디버그/멀티 플레이어 전투체. 첫 번째 플레이어는 <see cref="PlayerBattleCharactor"/>와 동일하게 유지.</summary>
    public IReadOnlyList<BattleCharactor> PlayerBattleCharactors => playerBattleCharactors;

    /// <summary>소환된 적 전투체 목록.</summary>
    public IReadOnlyList<BattleCharactor> EnemyBattleCharactors => enemyBattleCharactors;

    private void OnEnable()
    {
        if (battleFlowManager != null)
        {
            battleFlowManager.OnBattleEnded += HandleBattleEndedForTransition;
        }
    }

    private void OnDisable()
    {
        hostageScenarioController?.FlushResult();
        tutorialBattleDirector?.Shutdown();

        if (battleFlowManager != null)
        {
            battleFlowManager.OnBattleEnded -= HandleBattleEndedForTransition;
        }

        if (returnSceneCoroutine != null)
        {
            StopCoroutine(returnSceneCoroutine);
            returnSceneCoroutine = null;
        }
    }

    private void HandleBattleEndedForTransition(BattleResult result)
    {
        if (returnSceneCoroutine != null)
            return;

        returnSceneCoroutine = StartCoroutine(PostBattleSequence(result));
    }

    private IEnumerator PostBattleSequence(BattleResult result)
    {
        yield return WaitForActivePresentationSequence();
        yield return WaitForRemainingCueEffectsAndDeathAnimations();
        hostageScenarioController?.FlushResult();

        CombatContext combatContext = CombatContext.Instance;
        bool isSimulation = combatContext != null && combatContext.IsSimulation;
        bool isTutorial = combatContext != null && combatContext.IsTutorial;

        // 1. 보상 계산 (Repository/JSON 변경 없음)
        // 튜토리얼 전투는 일반 영속 저장소/보상 루프를 사용하지 않는다.
        BattleRewardPlan plan = isTutorial
            ? null
            : BattleResultPersistenceHandler.BuildBattleRewardPlan(
                playerBattleCharactors, result, victoryEnemyExperienceSnapshot);

        // 2. 레벨업 UI (TODO: 레벨업 UI가 생기면 여기서 yield return)
        // if (plan.UnitPreviews.Exists(u => u.HasLevelUp))
        //     yield return battleUIManager.ShowLevelUpSequence(plan);

        // 3. CombatContext 결과 설정
        if (combatContext != null)
        {
            CombatResult mappedResult = result switch
            {
                BattleResult.Victory   => CombatResult.Victory,
                BattleResult.Defeat    => CombatResult.Defeat,
                BattleResult.Escape    => CombatResult.Escape,
                BattleResult.Cancelled => CombatResult.Cancelled,
                _                      => CombatResult.None,
            };
            combatContext.SetCombatResult(mappedResult);
        }

        // 4. 승패 결과 UI (레벨업 스킬 슬롯 포함, Accept 버튼은 슬롯 모두 처리 후 활성화)
        // 모의/튜토리얼 전투는 일반 보상·스킬을 지급하지 않는다.
        BattleResultPanel resultPanel = battleUIManager?.ShowBattleResultUI(
            result,
            isSimulation || isTutorial ? null : plan);

        // 결과창 표시 후·Accept 전: 튜토리얼 flow에 통지(비차단 오버레이 기본).
        if (resultPanel != null)
        {
            tutorialBattleDirector?.NotifyBattleResultShown(result);
        }

        // 5. Accept 버튼 대기
        if (resultPanel != null)
        {
            bool accepted = false;
            resultPanel.OnAccepted += () => accepted = true;
            yield return new WaitUntil(() => accepted);
            tutorialBattleDirector?.NotifyBattleResultAccepted(result);
        }

        // 6. 저장 (스킬 선택 결과 포함)
        var skillResults = resultPanel?.GetSkillResults() ?? new System.Collections.Generic.List<SkillSelectionResult>();
        if (isTutorial)
        {
            if (!TutorialCombatResultProcessor.TryPersistAllies(playerBattleCharactors, out string tutorialSaveError))
            {
                Debug.LogError($"[BattleSceneManager] Tutorial ally save failed. {tutorialSaveError}", this);
                returnSceneCoroutine = null;
                yield break;
            }
        }
        else if (!isSimulation)
        {
            BattleResultPersistenceHandler.CommitBattleRewardPlan(
                plan, playerBattleCharactors, enemyBattleCharactors, result, skillResults);
        }

        string contextReturnScene = (isSimulation || isTutorial) && combatContext != null
            ? combatContext.ReturnSceneName
            : string.Empty;
        if (isSimulation && combatContext != null)
            combatContext.ClearSimulation();

        // 8. 씬 전환
        // [JC 260514] returnSceneName 빈 값이라도 GameSceneManager.Instance.ExplorationScene fallback이 있으면 통과.
        if (string.IsNullOrWhiteSpace(contextReturnScene) &&
            string.IsNullOrWhiteSpace(returnSceneName) &&
            GameSceneManager.Instance == null)
        {
            Debug.LogWarning("[BattleSceneManager] returnSceneName + GameSceneManager.Instance 모두 없음 — 씬 전환을 건너뜁니다.");
            returnSceneCoroutine = null;
            yield break;
        }

        yield return StartCoroutine(TransitionToSceneRoutine(contextReturnScene));
    }

    private IEnumerator WaitForActivePresentationSequence()
    {
        BattleManager battleManager = battleFlowManager != null ? battleFlowManager.BattleManager : null;
        if (battleManager == null)
        {
            yield break;
        }

        yield return new WaitUntil(() => !battleManager.Presentation.IsSequenceRunning);
    }

    private IEnumerator WaitForRemainingCueEffectsAndDeathAnimations()
    {
        float elapsed = 0f;
        float deathAnimationElapsed = 0f;
        HashSet<BattleCharactor> observedDeathAnimations = new HashSet<BattleCharactor>();
        while (elapsed < CueEffectWaitTimeoutSeconds)
        {
            bool waitingForCueEffects = HasActiveOneShotCueEffects();
            bool waitingForDeathAnimation = deathAnimationElapsed < DeathAnimationWaitTimeoutSeconds
                && HasPendingDeathAnimations(observedDeathAnimations);
            if (!waitingForCueEffects && !waitingForDeathAnimation)
            {
                yield break;
            }

            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            if (waitingForDeathAnimation)
            {
                deathAnimationElapsed += deltaTime;
            }
            yield return null;
        }

        LogCueEffectWaitTimeout();
    }

    private bool HasPendingDeathAnimations(HashSet<BattleCharactor> observedDeathAnimations)
    {
        foreach (BattleCharactor unit in playerBattleCharactors.Concat(enemyBattleCharactors))
        {
            if (unit == null || !unit.IsDead)
            {
                continue;
            }

            unit.EnsureAnimationController();
            CharactorAnimationController anim = unit.Anim;
            if (anim == null)
            {
                continue;
            }

            if (anim.IsInState("Dead"))
            {
                observedDeathAnimations.Add(unit);
                if (!anim.IsStateNearEnd("Dead", DeathAnimationEndThreshold))
                {
                    return true;
                }
            }
            else if (!observedDeathAnimations.Contains(unit))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasActiveOneShotCueEffects()
    {
        foreach (BattleCharactor unit in playerBattleCharactors.Concat(enemyBattleCharactors))
        {
            PresentationRuntimeContext context = unit != null
                ? unit.GetComponent<PresentationRuntimeContext>()
                : null;
            if (context != null && context.HasActiveOneShotCueEffects)
            {
                return true;
            }
        }

        return false;
    }

    private void LogCueEffectWaitTimeout()
    {
        var remainingEffectNames = new List<string>();
        foreach (BattleCharactor unit in playerBattleCharactors.Concat(enemyBattleCharactors))
        {
            PresentationRuntimeContext context = unit != null
                ? unit.GetComponent<PresentationRuntimeContext>()
                : null;
            context?.CollectActiveOneShotCueEffectNames(remainingEffectNames);
        }

        if (remainingEffectNames.Count > 0)
        {
            Debug.LogWarning(
                $"[BattleSceneManager] Cue effect wait timed out after {CueEffectWaitTimeoutSeconds:0.##}s. " +
                $"Continuing to result UI. Remaining={string.Join(", ", remainingEffectNames)}",
                this);
        }
    }

    private IEnumerator TransitionToSceneRoutine(string targetOverride = null)
    {
        if (returnDelay > 0f)
        {
            yield return new WaitForSeconds(returnDelay);
        }

        // [JC 260514] target 결정 흐름:
        //   1) returnSceneName 인스펙터 값 우선 (인스펙터 명시 시)
        //   2) 빈 값이면 GameSceneManager.Instance.ExplorationScene 토글
        //   3) Instance도 null이면 최후 fallback "DHScene_2"
        string target;
        if (!string.IsNullOrWhiteSpace(targetOverride))
            target = targetOverride.Trim();
        else if (!string.IsNullOrWhiteSpace(returnSceneName))
            target = returnSceneName.Trim();
        else if (GameSceneManager.Instance != null)
            target = GameSceneManager.Instance.ExplorationScene;
        else
            target = "DHScene_2";
        Debug.Log($"[BattleSceneManager] TransitionTo target='{target}' returnSceneName='{returnSceneName}' instanceOk={GameSceneManager.Instance != null} fadeOk={SceneFadeController.Instance != null}");
        // [JC 260513] SceneFadeController 영속(GameManager 자식). 없으면 직접 LoadScene fallback.
        SceneFadeController fade = SceneFadeController.Instance;
        if (fade != null)
            fade.FadeToScene(target, fadeOutDuration, fadeInDuration);
        // [JC 260514] GameSceneManager 컴포넌트 격상 — Instance 경유 호출.
        else if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(target);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(target);

        returnSceneCoroutine = null;
    }

    private void Start()
    {
        CombatContext combatContext = CombatContext.Instance;
        bool isEventBattle = combatContext != null && combatContext.HasEventBattle;

        if (battleFlowManager == null)
        {
            const string error = "battleFlowManager is not assigned.";
            Debug.LogError($"[BattleSceneManager] {error}", this);
            if (isEventBattle)
                AbortEventBattleSetup(combatContext, error);
            return;
        }

        playerBattleCharactor = null;
        playerBattleCharactors.Clear();
        enemyBattleCharactors.Clear();
        victoryEnemyExperienceSnapshot = 0f;

        playerSpawner?.SetSpawnOnStart(false);
        enemySpawner?.SetSpawnOnStart(false);

        BattleLogicalSlotMap eventSlotMap = null;
        if (isEventBattle &&
            !TryPrepareEventSlotMap(combatContext, out eventSlotMap, out string slotMapError))
        {
            AbortEventBattleSetup(combatContext, slotMapError);
            return;
        }

        playerSpawner?.ManualSpawn();

        // 이벤트 전투: (ZoneId, BattleKey, EnemyLevel) 키로 플랜을 직접 빌드해 스폰한다(§5).
        //   플랜 빌드 → 적 스폰(plan.Scenario로 인질 유닛 제외) → 인질 컨트롤러 생성.
        //   빌드/스폰 실패 시 폴백 없이 즉시 진입 실패(Abort) — 키-빌드 문제를 조용히 가리지 않는다.
        BattleScenarioConfig scenario = null;
        if (isEventBattle)
        {
            CombatEventBattleData eventBattle = combatContext.EventBattle;
            if (!EnemySpawnPlanBuilder.TryBuildFromEventBattleKey(
                    eventBattle.ZoneId, eventBattle.BattleKey, eventBattle.EnemyLevel,
                    out EnemySpawnPlan eventPlan, out string planError))
            {
                AbortEventBattleSetup(combatContext, planError);
                return;
            }

            if (enemySpawner == null || !enemySpawner.SpawnFromPreparedPlan(eventPlan))
            {
                AbortEventBattleSetup(
                    combatContext,
                    $"No complete event combat enemy set was spawned. Battle={eventBattle.BattleKey}");
                return;
            }

            scenario = eventPlan.Scenario;
        }
        else
        {
            enemySpawner?.ManualSpawn();
        }

        if (scenario != null && scenario.IsHostageRescue)
        {
            hostageScenarioController = HostageScenarioController.Create(eventSlotMap, scenario);
            if (hostageScenarioController == null || !hostageScenarioController.IsFullyInitialized)
            {
                string hostageError = hostageScenarioController != null
                    ? hostageScenarioController.InitializationError
                    : "Hostage scenario controller could not be created.";
                AbortEventBattleSetup(combatContext, hostageError);
                return;
            }
        }

        var inactiveMode = includeInactiveUnits ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        var sceneUnits = FindObjectsByType<BattleCharactor>(inactiveMode, FindObjectsSortMode.None).ToList();
        if (sceneUnits.Count == 0)
        {
            const string error = "No BattleCharactor was found in the battle scene.";
            Debug.LogWarning($"[BattleSceneManager] {error}", this);
            if (isEventBattle)
                AbortEventBattleSetup(combatContext, error);
            return;
        }

        // 그리드 ↔ 유닛 점유 동기화 후, 각 BattleCharactor.Initialize()로 스탯·스킬·무기 확정
        SyncGridOccupancy(sceneUnits);
        CollectParticipantsAfterInitialize(sceneUnits);

        // 실제 스폰 계획의 보상 합계를 전투 시작 전에 스냅샷한다.
        // 이후 시체 GameObject가 제거돼도 승리 보상은 이 순수 값으로 계산된다.
        victoryEnemyExperienceSnapshot = enemySpawner != null && enemySpawner.HasSuccessfulSpawnPlanRewardSnapshot
            ? enemySpawner.LastSpawnPlanTotalExperience
            : CalculateInitialEnemyExperience(enemyBattleCharactors);

        if (isEventBattle)
        {
            bool hasAlivePlayer = playerBattleCharactors.Any(unit => unit != null && !unit.IsDead);
            bool hasAliveEnemy = enemyBattleCharactors.Any(unit => unit != null && !unit.IsDead);
            if (!hasAlivePlayer || !hasAliveEnemy)
            {
                AbortEventBattleSetup(
                    combatContext,
                    $"Event battle participant validation failed. " +
                    $"Players={playerBattleCharactors.Count}, Enemies={enemyBattleCharactors.Count}, " +
                    $"HasAlivePlayer={hasAlivePlayer}, HasAliveEnemy={hasAliveEnemy}");
                return;
            }
        }

        var allUnits = new List<BattleCharactor>(playerBattleCharactors.Count + enemyBattleCharactors.Count);
        allUnits.AddRange(playerBattleCharactors);
        allUnits.AddRange(enemyBattleCharactors);

        // Flow는 먼저 참가자만 초기화하고 멈춰 둔다. TutorialBattleDirector가 첫 턴 이벤트보다
        // 먼저 구독하고 입장 단계를 실행한 다음, 공통 전투 루프를 한 번만 시작한다.
        battleFlowManager.Initialize(allUnits, false);

        // 기본 경로는 '일반 전투'다. 튜토리얼은 진입 키가 매칭될 때만 켜지는 특수 케이스로 취급한다.
        // 진입 키 우선순위: 이벤트 전투 → 탐사에서 넘어온 CombatContext 튜토리얼 키 → 씬 override(테스트용, §11.1).
        bool hasEventBattle = combatContext != null && combatContext.HasEventBattle;
        int tutorialZoneId;
        string tutorialKey;
        if (hasEventBattle)
        {
            tutorialZoneId = combatContext.EventBattle.ZoneId;
            tutorialKey = combatContext.EventBattle.BattleKey;
        }
        else if (combatContext != null && combatContext.IsTutorial &&
                 !string.IsNullOrWhiteSpace(combatContext.TutorialBattleKey))
        {
            tutorialZoneId = combatContext.TutorialZoneId;
            tutorialKey = combatContext.TutorialBattleKey;
        }
        else if (!string.IsNullOrWhiteSpace(tutorialBattleKeyOverride))
        {
            tutorialZoneId = tutorialZoneIdOverride;
            tutorialKey = tutorialBattleKeyOverride.Trim();
        }
        else
        {
            tutorialZoneId = -1;
            tutorialKey = null;
        }

        ITutorialBattleFlow tutorialFlow = ResolveTutorialFlow(tutorialZoneId, tutorialKey);
        if (tutorialFlow == null)
        {
            // 일반 전투: 조용히 진행(로그 없음). 혹시 남아있을 Director만 정리한다.
            tutorialBattleDirector?.Shutdown();
        }
        else
        {
            // 튜토리얼 전투: 여기서만 로그를 남긴다.
            if (tutorialBattleDirector == null)
            {
                tutorialBattleDirector = GetComponent<TutorialBattleDirector>();
            }

            if (tutorialBattleDirector == null)
            {
                // 튜토리얼 키인데 씬에 Director가 없는 것은 설정 오류다(개발자가 봐야 함).
                Debug.LogError(
                    $"[Tutorial] 튜토리얼 전투(BattleKey={tutorialKey})인데 씬에 " +
                    $"TutorialBattleDirector가 배치되지 않았습니다. 튜토리얼 없이 전투만 진행합니다.",
                    this);
            }
            else
            {
                tutorialBattleDirector.enabled = true;
                if (tutorialBattleUI == null)
                {
                    tutorialBattleUI = FindFirstObjectByType<TutorialBattleUI>(FindObjectsInactive.Include);
                }

                if (tutorialBattleDirector.Initialize(
                        tutorialFlow,
                        battleFlowManager,
                        battleFlowManager.BattleManager,
                        enemySpawner,
                        playerSpawner,
                        tutorialBattleUI,
                        tutorialKey,
                        tutorialZoneId))
                {
                    Debug.Log($"[Tutorial] 튜토리얼 진입: BattleKey={tutorialKey}, Zone={tutorialZoneId}", this);
                }
                else
                {
                    Debug.LogWarning(
                        $"[Tutorial] 튜토리얼 초기화 실패(BattleKey={tutorialKey}). 튜토리얼 없이 전투만 진행합니다.",
                        this);
                }
            }
        }

        battleFlowManager.StartBattleLoop();
    }

    private static float CalculateInitialEnemyExperience(IReadOnlyList<BattleCharactor> enemies)
    {
        if (enemies == null)
            return 0f;

        float totalExperience = 0f;
        for (int i = 0; i < enemies.Count; i++)
        {
            BattleCharactor enemy = enemies[i];
            if (enemy != null)
                totalExperience += Mathf.Max(0f, enemy.ExperienceReward);
        }

        return totalExperience;
    }

    // 진입 키 해석(§11.1): 이벤트 전투면 CombatContext, 아니면 씬 override로 계산된 (zoneId, battleKey)를 받는다.
    private ITutorialBattleFlow ResolveTutorialFlow(int zoneId, string battleKey)
    {
        if (string.IsNullOrWhiteSpace(battleKey))
        {
            return null;
        }

        if (tutorialFlowRegistry == null)
        {
            tutorialFlowRegistry = TutorialBattleFlowRegistry.CreateDefault();
        }

        return tutorialFlowRegistry.Resolve(zoneId, battleKey);
    }

    private bool TryPrepareEventSlotMap(
        CombatContext combatContext,
        out BattleLogicalSlotMap slotMap,
        out string error)
    {
        slotMap = null;
        error = string.Empty;

        if (combatContext == null || !combatContext.HasEventBattle)
        {
            error = "Event battle context is missing.";
            return false;
        }

        if (enemyPlace == null)
        {
            error = "EnemyPlace is not assigned.";
            return false;
        }

        if (enemySpawner == null)
        {
            error = "EnemySpawner is not assigned.";
            return false;
        }

        if (enemySpawner.GridRoot != enemyPlace)
        {
            error =
                $"Enemy grid root mismatch. enemyPlace='{enemyPlace.name}', " +
                $"enemySpawnerRoot='{enemySpawner.GridRoot?.name ?? "<null>"}'.";
            return false;
        }

        if (playerPlace != null &&
            (playerPlace == enemyPlace ||
             playerPlace.IsChildOf(enemyPlace) ||
             enemyPlace.IsChildOf(playerPlace)))
        {
            error =
                $"Player and enemy grid roots overlap. " +
                $"playerPlace='{playerPlace.name}', enemyPlace='{enemyPlace.name}'.";
            return false;
        }

        if (!BattleLogicalSlotMap.TryCreate(enemyPlace, out slotMap, out error))
            return false;

        if (slotMap.Count != EventBattleLogicalSlotCount)
        {
            error =
                $"Event enemy grid must contain exactly {EventBattleLogicalSlotCount} logical slots. " +
                $"Root='{enemyPlace.name}', Found={slotMap.Count}.";
            slotMap = null;
            return false;
        }

        if (!enemySpawner.ConfigureEventSlotMap(slotMap, out error))
        {
            slotMap = null;
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(slotMap.BuildDebugSummary(combatContext.EventBattle.BattleKey), enemyPlace);
#endif
        return true;
    }

    private void AbortEventBattleSetup(CombatContext combatContext, string reason)
    {
        if (combatContext == null || !combatContext.HasEventBattle)
        {
            Debug.LogError(
                $"[BattleSceneManager] Cannot abort event setup because its CombatContext is missing. Reason={reason}",
                this);
            return;
        }

        CombatEventBattleData eventBattle = combatContext.EventBattle;
        eventBattle.SetNumericResult(HostageScenarioController.InjuredCountResultKey, 0f);
        combatContext.SetCombatResult(CombatResult.Cancelled);

        Debug.LogError(
            $"[BattleSceneManager] Event battle setup cancelled. " +
            $"Battle={eventBattle.BattleKey}, Reason={reason}",
            this);

        if (returnSceneCoroutine == null)
            returnSceneCoroutine = StartCoroutine(AbortEventBattleSetupRoutine());
    }

    private IEnumerator AbortEventBattleSetupRoutine()
    {
        // 정상 PostBattleSequence를 사용하지 않습니다. 초기화 오류에는 보상/결과 UI/커밋이 없어야 합니다.
        yield return null;
        yield return StartCoroutine(TransitionToSceneRoutine());
    }

    /// <summary>
    /// 프로토타입 부트: 씬에 배치된 유닛에 대해 래퍼 Initialize(null) 우선 호출 후,
    /// 래퍼가 없는 경우에만 <see cref="BattleCharactor.Initialize"/>를 호출합니다.
    /// 셀이 연결된 유닛만 플레이어/적 목록에 넣습니다.
    /// </summary>
    private void CollectParticipantsAfterInitialize(List<BattleCharactor> sceneUnits)
    {
        for (int i = 0; i < sceneUnits.Count; i++)
        {
            var battle = sceneUnits[i];
            if (battle == null)
            {
                continue;
            }

            // 스포너가 persistent/CSV 주입으로 이미 초기화한 유닛은 재초기화로 값을 덮어쓰지 않습니다.
            CharactorScript playerWrapper = battle.GetComponent<CharactorScript>();
            EnemyScript enemyWrapper = battle.GetComponent<EnemyScript>();

            if (!battle.IsInitialized)
            {
                // 코어(BattleCharactor)의 필수 상태 초기화(IsPlayer, IsDead 등)는 항상 먼저 수행합니다.
                battle.Initialize();

                // 래퍼가 있으면 코어 기본 초기화 이후 인스펙터/튜닝 스탯을 덮어씁니다.
                if (playerWrapper != null)
                    playerWrapper.Initialize(null);
                else if (enemyWrapper != null)
                    enemyWrapper.Initialize(null);
            }

            if (battle.OccupiedCell == null)
            {
                Debug.LogWarning($"[BattleSceneManager] 셀 미연결 유닛은 참가 제외: {battle.UnitName} ({battle.name})");
                continue;
            }

            if (battle.TeamType == TeamType.Player)
            {
                playerBattleCharactors.Add(battle);
                if (playerBattleCharactor == null)
                {
                    playerBattleCharactor = battle;
                }
            }
            else
            {
                EnemyScript enemyScript = battle.GetComponent<EnemyScript>();
                if (enemyScript == null)
                {
                    Debug.LogWarning($"[BattleSceneManager] Enemy 유닛에 EnemyScript가 없어 AI 초기화를 건너뜁니다: {battle.UnitName} ({battle.name})");
                }
                else
                {
                    enemyScript.EnsureAIReady();
                }

                enemyBattleCharactors.Add(battle);
            }
        }
    }

    /// <summary>
    /// Prototype 전용: GridCell → BattleCharactor 하이어러키에 맞춰 OccupiedCell ↔ OccupyingUnit을 연결합니다.
    /// </summary>
    private void SyncGridOccupancy(List<BattleCharactor> sceneUnits)
    {
        // 1. 기존 점유 정보 해제
        for (int i = 0; i < sceneUnits.Count; i++)
        {
            if (sceneUnits[i] != null)
            {
                sceneUnits[i].ClearOccupiedCell();
            }
        }

        // 3. playerPlace / enemyPlace 하위 GridCell 수집
        // - 씬에서 이미 절대 좌표(Grid_2_0, Grid_3_0 등)를 정의하므로 별도 오프셋 보정을 하지 않습니다.
        var allCells = new List<GridCellRef>();
        CollectCells(playerPlace, allCells);
        CollectCells(enemyPlace, allCells);

        // 4. 각 셀에 대해 자식 BattleCharactor를 찾아 AssignToCell
        for (int i = 0; i < allCells.Count; i++)
        {
            var cell = allCells[i];
            if (cell == null)
            {
                continue;
            }

            BattleCharactor[] found = cell.GetComponentsInChildren<BattleCharactor>(includeInactiveUnits);
            if (found.Length > 1)
            {
                Debug.LogError(
                    $"[BattleSceneManager] GridCell 중복 점유 감지: cell={cell.name}, coords={cell.Coords}, units={found.Length}");
            }

            var unit = found.FirstOrDefault(x => x != null);
            if (unit == null)
            {
                continue;
            }

            unit.AssignToCell(cell);
        }

        for (int i = 0; i < sceneUnits.Count; i++)
        {
            var unit = sceneUnits[i];
            if (unit == null)
            {
                continue;
            }

            if (unit.OccupiedCell == null)
            {
                Debug.LogWarning(
                    $"[BattleSceneManager] GridCell과 연결되지 않은 유닛: {unit.UnitName} ({unit.name})");
            }
        }

        // 씬의 절대 좌표를 기준으로 캐시를 재구성해 좌표 조회 일관성을 보장합니다.
        ASB.Work.BattleGrid.BattleGridManager.Instance?.RebuildCache();
    }

    private void CollectCells(Transform root, List<GridCellRef> buffer)
    {
        if (root == null)
        {
            return;
        }

        var cells = root.GetComponentsInChildren<GridCellRef>(includeInactiveUnits);
        for (int i = 0; i < cells.Length; i++)
        {
            var c = cells[i];
            if (c == null)
            {
                continue;
            }

            if (!buffer.Contains(c))
            {
                // GridCell이 가진 절대 좌표를 그대로 사용합니다.
                buffer.Add(c);
            }
        }
    }
}
