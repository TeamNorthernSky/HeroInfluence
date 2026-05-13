using UnityEngine;

[DisallowMultipleComponent]
public class CombatContext : MonoBehaviour
{
    public static CombatContext Instance { get; private set; }

    [Header("Current Combat Context")]
    [SerializeField] private CombatPartyPersistentData combatParty;
    [SerializeField] private CombatEnemyPersistentData combatEnemy;
    [SerializeField] private CombatResult combatResult = CombatResult.None;
    // [JC 추가 260513] 전투 종료 후 적 인스턴스 처리 정책. null이면 BattleResult 기본 정책 사용.
    private PostCombatEnemyDirective enemyDirective;

    public CombatPartyPersistentData CombatParty => combatParty;
    public CombatEnemyPersistentData CombatEnemy => combatEnemy;
    public CombatResult Result => combatResult;
    public PostCombatEnemyDirective EnemyDirective => enemyDirective;

    public void SetEnemyDirective(PostCombatEnemyDirective directive)
    {
        enemyDirective = directive;
    }

    public void ClearEnemyDirective()
    {
        enemyDirective = null;
    }

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

    public void RegisterCombatEnemy(string enemyId, string instanceId, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        if (combatEnemy == null)
        {
            combatEnemy = new CombatEnemyPersistentData(enemyId, instanceId, unitIndices);
            return;
        }

        combatEnemy.SetEnemyId(enemyId);
        combatEnemy.SetInstanceId(instanceId);
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
        enemyDirective = null;
    }
}
