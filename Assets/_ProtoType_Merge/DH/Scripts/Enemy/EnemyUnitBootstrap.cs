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
    [SerializeField] private List<EnemyUnitSeed> unitSeeds = new List<EnemyUnitSeed>();
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

        if (enemyUnit == null || enemyIdentity == null || enemyComposition == null || enemyRepository == null)
            return;

        if (!HasConfiguredUnitSeeds())
            CollectUnitSeedsFromChildren();

        if (!HasConfiguredUnitSeeds())
            return;

        if (onlyWhenUninitialized && !string.IsNullOrWhiteSpace(enemyIdentity.EnemyId) && !AreAllSlotsEmpty())
            return;

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("EnemyUnitBootstrap could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

        List<int> unitIndices = new List<int>(unitSeeds.Count);
        enemyComposition.EnsureSlotCount(unitSeeds.Count);
        for (int i = 0; i < unitSeeds.Count; i++)
        {
            EnemyUnitSeed seed = unitSeeds[i];
            if (seed == null)
                continue;

            if (string.IsNullOrWhiteSpace(seed.UnitTemplateKey))
            {
                Debug.LogWarning($"Enemy unit seed on '{seed.name}' is missing a unitTemplateKey.", seed);
                continue;
            }

            if (!templateCatalog.TryGetEnemyTemplate(seed.UnitTemplateKey, out EnemyData template))
            {
                Debug.LogWarning($"Enemy unit seed on '{seed.name}' could not resolve CSV template '{seed.UnitTemplateKey}'.", seed);
                continue;
            }

            int unitIndex = enemyRepository.CreateUnit(
                seed.UnitTemplateKey,
                seed.Level,
                template.baseStats);
            unitIndices.Add(unitIndex);
            enemyComposition.SetUnitIndexAt(i, unitIndex);
        }

        if (unitIndices.Count == 0)
            return;

        string enemyId = enemyRepository.CreateEnemy(unitIndices);
        enemyIdentity.SetEnemyId(enemyId);
        enemyUnit.InitializePersistentIdentity(enemyId);
    }

    [ContextMenu("Collect Unit Seeds From Children")]
    public void CollectUnitSeedsFromChildren()
    {
        unitSeeds.Clear();
        unitSeeds.AddRange(GetComponentsInChildren<EnemyUnitSeed>(true));
    }

    private bool HasConfiguredUnitSeeds()
    {
        for (int i = 0; i < unitSeeds.Count; i++)
        {
            if (unitSeeds[i] != null)
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
