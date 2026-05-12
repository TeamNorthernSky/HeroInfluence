using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyGridMover))]
[RequireComponent(typeof(EnemyIdentity))]
[RequireComponent(typeof(EnemyComposition))]
public class EnemyUnitBootstrap : MonoBehaviour
{
    [Header("Bootstrap")]
    [SerializeField] private bool populateOnStart = true;
    [SerializeField] private List<EnemyUnitState> unitStates = new List<EnemyUnitState>();
    [SerializeField] private bool onlyWhenUninitialized = true;

    private EnemyGridMover enemyUnit;
    private EnemyIdentity enemyIdentity;
    private EnemyComposition enemyComposition;

    private void Awake()
    {
        enemyUnit = GetComponent<EnemyGridMover>();
        enemyIdentity = GetComponent<EnemyIdentity>();
        enemyComposition = GetComponent<EnemyComposition>();
    }

    private void Start()
    {
        if (!Application.isPlaying || !populateOnStart)
            return;

        InitializeEnemyUnits();
    }

    [ContextMenu("Initialize Enemy Units")]
    public void InitializeEnemyUnits()
    {
        enemyUnit ??= GetComponent<EnemyGridMover>();
        enemyIdentity ??= GetComponent<EnemyIdentity>();
        enemyComposition ??= GetComponent<EnemyComposition>();
        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;

        if (enemyUnit == null || enemyIdentity == null || enemyComposition == null || enemyRepository == null || enemyGroupRepository == null)
            return;

        if (!HasConfiguredUnitStates())
            CollectUnitStatesFromChildren();

        if (!HasConfiguredUnitStates())
            return;

        if (onlyWhenUninitialized && !string.IsNullOrWhiteSpace(enemyIdentity.EnemyId) && !AreAllSlotsEmpty())
            return;

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("EnemyUnitBootstrap could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

        List<int> unitIndices = new List<int>(unitStates.Count);
        enemyComposition.EnsureSlotCount(unitStates.Count);
        for (int i = 0; i < unitStates.Count; i++)
        {
            EnemyUnitState unitState = unitStates[i];
            if (unitState == null)
                continue;

            if (string.IsNullOrWhiteSpace(unitState.UnitTemplateKey))
            {
                Debug.LogWarning($"Enemy unit state on '{unitState.name}' is missing a unitTemplateKey.", unitState);
                continue;
            }

            if (!templateCatalog.TryGetEnemyTemplate(unitState.UnitTemplateKey, out EnemyData template))
            {
                Debug.LogWarning($"Enemy unit state on '{unitState.name}' could not resolve CSV template '{unitState.UnitTemplateKey}'.", unitState);
                continue;
            }

            unitState.InitializeFromTemplate(template);
            int unitIndex = enemyRepository.CreateUnit(
                unitState.UnitTemplateKey,
                unitState.Level,
                unitState.BaseStats,
                unitState.IngameStats,
                unitState.CurrentHp);
            unitIndices.Add(unitIndex);
            unitState.AssignUnitIndex(unitIndex);
            enemyComposition.SetUnitIndexAt(i, unitIndex);
        }

        if (unitIndices.Count == 0)
            return;

        string enemyId = enemyGroupRepository.CreateEnemy(unitIndices);
        enemyIdentity.SetEnemyId(enemyId);
        enemyUnit.InitializePersistentIdentity(enemyId);
    }

    [ContextMenu("Collect Unit States From Children")]
    public void CollectUnitStatesFromChildren()
    {
        unitStates.Clear();
        unitStates.AddRange(GetComponentsInChildren<EnemyUnitState>(true));
    }

    private bool HasConfiguredUnitStates()
    {
        for (int i = 0; i < unitStates.Count; i++)
        {
            if (unitStates[i] != null)
                return true;
        }

        return false;
    }

    private bool AreAllSlotsEmpty()
    {
        if (enemyComposition == null)
            return true;

        int[] unitIndices = enemyComposition.UnitIndices;
        for (int i = 0; i < unitIndices.Length; i++)
        {
            if (unitIndices[i] > 0)
                return false;
        }

        return true;
    }
}
