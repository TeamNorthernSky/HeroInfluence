using UnityEngine;

[DisallowMultipleComponent]
public class EnemyUnitState : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int unitIndex = -1;
    [SerializeField] private string unitTemplateKey;

    [Header("Runtime State")]
    [SerializeField] private int level = 1;
    [SerializeField] private StatBlock baseStats;
    [SerializeField] private StatBlock ingameStats;
    [SerializeField] private float currentHp;

    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => Mathf.Max(1, level);
    public StatBlock BaseStats => baseStats;
    public StatBlock IngameStats => ingameStats;
    public float CurrentHp => currentHp;

    public void InitializeFromTemplate(EnemyData template)
    {
        if (template == null)
            return;

        baseStats = template.baseStats;
        ingameStats = baseStats;
        currentHp = Mathf.Max(0f, ingameStats.HP);
    }

    public void AssignUnitIndex(int nextUnitIndex)
    {
        unitIndex = Mathf.Max(1, nextUnitIndex);
    }

    public void ApplyPersistentData(EnemyUnitPersistentData data)
    {
        if (data == null)
            return;

        unitIndex = data.UnitIndex;
        unitTemplateKey = data.UnitTemplateKey;
        level = Mathf.Max(1, data.Level);
        baseStats = data.BaseStats;
        ingameStats = data.IngameStats;
        currentHp = Mathf.Max(0f, data.CurrentHp);
    }

    public bool SyncToRepository()
    {
        if (unitIndex <= 0)
            return false;

        PersistentEnemyRepository repository = PersistentEnemyRepository.Instance;
        if (repository == null)
            return false;

        return repository.UpdateUnitRuntimeState(
            unitIndex,
            unitTemplateKey,
            Level,
            baseStats,
            ingameStats,
            currentHp);
    }

    public bool RefreshFromRepository()
    {
        if (unitIndex <= 0)
            return false;

        PersistentEnemyRepository repository = PersistentEnemyRepository.Instance;
        if (repository == null || !repository.TryGetUnit(unitIndex, out EnemyUnitPersistentData data))
            return false;

        ApplyPersistentData(data);
        return true;
    }

    public void SetLevel(int nextLevel)
    {
        level = Mathf.Max(1, nextLevel);
    }

    public void SetCurrentHp(float nextCurrentHp)
    {
        currentHp = Mathf.Clamp(nextCurrentHp, 0f, Mathf.Max(0f, ingameStats.HP));
    }

    private void OnValidate()
    {
        level = Mathf.Max(1, level);
        ingameStats = baseStats;
        currentHp = Mathf.Clamp(currentHp, 0f, Mathf.Max(0f, ingameStats.HP));
    }
}
