using com.IvanMurzak.Unity.MCP.Runtime.Extensions;
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
    [Header("Prototype Boot")]
    [Tooltip("Prototype 전용: 씬에 배치된 BattleCharactor를 그대로 초기화해 전투를 시작합니다.")]
    [SerializeField] private bool includeInactiveUnits = true;
    [SerializeField] private Transform playerPlace;
    [SerializeField] private Transform enemyPlace;
    [SerializeField] private BattleFlowManager battleFlowManager;
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private EnemySpawner enemySpawner;

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
        yield return WaitDeadUnitDeathAnimations();

        // 1. 보상 계산 (Repository/JSON 변경 없음)
        BattleRewardPlan plan = BattleResultPersistenceHandler.BuildBattleRewardPlan(
            playerBattleCharactors, enemyBattleCharactors, result);

        // 2. 레벨업 UI (TODO: 레벨업 UI가 생기면 여기서 yield return)
        // if (plan.UnitPreviews.Exists(u => u.HasLevelUp))
        //     yield return battleUIManager.ShowLevelUpSequence(plan);

        // 3. CombatContext 결과 설정
        CombatContext combatContext = CombatContext.Instance;
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
        BattleResultPanel resultPanel = battleUIManager?.ShowBattleResultUI(result, plan);

        // 5. Accept 버튼 대기
        if (resultPanel != null)
        {
            bool accepted = false;
            resultPanel.OnAccepted += () => accepted = true;
            yield return new WaitUntil(() => accepted);
        }

        // 6. 저장 (스킬 선택 결과 포함)
        var skillResults = resultPanel?.GetSkillResults() ?? new System.Collections.Generic.List<SkillSelectionResult>();
        BattleResultPersistenceHandler.CommitBattleRewardPlan(
            plan, playerBattleCharactors, enemyBattleCharactors, result, skillResults);

        // 8. 씬 전환
        // [JC 260514] returnSceneName 빈 값이라도 GameSceneManager.Instance.ExplorationScene fallback이 있으면 통과.
        if (string.IsNullOrWhiteSpace(returnSceneName) && GameSceneManager.Instance == null)
        {
            Debug.LogWarning("[BattleSceneManager] returnSceneName + GameSceneManager.Instance 모두 없음 — 씬 전환을 건너뜁니다.");
            returnSceneCoroutine = null;
            yield break;
        }

        yield return StartCoroutine(TransitionToSceneRoutine());
    }

    private IEnumerator WaitDeadUnitDeathAnimations()
    {
        const float maxWait = 1.2f;
        const float endThreshold = 0.95f;

        float elapsed = 0f;
        HashSet<BattleCharactor> observedDeathAnimations = new HashSet<BattleCharactor>();
        while (elapsed < maxWait)
        {
            bool waitingForDeathAnimation = false;

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

                if (anim.IsInState("Die"))
                {
                    observedDeathAnimations.Add(unit);
                    if (!anim.IsStateNearEnd("Die", endThreshold))
                    {
                        waitingForDeathAnimation = true;
                        break;
                    }
                }
                else if (!observedDeathAnimations.Contains(unit))
                {
                    waitingForDeathAnimation = true;
                    break;
                }
            }

            if (!waitingForDeathAnimation)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator TransitionToSceneRoutine()
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
        if (!string.IsNullOrWhiteSpace(returnSceneName))
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
        if (battleFlowManager == null)
        {
            Debug.LogError("[BattleSceneManager] battleFlowManager가 할당되지 않았습니다.");
            return;
        }

        playerBattleCharactor = null;
        playerBattleCharactors.Clear();
        enemyBattleCharactors.Clear();

        playerSpawner?.SetSpawnOnStart(false);
        enemySpawner?.SetSpawnOnStart(false);

        playerSpawner?.ManualSpawn();
        enemySpawner?.ManualSpawn();

        var inactiveMode = includeInactiveUnits ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        var sceneUnits = FindObjectsByType<BattleCharactor>(inactiveMode, FindObjectsSortMode.None).ToList();
        if (sceneUnits.Count == 0)
        {
            Debug.LogWarning("[BattleSceneManager] 씬에서 BattleCharactor를 찾지 못했습니다.");
            return;
        }

        // 그리드 ↔ 유닛 점유 동기화 후, 각 BattleCharactor.Initialize()로 스탯·스킬·무기 확정
        SyncGridOccupancy(sceneUnits);
        CollectParticipantsAfterInitialize(sceneUnits);

        var allUnits = new List<BattleCharactor>(playerBattleCharactors.Count + enemyBattleCharactors.Count);
        allUnits.AddRange(playerBattleCharactors);
        allUnits.AddRange(enemyBattleCharactors);

        // BattleFlowManager.Initialize → RebuildRuntimeLookup: 스포너/CollectParticipantsAfterInitialize 이후이며
        // 각 인스턴스의 BattleCharactor.Awake에서 런타임 키가 이미 할당된 상태입니다.
        battleFlowManager.Initialize(allUnits);
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
        ASB.Work.BattleGrid.GridManager.Instance?.RebuildCache();
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
