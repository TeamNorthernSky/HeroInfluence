using UnityEngine;

[DisallowMultipleComponent]
public class CombatContext : MonoBehaviour
{
    public static CombatContext Instance { get; private set; }

    [Header("Current Combat Context")]
    [SerializeField] private CombatPartyPersistentData combatParty;
    [SerializeField] private CombatEnemyPersistentData combatEnemy;
    [SerializeField] private CombatResult combatResult = CombatResult.None;

    public CombatPartyPersistentData CombatParty => combatParty;
    public CombatEnemyPersistentData CombatEnemy => combatEnemy;
    public CombatResult Result => combatResult;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterCombatParty(string partyId, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(partyId))
            return;

        if (combatParty == null)
        {
            combatParty = new CombatPartyPersistentData(partyId, unitIndices);
            return;
        }

        combatParty.SetPartyId(partyId);
        combatParty.SetUnitIndices(unitIndices);
    }

    public void RegisterCombatEnemy(string enemyId, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        RegisterCombatEnemy(enemyId, string.Empty, unitIndices);
    }

    public void RegisterCombatEnemy(string enemyId, string placementKey, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        if (combatEnemy == null)
        {
            combatEnemy = new CombatEnemyPersistentData(enemyId, placementKey, unitIndices);
            return;
        }

        combatEnemy.SetEnemyId(enemyId);
        combatEnemy.SetPlacementKey(placementKey);
        combatEnemy.SetUnitIndices(unitIndices);
    }

    public void SetCombatResult(CombatResult nextResult)
    {
        combatResult = nextResult;
    }

    public void Clear()
    {
        combatParty = null;
        combatEnemy = null;
        combatResult = CombatResult.None;
    }
}
