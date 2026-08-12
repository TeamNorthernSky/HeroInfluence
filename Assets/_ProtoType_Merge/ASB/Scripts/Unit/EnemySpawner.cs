using System;
using System.Collections.Generic;
using UnityEngine;
using ASB.Work.BattleGrid;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// EnemyPlace 최상위에 부착. Grid/Grid_n 월드 위치 참조, 소환 유닛은 Units 자식.
/// 프리팹은 Resources/prefab/BattlePrefab/EnemyUnit 아래에서 Unit_{UnitType}_{Index} 규칙으로 로드합니다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    private static readonly Quaternion FacingPlayerYawOffset = Quaternion.Euler(0f, 180f, 0f);

    [Serializable]
    public struct SpawnRequest
    {
        public string unitId;
        public int gridNumber;
    }

    [Header("Dependencies")]
    // enemyManager 제거 — DHCsvTemplateCatalog.Instance 로 대체

    [Header("Inspector / Battle debug spawn")]
    public List<SpawnRequest> debugSpawnRequests = new List<SpawnRequest>();
    [SerializeField] private bool spawnOnStart;

    private Transform unitParent;
    private readonly Dictionary<int, Vector3> gridSlots = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Quaternion> gridRotations = new Dictionary<int, Quaternion>();
    private readonly Dictionary<int, GameObject> spawnedByGrid = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GridCellRef> gridCellsByNumber = new Dictionary<int, GridCellRef>();
    private BattleLogicalSlotMap eventSlotMap;
    private bool hierarchyReady;

    public Transform GridRoot => transform;

    private void Awake()
    {
        gridSlots.Clear();
        gridRotations.Clear();
        gridCellsByNumber.Clear();
        eventSlotMap = null;
        unitParent = null;
        hierarchyReady = false;

        unitParent = transform.Find("UnitContainer");
        if (unitParent == null)
        {
            var created = new GameObject("UnitContainer");
            created.transform.SetParent(transform, false);
            unitParent = created.transform;
        }
        if (unitParent == null)
        {
            Debug.LogError($"[{gameObject.name}] 'Units' 자식 오브젝트를 찾을 수 없습니다.");
            return;
        }

        GridCellRef[] cells = GetComponentsInChildren<GridCellRef>(true);
        for (int i = 0; i < cells.Length; i++)
        {
            GridCellRef cell = cells[i];
            if (cell == null) continue;

            if (!TryResolveGridNumber(cell.transform, out int gridNumber)) continue;
            if (gridSlots.ContainsKey(gridNumber)) continue;

            gridSlots[gridNumber] = cell.transform.position;
            gridRotations[gridNumber] = cell.transform.rotation;
            gridCellsByNumber[gridNumber] = cell;
        }

        hierarchyReady = true;
    }

    private void Start()
    {
        // 스포너는 BattleSceneManager가 수동 호출(ManualSpawn)로 실행을 제어합니다.
    }

    public void SetSpawnOnStart(bool enabled)
    {
        spawnOnStart = enabled;
    }

    public bool ConfigureEventSlotMap(BattleLogicalSlotMap slotMap, out string error)
    {
        eventSlotMap = null;
        error = string.Empty;

        if (slotMap == null)
        {
            error = "Event slot map is null.";
            return false;
        }

        if (slotMap.Root != transform)
        {
            error =
                $"Event slot map root mismatch. mapRoot='{slotMap.Root?.name ?? "<null>"}', " +
                $"spawnerRoot='{transform.name}'.";
            return false;
        }

        eventSlotMap = slotMap;
        return true;
    }

    public bool ManualSpawn()
    {
        return SpawnFromRepositoryOrFallback();
    }

    /// <summary>
    /// 이미 빌드된 플랜으로 스폰(§5). 빌드는 BattleSceneManager가 수행하고 스포너는 스폰만 담당한다.
    /// 인질 제외에 쓰는 hostageConfig는 <b>오직 plan.Scenario.HostageRescue에서만</b> 도출한다(별도 인자 없음, §7).
    /// 이벤트 전투는 사전 주입된 eventSlotMap을, 없으면 스포너 그리드로 만든 슬롯맵을 사용한다.
    /// </summary>
    public bool SpawnFromPreparedPlan(EnemySpawnPlan plan)
    {
        if (!hierarchyReady)
            Awake();

        if (plan == null || plan.Count == 0)
        {
            Debug.LogError("[EnemySpawner] SpawnFromPreparedPlan: plan is null or empty.", this);
            return false;
        }

        BattleLogicalSlotMap slotMap = eventSlotMap;
        if (slotMap == null && !BattleLogicalSlotMap.TryCreate(transform, out slotMap, out string slotError))
        {
            Debug.LogError($"[EnemySpawner] SpawnFromPreparedPlan: slot map build failed. {slotError}", this);
            return false;
        }

        HostageScenarioConfig hostageConfig = plan.Scenario != null && plan.Scenario.IsHostageRescue
            ? plan.Scenario.HostageRescue
            : null;

        return SpawnFromPlan(plan, slotMap, hostageConfig, "prepared-plan");
    }

    public bool SpawnFromRepositoryOrFallback()
    {
        if (!hierarchyReady)
            Awake();

        CombatContext combatContext = CombatContext.Instance;
        if (combatContext != null && combatContext.HasEventBattle)
            return SpawnFromEventBattle(combatContext.EventBattle);

        // 그룹키 기반 스폰 우선(일반 전투). 그룹키가 있으면 실패해도 폴백하지 않고 진입 실패로 둔다
        // (잘못된 적 구성으로 전투가 시작되는 것을 막는다).
        string enemyGroupKey = combatContext != null && combatContext.CombatEnemy != null
            ? combatContext.CombatEnemy.EnemyGroupKey
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(enemyGroupKey))
            return SpawnFromEnemyGroupPlan(enemyGroupKey, combatContext.EnemyLevel);

        // 명시적 개발용: 인스펙터 debugSpawnRequests가 설정된 경우에만 디버그 스폰(자동 폴백 아님).
        if (debugSpawnRequests != null && debugSpawnRequests.Count > 0)
        {
            Debug.LogWarning(
                "[EnemySpawner] EnemyGroupKey/EventBattle 없음 — 인스펙터 debugSpawnRequests로 개발용 스폰.",
                this);
            DebugSpawn();
            return false;
        }

        // 그룹키도 이벤트도 없으면 잘못된 진입 → 조용한 폴백 없이 실패.
        // (PersistentEnemyRepository 자동 폴백 제거: 스폰 경로의 영속 의존을 끊는다.)
        Debug.LogError(
            "[EnemySpawner] CombatContext에 EnemyGroupKey/EventBattle이 없어 적을 스폰할 수 없습니다.",
            this);
        return false;
    }

    private GameObject FindPrefab(EnemyData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.Index))
        {
            Debug.LogError("[EnemySpawner] Index가 비어 있거나 EnemyData가 없습니다.");
            return null;
        }

        string trimmedIndex = data.Index.Trim();
        string suffix = $"_{trimmedIndex}";

        GameObject[] all = Resources.LoadAll<GameObject>("prefab/BattlePrefab/EnemyUnit");
        foreach (GameObject prefab in all)
        {
            if (prefab.name.EndsWith(suffix))
                return prefab;
        }

        Debug.LogError($"[EnemySpawner] Prefab not found. Index={trimmedIndex}");
        return null;
    }

    public GameObject SpawnUnit(string enemyId, int gridNumber)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            Debug.LogError($"[EnemySpawner] enemyId가 비어 있습니다. ({gameObject.name})");
            return null;
        }

        if (!hierarchyReady || unitParent == null)
        {
            Debug.LogError($"[EnemySpawner] Grid/Units 계층이 준비되지 않았습니다. ({gameObject.name})");
            return null;
        }

        if (!gridSlots.TryGetValue(gridNumber, out Vector3 worldPos) ||
            !gridRotations.TryGetValue(gridNumber, out Quaternion worldRot))
        {
            Debug.LogError(
                $"[EnemySpawner] {gridNumber}번 그리드를 찾지 못했습니다. (Grid/Grid_{gridNumber}, {gameObject.name})");
            return null;
        }

        if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef resolvedCell) || resolvedCell == null)
        {
            Debug.LogError($"[EnemySpawner] GridCell을 찾지 못했습니다. grid={gridNumber} ({gameObject.name})");
            return null;
        }

        ClearGrid(gridNumber);

        DHCsvTemplateCatalog.Instance.TryGetEnemyTemplate(enemyId, out EnemyData data);
        if (data == null)
        {
            Debug.LogError(
                $"[EnemySpawner] DHCsvTemplateCatalog에서 EnemyData를 찾지 못했습니다. enemyId='{enemyId}' — 스폰을 중단합니다.");
            return null;
        }

        GameObject prefab = FindPrefab(data);
        if (prefab == null)
        {
            return null;
        }

        // BattleSceneManager.SyncGridOccupancy가 cell 하위에서 유닛을 탐색하므로, 반드시 GridCell 아래에 붙입니다.
        Quaternion facingPlayerRot = ApplyFacingPlayerRotation(worldRot);
        var go = Instantiate(prefab, worldPos, facingPlayerRot, resolvedCell.transform);
        go.name = $"Enemy_{data.Index}_{go.GetInstanceID()}";

        // 적 인스턴스에서는 IUnitIdentifier를 EnemyScript만 담당하도록 CharactorScript 제거(클릭 식별 모호 방지).
        // 같은 프레임에 RebuildRuntimeLookup이 돌 수 있어 DestroyImmediate로 즉시 제거한다.
        foreach (var legacy in go.GetComponentsInChildren<CharactorScript>(true))
        {
            DestroyImmediate(legacy);
        }

        var enemyScript = go.GetComponent<EnemyScript>();
        if (enemyScript == null)
        {
            enemyScript = go.AddComponent<EnemyScript>();
        }

        // 플레이어 스폰(PlayerSpawner → CharactorScript.Initialize)과 동일하게 루트에 UnitData 주입.
        enemyScript.Initialize(data);

        var battle = go.GetComponent<BattleCharactor>();
        if (battle == null)
        {
            battle = go.AddComponent<BattleCharactor>();
        }

        battle.AssignToCell(resolvedCell);
        resolvedCell.SetOccupyingUnit(battle);

        // 디버그: battleById는 BattleCharactor.UnitId 기준. 래퍼 UnitID는 별도 문자열입니다.
        Debug.Log(
            $"[EnemySpawner] 스폰 직후 BattleCharactor.UnitId='{battle.UnitId}' EnemyScript.UnitID='{enemyScript.UnitID}' (enemyId={enemyId}, grid={gridNumber})");

        spawnedByGrid[gridNumber] = go;
        Debug.Log(
            $"[EnemySpawner] 적 스폰 완료: id={enemyId}, grid={gridNumber}, Index={data.Index}, place={gameObject.name}");
        return go;
    }

    /// <summary>
    /// 점유 시 실패하는 안전 소환. 보스 런타임 소환용(구현지시서: 보스유닛_소환_창구스킬_페이즈AI §1·§4).
    /// <see cref="SpawnUnit"/>과 달리 <c>ClearGrid</c>로 기존 유닛을 파괴하지 않는다 — 대상 셀에
    /// BattleCharactor/인질이 이미 있으면 스폰하지 않고 false를 반환한다(SpawnEventEnemy 점유 검사 패턴 재사용).
    /// </summary>
    public bool TrySpawnUnitIfEmpty(string enemyId, int gridNumber, out GameObject spawned)
    {
        spawned = null;

        if (string.IsNullOrWhiteSpace(enemyId))
        {
            Debug.LogError($"[EnemySpawner] TrySpawnUnitIfEmpty: enemyId가 비어 있습니다. ({gameObject.name})");
            return false;
        }

        if (!hierarchyReady || unitParent == null)
        {
            Debug.LogError($"[EnemySpawner] TrySpawnUnitIfEmpty: Grid/Units 계층이 준비되지 않았습니다. ({gameObject.name})");
            return false;
        }

        if (!gridSlots.TryGetValue(gridNumber, out Vector3 worldPos) ||
            !gridRotations.TryGetValue(gridNumber, out Quaternion worldRot))
        {
            Debug.LogError($"[EnemySpawner] TrySpawnUnitIfEmpty: {gridNumber}번 그리드를 찾지 못했습니다. ({gameObject.name})");
            return false;
        }

        if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef resolvedCell) || resolvedCell == null)
        {
            Debug.LogError($"[EnemySpawner] TrySpawnUnitIfEmpty: GridCell을 찾지 못했습니다. grid={gridNumber} ({gameObject.name})");
            return false;
        }

        // 점유 검사(SpawnEventEnemy 패턴) — 점유 시 기존 유닛을 보존하고 조용히 실패.
        BattleCharactor existingUnit = resolvedCell.GetComponentInChildren<BattleCharactor>(true);
        HostageBattleActor existingHostage = resolvedCell.GetComponentInChildren<HostageBattleActor>(true);
        if (existingUnit != null || existingHostage != null)
        {
            return false;
        }

        DHCsvTemplateCatalog.Instance.TryGetEnemyTemplate(enemyId, out EnemyData data);
        if (data == null)
        {
            Debug.LogError($"[EnemySpawner] TrySpawnUnitIfEmpty: EnemyData를 찾지 못했습니다. enemyId='{enemyId}'");
            return false;
        }

        GameObject prefab = FindPrefab(data);
        if (prefab == null)
        {
            return false;
        }

        Quaternion facingPlayerRot = ApplyFacingPlayerRotation(worldRot);
        var go = Instantiate(prefab, worldPos, facingPlayerRot, resolvedCell.transform);
        go.name = $"Enemy_{data.Index}_{go.GetInstanceID()}";

        foreach (var legacy in go.GetComponentsInChildren<CharactorScript>(true))
        {
            DestroyImmediate(legacy);
        }

        var enemyScript = go.GetComponent<EnemyScript>();
        if (enemyScript == null)
        {
            enemyScript = go.AddComponent<EnemyScript>();
        }
        enemyScript.Initialize(data);

        var battle = go.GetComponent<BattleCharactor>();
        if (battle == null)
        {
            battle = go.AddComponent<BattleCharactor>();
        }

        battle.AssignToCell(resolvedCell);
        resolvedCell.SetOccupyingUnit(battle);

        spawnedByGrid[gridNumber] = go;
        spawned = go;
        Debug.Log(
            $"[EnemySpawner] 안전 소환 완료: id={enemyId}, grid={gridNumber}, Index={data.Index}, place={gameObject.name}");
        return true;
    }

    // 이벤트 전투 사전빌드(EnemyUnits) 구경로. [TEMP:EVENTBUILD] 현재 미사용 —
    // 전투씬은 키-빌드(SpawnFromPreparedPlan)로만 스폰하고 폴백하지 않으므로, ManualSpawn 이벤트 분기(→여기)는 도달하지 않는다.
    // DH 사전빌드는 A/B 대조·안전망 데이터로 남겨둔 상태이며, §6에서 이 경로와 함께 제거한다.
    private bool SpawnFromEventBattle(CombatEventBattleData eventBattle)
    {
        if (eventBattle == null)
            return false;

        if (eventSlotMap == null)
        {
            Debug.LogError(
                $"[EnemySpawner] Event slot map is not configured. Battle={eventBattle.BattleKey}",
                this);
            return false;
        }

        if (!EnemySpawnPlanBuilder.TryBuildFromEventBattle(eventBattle, out EnemySpawnPlan plan, out string error))
        {
            Debug.LogError(
                $"[EnemySpawner] Event battle spawn plan build failed. Battle={eventBattle.BattleKey}, error={error}",
                this);
            return false;
        }

        HostageScenarioConfig hostageConfig = eventBattle.Scenario != null && eventBattle.Scenario.IsHostageRescue
            ? eventBattle.Scenario.HostageRescue
            : null;

        return SpawnFromPlan(plan, eventSlotMap, hostageConfig, $"event:{eventBattle.BattleKey}");
    }

    private GameObject SpawnEventEnemy(string battleKey, CombatEventBattleUnitData unit)
    {
        if (unit == null || unit.Slot <= 0)
            return null;

        if (eventSlotMap == null ||
            !eventSlotMap.TryResolve(unit.Slot, out BattleLogicalSlotMap.Slot resolvedSlot) ||
            resolvedSlot.Cell == null)
        {
            Debug.LogError(
                $"[EnemySpawner] Event enemy grid was not found. " +
                $"Battle={battleKey}, Unit={unit.UnitKey}, LogicalSlot={unit.Slot}",
                this);
            return null;
        }

        GridCellRef cell = resolvedSlot.Cell;
        BattleCharactor existingUnit = cell.GetComponentInChildren<BattleCharactor>(true);
        HostageBattleActor existingHostage = cell.GetComponentInChildren<HostageBattleActor>(true);
        if (existingUnit != null || existingHostage != null)
        {
            Debug.LogError(
                $"[EnemySpawner] Event grid is already occupied. " +
                $"Battle={battleKey}, Unit={unit.UnitKey}, LogicalSlot={unit.Slot}, " +
                $"ResolvedGridNumber={resolvedSlot.GridNumber}, ResolvedGridName={resolvedSlot.GridName}",
                this);
            return null;
        }

        GameObject prefab = FindEventEnemyPrefab(unit);
        if (prefab == null)
            return null;

        ClearGrid(resolvedSlot.GridNumber);
        Quaternion facingPlayerRot = ApplyFacingPlayerRotation(resolvedSlot.WorldRotation);
        GameObject go = Instantiate(
            prefab,
            resolvedSlot.WorldPosition,
            facingPlayerRot,
            cell.transform);
        go.name = $"EventEnemy_{unit.UnitKey}_{go.GetInstanceID()}";

        foreach (CharactorScript legacy in go.GetComponentsInChildren<CharactorScript>(true))
            Destroy(legacy);

        BattleCharactor battle = go.GetComponent<BattleCharactor>();
        if (battle == null)
            battle = go.AddComponent<BattleCharactor>();

        EnemyScript enemyScript = go.GetComponent<EnemyScript>();
        if (enemyScript == null)
            enemyScript = go.AddComponent<EnemyScript>();

        EnemyData runtimeData = BuildEventEnemyData(unit);
        enemyScript.Initialize(runtimeData);

        battle.availableSkills.Clear();
        if (unit.Skills != null)
        {
            for (int i = 0; i < unit.Skills.Count; i++)
            {
                if (unit.Skills[i] != null)
                    battle.availableSkills.Add(unit.Skills[i]);
            }
        }

        if (battle.availableSkills.Count > 0)
        {
            battle.SetClassSkillIndex(battle.availableSkills[0].skillIndex);
            battle.ResolveSelectedSkill(false);
        }

        battle.AssignToCell(cell);
        cell.SetOccupyingUnit(battle);
        spawnedByGrid[resolvedSlot.GridNumber] = go;

        Debug.Log(
            $"[EnemySpawner] Event enemy spawned. " +
            $"Battle={battleKey}, Unit={unit.UnitKey}, LogicalSlot={unit.Slot}, " +
            $"ResolvedGridNumber={resolvedSlot.GridNumber}, ResolvedGridName={resolvedSlot.GridName}, " +
            $"Skills={battle.availableSkills.Count}",
            go);
        return go;
    }

    private static EnemyData BuildEventEnemyData(CombatEventBattleUnitData unit)
    {
        return new EnemyData
        {
            Index = BattleScenarioUnitKey.Normalize(unit.UnitKey),
            UnitType = unit.EnemyConcept,
            Name = unit.EnemyName,
            baseStats = new StatBlock(
                hp: Mathf.Max(1, unit.MaxHp),
                atk: Mathf.Max(0, unit.Atk),
                def: Mathf.Max(0, unit.Def),
                luck: 0f,
                speed: Mathf.Max(0, unit.Speed),
                criticalRate: Mathf.Max(0f, unit.CriticalRate),
                critMultiplier: 1.5f,
                counterRate: Mathf.Max(0f, unit.CounterRate),
                avoidRate: Mathf.Max(0f, unit.ReduceRate)),
            levelupStats = new StatBlock(0f, 0f, 0f, 0f, 0f),
            IsEnemyRow = true,
            UnitAI = unit.UnitAI,
            ExperiencePoint = Mathf.Max(0, unit.ExperiencePoint)
        };
    }

    private static GameObject FindEventEnemyPrefab(CombatEventBattleUnitData unit)
    {
        string resourcePath = unit != null ? unit.PrefabResourcePath : string.Empty;
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            string normalized = unit != null ? BattleScenarioUnitKey.Normalize(unit.UnitKey) : string.Empty;
            resourcePath = normalized == "20007"
                ? "prefab/BattlePrefab/EnemyUnit/Unit_AdvancedMonster_20003"
                : normalized == "20006"
                    ? "prefab/BattlePrefab/EnemyUnit/Unit_MiddleMonster_20002"
                    : "prefab/BattlePrefab/EnemyUnit/Unit_LowerMonster_20001";
        }

        GameObject prefab = Resources.Load<GameObject>(resourcePath.Trim());
        if (prefab == null)
        {
            Debug.LogError(
                $"[EnemySpawner] Event enemy prefab was not found. unit={unit?.UnitKey}, path={resourcePath}");
        }

        return prefab;
    }

    // 일반 전투 그룹키 스폰: 공용 빌더로 EnemySpawnPlan을 만들고 원자적으로 스폰한다.
    private bool SpawnFromEnemyGroupPlan(string enemyGroupKey, int enemyLevel)
    {
        if (!EnemySpawnPlanBuilder.TryBuildFromEnemyGroup(enemyGroupKey, enemyLevel, out EnemySpawnPlan plan, out string error))
        {
            Debug.LogError(
                $"[EnemySpawner] Enemy group spawn plan build failed. groupKey='{enemyGroupKey}', level={enemyLevel}, error={error}",
                this);
            return false; // 폴백 금지 — 진입 실패
        }

        // 일반 전투는 스포너 그리드로 논리 슬롯맵을 만든다(이벤트는 미리 주입된 eventSlotMap 사용).
        if (!BattleLogicalSlotMap.TryCreate(transform, out BattleLogicalSlotMap slotMap, out string slotError))
        {
            Debug.LogError($"[EnemySpawner] Slot map build failed. groupKey='{enemyGroupKey}', {slotError}", this);
            return false;
        }

        return SpawnFromPlan(plan, slotMap, null, $"group:{enemyGroupKey}");
    }

    // 원자적 2단계 스폰: (1) 슬롯·프리팹까지 전체 검증 → (2) 전원 성공한 경우에만 Instantiate.
    // 하나라도 실패하면 부분 스폰 없이 false를 반환한다.
    // slotMap: 일반=스포너 그리드로 생성, 이벤트=미리 주입된 eventSlotMap.
    // hostageConfig: 이벤트 인질 시나리오면 인질 원본 유닛을 전투 적 스폰에서 제외(null=제외 없음).
    private bool SpawnFromPlan(
        EnemySpawnPlan plan,
        BattleLogicalSlotMap slotMap,
        HostageScenarioConfig hostageConfig,
        string contextLabel)
    {
        if (!hierarchyReady)
            Awake();

        if (plan == null || plan.Count == 0)
        {
            Debug.LogError($"[EnemySpawner] Spawn plan is empty. context={contextLabel}", this);
            return false;
        }

        if (!hierarchyReady || unitParent == null)
        {
            Debug.LogError($"[EnemySpawner] Grid/Units 계층이 준비되지 않았습니다. context={contextLabel}", this);
            return false;
        }

        if (slotMap == null)
        {
            Debug.LogError($"[EnemySpawner] Slot map is not available. context={contextLabel}", this);
            return false;
        }

        // 인질 유닛은 별도 시스템(HostageScenarioController)이 스폰하므로 전투 적 목록에서 제외.
        var combatEntries = new List<EnemySpawnEntry>(plan.Count);
        for (int i = 0; i < plan.Entries.Count; i++)
        {
            EnemySpawnEntry entry = plan.Entries[i];
            if (entry == null || entry.Data == null)
            {
                Debug.LogError($"[EnemySpawner] Spawn entry is invalid. context={contextLabel}, index={i}", this);
                return false;
            }

            if (hostageConfig != null &&
                !string.IsNullOrEmpty(entry.SourceUnitKey) &&
                hostageConfig.ContainsHostageUnit(entry.SourceUnitKey))
            {
                continue;
            }

            combatEntries.Add(entry);
        }

        if (combatEntries.Count == 0)
        {
            Debug.LogError($"[EnemySpawner] Spawn plan has no combat enemies. context={contextLabel}", this);
            return false;
        }

        int count = combatEntries.Count;
        var resolvedSlots = new BattleLogicalSlotMap.Slot[count];
        var resolvedPrefabs = new GameObject[count];
        var claimedSlots = new HashSet<int>();

        // Phase A: 전체 검증. 실패 시 Instantiate 0회.
        for (int i = 0; i < count; i++)
        {
            EnemySpawnEntry entry = combatEntries[i];

            if (!claimedSlots.Add(entry.CombatSlot))
            {
                Debug.LogError($"[EnemySpawner] Duplicate CombatSlot. context={contextLabel}, slot={entry.CombatSlot}", this);
                return false;
            }

            if (!slotMap.TryResolve(entry.CombatSlot, out BattleLogicalSlotMap.Slot slot) || slot.Cell == null)
            {
                Debug.LogError($"[EnemySpawner] CombatSlot could not be resolved. context={contextLabel}, slot={entry.CombatSlot}", this);
                return false;
            }

            GameObject prefab = ResolvePlanPrefab(entry);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[EnemySpawner] Prefab not found. context={contextLabel}, prefabKey='{entry.PrefabKey}', index='{entry.Data.Index}'",
                    this);
                return false;
            }

            resolvedSlots[i] = slot;
            resolvedPrefabs[i] = prefab;
        }

        // Phase B: 전원 검증 성공 → Instantiate.
        for (int i = 0; i < count; i++)
            SpawnPlanEntry(combatEntries[i], resolvedSlots[i], resolvedPrefabs[i]);

        return true;
    }

    private GameObject SpawnPlanEntry(EnemySpawnEntry entry, BattleLogicalSlotMap.Slot slot, GameObject prefab)
    {
        GridCellRef cell = slot.Cell;
        ClearGrid(slot.GridNumber);

        Quaternion facingPlayerRot = ApplyFacingPlayerRotation(slot.WorldRotation);
        GameObject go = Instantiate(prefab, slot.WorldPosition, facingPlayerRot, cell.transform);
        go.name = $"Enemy_{entry.Data.Index}_{go.GetInstanceID()}";

        foreach (CharactorScript legacy in go.GetComponentsInChildren<CharactorScript>(true))
            DestroyImmediate(legacy);

        BattleCharactor battle = go.GetComponent<BattleCharactor>();
        if (battle == null)
            battle = go.AddComponent<BattleCharactor>();

        EnemyScript enemyScript = go.GetComponent<EnemyScript>();
        if (enemyScript == null)
            enemyScript = go.AddComponent<EnemyScript>();

        // EnemyData.baseStats에 레벨 스탯이 반영돼 있으므로 SetLevelScaling(false) 경로로 그대로 최종 스탯이 된다.
        enemyScript.Initialize(entry.Data);

        // 명시 스킬(이벤트)이 있으면 배선. 일반 전투는 EnemyData.Index 규칙(EnemyScript)으로 기본 스킬 유도.
        if (entry.HasExplicitSkills)
        {
            battle.availableSkills.Clear();
            for (int i = 0; i < entry.ExplicitSkills.Count; i++)
            {
                if (entry.ExplicitSkills[i] != null)
                    battle.availableSkills.Add(entry.ExplicitSkills[i]);
            }

            if (battle.availableSkills.Count > 0)
            {
                battle.SetClassSkillIndex(battle.availableSkills[0].skillIndex);
                battle.ResolveSelectedSkill(false);
            }
        }

        battle.AssignToCell(cell);
        cell.SetOccupyingUnit(battle);
        spawnedByGrid[slot.GridNumber] = go;
        return go;
    }

    private GameObject ResolvePlanPrefab(EnemySpawnEntry entry)
    {
        string key = !string.IsNullOrWhiteSpace(entry.PrefabKey)
            ? entry.PrefabKey.Trim()
            : (entry.Data != null ? entry.Data.Index : string.Empty);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        key = key.Trim();

        // 리소스 경로(이벤트 PrefabResourcePath 등)면 직접 로드.
        if (key.Contains("/"))
            return Resources.Load<GameObject>(key);

        // 그 외(일반=Index): FindPrefab과 동일 규칙(prefab/BattlePrefab/EnemyUnit 아래 이름이 _{key}로 끝나는 프리팹).
        string suffix = $"_{key}";
        GameObject[] all = Resources.LoadAll<GameObject>("prefab/BattlePrefab/EnemyUnit");
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name.EndsWith(suffix))
                return all[i];
        }

        return null;
    }

    private void ClearGrid(int gridNumber)
    {
        if (spawnedByGrid.TryGetValue(gridNumber, out var existing) && existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing);
            }
            else
            {
                DestroyImmediate(existing);
            }
        }

        spawnedByGrid.Remove(gridNumber);
    }

    [ContextMenu("Debug Spawn Enemies")]
    private void DebugSpawn()
    {
        if (debugSpawnRequests == null) return;

        for (int i = 0; i < debugSpawnRequests.Count; i++)
        {
            var req = debugSpawnRequests[i];
            if (string.IsNullOrWhiteSpace(req.unitId)) continue;

            SpawnUnit(req.unitId, req.gridNumber);
        }
    }

    private static Quaternion ApplyFacingPlayerRotation(Quaternion gridRotation)
    {
        return gridRotation * FacingPlayerYawOffset;
    }

    private static bool TryResolveGridNumber(Transform slotTransform, out int gridNumber)
    {
        gridNumber = 0;
        if (slotTransform == null)
            return false;

        return BattleLogicalSlotMap.TryParseGridNumber(slotTransform.name, out gridNumber);
    }
}
