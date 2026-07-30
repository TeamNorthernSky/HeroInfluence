using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

[DisallowMultipleComponent]
public sealed class HostageScenarioController : MonoBehaviour
{
    public const string InjuredCountResultKey = "HostageInjuredCount";
    private static readonly Quaternion FacingPlayerYawOffset = Quaternion.Euler(0f, 180f, 0f);

    private readonly List<HostageBattleActor> hostages = new List<HostageBattleActor>();
    private HostageScenarioConfig config;
    private int lastAdvancedRound;
    private int injuredCount;

    public static HostageScenarioController Active { get; private set; }
    public int InjuredCount => injuredCount;
    public IReadOnlyList<HostageBattleActor> Hostages => hostages;
    public bool IsFullyInitialized { get; private set; }
    public string InitializationError { get; private set; } = string.Empty;

    public static HostageScenarioController Create(
        BattleLogicalSlotMap slotMap,
        BattleScenarioConfig scenario)
    {
        if (scenario == null || !scenario.IsHostageRescue)
            return null;

        if (slotMap == null || slotMap.Root == null)
        {
            Debug.LogError("[HostageScenario] Shared enemy slot map is missing.");
            return null;
        }

        GameObject root = new GameObject("[HostageScenario]");
        root.transform.SetParent(slotMap.Root, false);
        HostageScenarioController controller = root.AddComponent<HostageScenarioController>();
        controller.Initialize(slotMap, scenario.HostageRescue);
        return controller;
    }

    private void Awake()
    {
        if (Active != null && Active != this)
        {
            Debug.LogWarning("[HostageScenario] Duplicate controller was replaced.", this);
        }

        Active = this;
    }

    private void OnDestroy()
    {
        if (Active == this)
            Active = null;
    }

    private void Initialize(BattleLogicalSlotMap slotMap, HostageScenarioConfig nextConfig)
    {
        config = nextConfig;
        hostages.Clear();
        injuredCount = 0;
        lastAdvancedRound = 0;
        IsFullyInitialized = false;
        InitializationError = string.Empty;
        WriteInjuredCount();

        if (slotMap == null || config == null || config.Hostages == null)
        {
            InitializationError = "Hostage configuration or shared slot map is missing.";
            Debug.LogError($"[HostageScenario] {InitializationError}", this);
            return;
        }

        var errors = new List<string>();
        var claimedLogicalSlots = new HashSet<int>();
        int expectedHostages = config.Hostages.Count;
        for (int i = 0; i < config.Hostages.Count; i++)
        {
            HostageSpawnConfig spawn = config.Hostages[i];
            if (spawn == null)
            {
                errors.Add($"Hostage config at index {i} is null.");
                continue;
            }

            if (!claimedLogicalSlots.Add(spawn.Slot))
            {
                errors.Add($"Duplicate hostage logical slot. id={spawn.HostageId}, slot={spawn.Slot}");
                continue;
            }

            if (!slotMap.TryResolve(spawn.Slot, out BattleLogicalSlotMap.Slot resolvedSlot) ||
                resolvedSlot.Cell == null)
            {
                errors.Add($"Hostage grid was not found. id={spawn.HostageId}, slot={spawn.Slot}");
                continue;
            }

            GridCellRef cell = resolvedSlot.Cell;
            BattleCharactor occupyingUnit = cell.GetComponentInChildren<BattleCharactor>(true);
            HostageBattleActor occupyingHostage = cell.GetComponentInChildren<HostageBattleActor>(true);
            if (occupyingUnit != null || occupyingHostage != null)
            {
                errors.Add(
                    $"Hostage grid is already occupied. id={spawn.HostageId}, " +
                    $"slot={spawn.Slot}, grid={resolvedSlot.GridName}({resolvedSlot.GridNumber})");
                continue;
            }

            HostageBattleActor actor = SpawnHostage(spawn, cell);
            if (actor == null)
            {
                errors.Add($"Hostage spawn failed. id={spawn.HostageId}, slot={spawn.Slot}");
                continue;
            }

            actor.Injured += HandleHostageInjured;
            hostages.Add(actor);
            Debug.Log(
                $"[HostageScenario] Hostage spawned. Id={spawn.HostageId}, " +
                $"LogicalSlot={spawn.Slot}, ResolvedGridNumber={resolvedSlot.GridNumber}, " +
                $"ResolvedGridName={resolvedSlot.GridName}",
                actor);
        }

        IsFullyInitialized = errors.Count == 0 && hostages.Count == expectedHostages;
        if (!IsFullyInitialized)
        {
            InitializationError = string.Join(" | ", errors);
            if (string.IsNullOrEmpty(InitializationError))
            {
                InitializationError =
                    $"Hostage count mismatch. Expected={expectedHostages}, Spawned={hostages.Count}";
            }

            Debug.LogError(
                $"[HostageScenario] Initialization failed. {InitializationError}",
                this);
            return;
        }

        Debug.Log($"[HostageScenario] Initialized. hostages={hostages.Count}", this);
    }

    public void AdvanceRound(int roundIndex)
    {
        if (config == null || roundIndex <= lastAdvancedRound)
            return;

        lastAdvancedRound = roundIndex;
        for (int i = 0; i < hostages.Count; i++)
        {
            HostageBattleActor hostage = hostages[i];
            if (hostage != null)
                hostage.AdvanceRound(config.FullHealthAggroGain, config.DamagedAggroGain);
        }
    }

