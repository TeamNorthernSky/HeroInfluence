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
    [SerializeField] private BattleUnitPrefabCatalog prefabCatalog;
    [SerializeField] private TmpBattlePrefabManager prefabManager;
    // enemyManager 제거 — DHCsvTemplateCatalog.Instance 로 대체

    [Header("Inspector / Battle debug spawn")]
    public List<SpawnRequest> debugSpawnRequests = new List<SpawnRequest>();
    [SerializeField] private bool spawnOnStart;

    private Transform unitParent;
    private readonly Dictionary<int, Vector3> gridSlots = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Quaternion> gridRotations = new Dictionary<int, Quaternion>();
    [Tooltip("적 사망 후 시체를 필드에서 제거하기까지의 지연(초). 죽는 애니 길이 이상 권장.")]
    [SerializeField] private float _corpseRemovalDelay = 0.8f;

    private readonly Dictionary<int, GameObject> spawnedByGrid = new Dictionary<int, GameObject>();
    private BattleFlowManager _flowForRemoval;
    private readonly Dictionary<int, GridCellRef> gridCellsByNumber = new Dictionary<int, GridCellRef>();
    private BattleLogicalSlotMap eventSlotMap;
    private bool hierarchyReady;
    private bool prefabCatalogMismatchReported;
    private float lastSpawnPlanTotalExperience;
    private bool hasSuccessfulSpawnPlanRewardSnapshot;

    public Transform GridRoot => transform;
    public float LastSpawnPlanTotalExperience => lastSpawnPlanTotalExperience;
    public bool HasSuccessfulSpawnPlanRewardSnapshot => hasSuccessfulSpawnPlanRewardSnapshot;

    private void Awake()
    {
        gridSlots.Clear();
        gridRotations.Clear();
        gridCellsByNumber.Clear();
        eventSlotMap = null;
        unitParent = null;
        hierarchyReady = false;
        lastSpawnPlanTotalExperience = 0f;
        hasSuccessfulSpawnPlanRewardSnapshot = false;

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
        {
            CombatEventBattleData eventBattle = combatContext.EventBattle;
            if (!EnemySpawnPlanBuilder.TryBuildFromEventBattleKey(
                    eventBattle.ZoneId,
                    eventBattle.BattleKey,
                    eventBattle.EnemyLevel,
                    out EnemySpawnPlan eventPlan,
                    out string eventError))
            {
                Debug.LogError(
                    $"[EnemySpawner] Event battle spawn plan build failed. Battle={eventBattle.BattleKey}, error={eventError}",
                    this);
                return false;
            }

            return SpawnFromPreparedPlan(eventPlan);
        }

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
        // (적 유닛 리포지토리 자동 폴백 제거: 스폰 경로의 영속 의존을 끊는다.)
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
        if (TryGetRegisteredEnemyPrefab(trimmedIndex, out GameObject registeredPrefab))
            return registeredPrefab;

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


    private bool TryGetRegisteredEnemyPrefab(string unitKey, out GameObject prefab)
    {
        prefab = null;

        TmpBattlePrefabManager resolver = prefabManager != null
            ? prefabManager
            : TmpBattlePrefabManager.Instance;
        if (resolver != null)
            ReportCatalogMismatchOnce(resolver);

        if (prefabCatalog != null && prefabCatalog.TryGetEnemyPrefab(unitKey, out prefab))
            return true;

        return resolver != null && resolver.TryGetEnemyPrefab(unitKey, out prefab);
    }

    private void ReportCatalogMismatchOnce(TmpBattlePrefabManager resolver)
    {
        if (prefabCatalogMismatchReported || prefabCatalog == null || resolver == null ||
            resolver.Catalog == null || resolver.Catalog == prefabCatalog)
        {
            return;
        }

        prefabCatalogMismatchReported = true;
        Debug.LogWarning(
            "[EnemySpawner] Direct Catalog and TmpBattlePrefabManager Catalog differ. " +
            "The directly assigned Catalog has priority.",
            this);
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
        HookEnemyDeathRemoval(battle, gridNumber);
        Debug.Log(
            $"[EnemySpawner] 적 스폰 완료: id={enemyId}, grid={gridNumber}, Index={data.Index}, place={gameObject.name}");
        return go;
    }

    /// <summary>
    /// 점유 시 실패하는 안전 소환. 보스 런타임 소환용(구현지시서: 보스유닛_소환_창구스킬_페이즈AI §1·§4).
    /// <see cref="SpawnUnit"/>과 달리 <c>ClearGrid</c>로 기존 유닛을 파괴하지 않는다 — 대상 셀에
    /// BattleCharactor/인질이 이미 있으면 스폰하지 않고 false를 반환한다.
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

        // 점유 시 기존 유닛을 보존하고 조용히 실패.
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
        HookEnemyDeathRemoval(battle, gridNumber);
        spawned = go;
        Debug.Log(
            $"[EnemySpawner] 안전 소환 완료: id={enemyId}, grid={gridNumber}, Index={data.Index}, place={gameObject.name}");
        return true;
    }

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

        lastSpawnPlanTotalExperience = 0f;
        hasSuccessfulSpawnPlanRewardSnapshot = false;

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

        // 전투 중 적 오브젝트가 제거돼도 보상 계산이 가능하도록 실제 스폰 대상의 EXP를 순수 값으로 보관한다.
        // 인질 시나리오에서 combatEntries는 인질 원본을 이미 제외한 목록이다.
        for (int i = 0; i < combatEntries.Count; i++)
            lastSpawnPlanTotalExperience += Mathf.Max(0f, combatEntries[i].Data.ExperiencePoint);
        hasSuccessfulSpawnPlanRewardSnapshot = true;

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
        HookEnemyDeathRemoval(battle, slot.GridNumber);
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

        string dataIndex = entry.Data != null && !string.IsNullOrWhiteSpace(entry.Data.Index)
            ? entry.Data.Index.Trim()
            : string.Empty;
        if (!string.IsNullOrEmpty(dataIndex) &&
            TryGetRegisteredEnemyPrefab(dataIndex, out GameObject registeredPrefab))
        {
            return registeredPrefab;
        }

        if (!string.Equals(dataIndex, key, StringComparison.Ordinal) &&
            TryGetRegisteredEnemyPrefab(key, out registeredPrefab))
        {
            return registeredPrefab;
        }

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

    /// <summary>
    /// 런타임 스폰 유닛을 그리드 점유·룩업까지 정리하고 파괴한다(튜토리얼 Spawn rollback용, §5-⑦).
    /// 참가자 등록까지 되어 있었다면 flow 참가 목록에서도 제외한다. 단순 Destroy와 달리
    /// <c>spawnedByGrid</c>/GridCell 점유 잔류를 남기지 않는다. 성공 시 true.
    /// </summary>
    public bool TryDespawnRuntimeUnit(GameObject spawned)
    {
        if (spawned == null)
        {
            return false;
        }

        // 역참조로 gridNumber 찾기(등록 실패 롤백이면 방금 넣은 항목).
        int gridNumber = -1;
        foreach (KeyValuePair<int, GameObject> kv in spawnedByGrid)
        {
            if (kv.Value == spawned)
            {
                gridNumber = kv.Key;
                break;
            }
        }

        BattleCharactor bc = spawned.GetComponentInChildren<BattleCharactor>(true);
        if (bc != null)
        {
            // 논리 상태 제거: 참가자로 등록되어 있으면 flow에서 제외(미등록이면 무시된다).
            if (_flowForRemoval == null)
            {
                _flowForRemoval = FindFirstObjectByType<BattleFlowManager>();
            }
            _flowForRemoval?.RemoveUnit(bc, refreshQueue: false);

            // 셀 점유 해제.
            GridCellRef cell = bc.OccupiedCell;
            if (cell != null && cell.OccupyingUnit == bc)
            {
                cell.SetOccupyingUnit(null);
            }
            bc.ClearOccupiedCell();
        }

        if (gridNumber >= 0)
        {
            spawnedByGrid.Remove(gridNumber);
        }

        if (Application.isPlaying)
        {
            Destroy(spawned);
        }
        else
        {
            DestroyImmediate(spawned);
        }
        return true;
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

    /// <summary>GridCell에 대응하는 스포너의 표준 gridNumber를 반환한다.</summary>
    public bool TryGetGridNumber(GridCellRef cell, out int gridNumber)
    {
        gridNumber = -1;
        if (cell == null) return false;
        if (!hierarchyReady) Awake();

        foreach (KeyValuePair<int, GridCellRef> pair in gridCellsByNumber)
        {
            if (pair.Value != cell) continue;
            gridNumber = pair.Key;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 유지 중인 시체를 원래 셀과 전투 참가 목록에 복구한 뒤 동일 인스턴스로 부활시킨다.
    /// 현재 라운드 큐는 갱신하지 않아 실제 행동은 다음 라운드부터 가능하다.
    /// </summary>
    public bool TryReviveCorpseAt(BattleCharactor corpse, int gridNumber, float hpRatio)
    {
        if (corpse == null || !corpse.IsDead || corpse.IsPlayer) return false;
        if (!hierarchyReady) Awake();

        if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef cell) || cell == null)
        {
            Debug.LogWarning($"[EnemySpawner] 부활 셀을 찾지 못했습니다: grid={gridNumber}", this);
            return false;
        }

        if (cell.OccupyingUnit != null && cell.OccupyingUnit != corpse)
        {
            return false;
        }

        BattleCharactor[] unitsInCell = cell.GetComponentsInChildren<BattleCharactor>(true);
        for (int i = 0; i < unitsInCell.Length; i++)
        {
            if (unitsInCell[i] != null && unitsInCell[i] != corpse)
            {
                return false;
            }
        }

        HostageBattleActor hostage = cell.GetComponentInChildren<HostageBattleActor>(true);
        if (hostage != null)
        {
            return false;
        }

        if (spawnedByGrid.TryGetValue(gridNumber, out GameObject existing) &&
            existing != null && existing != corpse.gameObject)
        {
            return false;
        }

        if (_flowForRemoval == null) _flowForRemoval = FindFirstObjectByType<BattleFlowManager>();
        if (_flowForRemoval == null || ContainsParticipant(_flowForRemoval, corpse))
        {
            // 지연 RemoveUnit이 아직 끝나지 않았거나 전투 흐름이 없으면 다음 라운드에 재시도한다.
            return false;
        }

        // 먼저 점유와 참가 등록을 확정하고 마지막에 Revive하여
        // 살아 있지만 전투 시스템에는 없는 부분 성공 상태를 만들지 않는다.
        corpse.AssignToCell(cell);
        if (corpse.OccupiedCell != cell || cell.OccupyingUnit != corpse)
        {
            corpse.ClearOccupiedCell();
            return false;
        }

        if (!_flowForRemoval.RegisterRuntimeParticipant(corpse))
        {
            corpse.ClearOccupiedCell();
            return false;
        }

        spawnedByGrid[gridNumber] = corpse.gameObject;
        corpse.Revive(hpRatio);
        if (!corpse.IsDead)
        {
            return true;
        }

        // Revive가 예외적으로 적용되지 않았을 때 논리 등록을 되돌린다.
        if (spawnedByGrid.TryGetValue(gridNumber, out GameObject registered) &&
            registered == corpse.gameObject)
        {
            spawnedByGrid.Remove(gridNumber);
        }
        _flowForRemoval.RemoveUnit(corpse, refreshQueue: false);
        return false;
    }

    private static bool ContainsParticipant(BattleFlowManager flow, BattleCharactor unit)
    {
        if (flow == null || unit == null) return false;

        IReadOnlyList<BattleCharactor> participants = flow.Participants;
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i] == unit) return true;
        }
        return false;
    }

    // ── 적 사망 시 시체 제거 (죽는 연출 후 지연 제거). 플레이어는 제외. ──
    private void HookEnemyDeathRemoval(BattleCharactor battle, int gridNumber)
    {
        if (battle == null) return;
        int g = gridNumber;
        battle.OnDied += (bc) => ScheduleCorpseRemoval(bc, g);
    }

    public void ScheduleCorpseRemoval(BattleCharactor bc, int gridNumber)
    {
        if (bc == null || bc.IsPlayer) return;   // 플레이어 방어(정상적으론 적만 구독)
        StartCoroutine(RemoveCorpseAfter(bc, gridNumber, _corpseRemovalDelay));
    }

    private System.Collections.IEnumerator RemoveCorpseAfter(BattleCharactor bc, int gridNumber, float delay)
    {
        yield return new WaitForSeconds(delay);
        RemoveCorpseNow(bc, gridNumber);
    }

    private void RemoveCorpseNow(BattleCharactor bc, int gridNumber)
    {
        if (bc == null) { spawnedByGrid.Remove(gridNumber); return; }
        if (!bc.IsDead || bc.IsIncapacitated) return;   // 되살아났거나(부활) 무력화면 제거 취소

        GameObject go = bc.gameObject;
        EncounterParticipant participant = bc.GetComponent<EncounterParticipant>();
        bool keepCorpse = participant != null && participant.KeepCorpseAfterDeath;

        // 시체 유지 여부와 무관하게 논리 상태는 제거해 턴/타깃/그리드 점유에서 제외합니다.
        if (_flowForRemoval == null) _flowForRemoval = FindFirstObjectByType<BattleFlowManager>();
        _flowForRemoval?.RemoveUnit(bc, refreshQueue: false);   // 라운드 도중 제거 — 순서/라운드 초기화 방지(파괴된 참조는 GetNextUnit이 skip)

        if (spawnedByGrid.TryGetValue(gridNumber, out var g) && g == go) spawnedByGrid.Remove(gridNumber);

        if (keepCorpse)
        {
            DisableCorpseInteraction(go);
            return;
        }

        Destroy(go);
    }

    private static void DisableCorpseInteraction(GameObject corpse)
    {
        if (corpse == null) return;

        Collider[] colliders = corpse.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        Collider2D[] colliders2D = corpse.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null) colliders2D[i].enabled = false;
        }
    }

    private static bool TryResolveGridNumber(Transform slotTransform, out int gridNumber)
    {
        gridNumber = 0;
        if (slotTransform == null)
            return false;

        return BattleLogicalSlotMap.TryParseGridNumber(slotTransform.name, out gridNumber);
    }
}