    public bool TryPrepareThreat(BattleCharactor enemy, out HostageBattleActor target)
    {
        target = null;
        if (config == null || enemy == null || enemy.IsDead)
            return false;

        EnemyScript enemyScript = enemy.GetComponent<EnemyScript>();
        string unitKey = enemyScript != null && enemyScript.Data != null
            ? enemyScript.Data.Index
            : enemy.UnitId;
        if (!config.CanEnemyThreaten(unitKey))
            return false;

        float totalAggro = 0f;
        for (int i = 0; i < hostages.Count; i++)
        {
            HostageBattleActor candidate = hostages[i];
            if (candidate != null && candidate.IsSafe)
                totalAggro += Mathf.Max(0f, candidate.HostageAggro);
        }

        if (totalAggro <= 0f)
            return false;

        float chance = Mathf.Clamp(
            totalAggro * Mathf.Max(0f, config.ThreatChancePerTotalAggro),
            0f,
            Mathf.Clamp01(config.MaximumThreatChance));
        if (Random.value > chance)
            return false;

        for (int i = 0; i < hostages.Count; i++)
        {
            HostageBattleActor candidate = hostages[i];
            if (candidate == null || !candidate.IsSafe)
                continue;

            if (target == null ||
                candidate.HostageAggro > target.HostageAggro ||
                (Mathf.Approximately(candidate.HostageAggro, target.HostageAggro) && candidate.Slot < target.Slot))
            {
                target = candidate;
            }
        }

        return target != null;
    }

    public IEnumerator ExecuteThreat(BattleCharactor enemy, HostageBattleActor target)
    {
        if (config == null || enemy == null || target == null || !target.IsSafe)
            yield break;

        Quaternion originalRotation = enemy.transform.rotation;
        try
        {
            Vector3 direction = target.transform.position - enemy.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                enemy.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            yield return WaitScaledSeconds(config.ThreatWindupSeconds);

            if (enemy == null || enemy.IsDead || target == null || !target.IsSafe)
                yield break;

            Debug.DrawLine(enemy.transform.position, target.transform.position, Color.red, 0.75f);
            target.ApplyThreatDamage(config.ThreatDamage, config.ThreatAggroReduction);

            yield return WaitScaledSeconds(config.ThreatRecoverySeconds);
        }
        finally
        {
            if (enemy != null)
                enemy.transform.rotation = originalRotation;
        }
    }

    public void FlushResult()
    {
        WriteInjuredCount();
    }

    private void HandleHostageInjured(HostageBattleActor actor)
    {
        if (actor == null)
            return;

        injuredCount++;
        WriteInjuredCount();
        Debug.Log(
            $"[HostageScenario] Hostage injured. id={actor.HostageId}, count={injuredCount}",
            actor);
    }

    private void WriteInjuredCount()
    {
        CombatContext context = CombatContext.Instance;
        if (context == null || context.EventBattle == null)
            return;

        context.EventBattle.SetNumericResult(InjuredCountResultKey, injuredCount);
    }

    private static IEnumerator WaitScaledSeconds(float seconds)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, seconds);
        while (elapsed < duration)
        {
            float battleSpeed = BattleManager.Instance != null
                ? Mathf.Max(0f, BattleManager.Instance.CurrentBattleSpeed)
                : 1f;
            elapsed += Time.deltaTime * battleSpeed;
            yield return null;
        }
    }

    private static HostageBattleActor SpawnHostage(HostageSpawnConfig spawn, GridCellRef cell)
    {
        GameObject instance = null;
        if (!string.IsNullOrWhiteSpace(spawn.PrefabResourcePath))
        {
            GameObject prefab = Resources.Load<GameObject>(spawn.PrefabResourcePath.Trim());
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[HostageScenario] Hostage prefab was not found. path={spawn.PrefabResourcePath}, id={spawn.HostageId}");
            }
            else if (prefab.GetComponentInChildren<BattleCharactor>(true) != null)
            {
                Debug.LogWarning(
                    $"[HostageScenario] Hostage prefab must be visual-only and cannot contain BattleCharactor. path={spawn.PrefabResourcePath}");
            }
            else
            {
                Quaternion facingPlayerRotation = cell.transform.rotation * FacingPlayerYawOffset;
                instance = Object.Instantiate(prefab, cell.transform.position, facingPlayerRotation, cell.transform);
            }
        }

        if (instance == null)
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            instance.transform.SetParent(cell.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            Collider placeholderCollider = instance.GetComponent<Collider>();
            if (placeholderCollider != null)
                placeholderCollider.enabled = false;
        }

        instance.name = $"Hostage_{spawn.HostageId}_{spawn.Slot}";
        InitializeVisualAnimators(instance);
        HostageBattleActor actor = instance.GetComponent<HostageBattleActor>();
        if (actor == null)
            actor = instance.AddComponent<HostageBattleActor>();
        actor.Initialize(spawn);
        return actor;
    }

    private static void InitializeVisualAnimators(GameObject instance)
    {
        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
        {
            if (animator.runtimeAnimatorController == null || animator.avatar == null)
            {
                Debug.LogWarning($"[HostageScenario] Hostage animator is missing its controller or avatar. object={instance.name}");
                continue;
            }

            animator.enabled = true;
            animator.Rebind();
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
        }
    }

}
