using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialUnitState : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string unitTemplateKey;

    [Header("Runtime Stats")]
    [SerializeField] private int level = 1;
    [SerializeField] private int currentHp;
    [SerializeField] private int maxHp;
    [SerializeField] private int currentIp;
    [SerializeField] private int maxIp;
    [SerializeField] private int atk;

    [Header("Startup")]
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private bool useTutorialProgressRepository = true;

    public string UnitTemplateKey => NormalizeKey(unitTemplateKey);
    public int Level => level;
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public int CurrentIp => currentIp;
    public int MaxIp => maxIp;
    public int Atk => atk;

    private void Awake()
    {
        if (initializeOnAwake)
            InitializeFromTutorialState();
    }

    private void OnValidate()
    {
        unitTemplateKey = NormalizeKey(unitTemplateKey);
        level = Mathf.Max(1, level);
        maxHp = Mathf.Max(0, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        maxIp = Mathf.Max(0, maxIp);
        currentIp = Mathf.Clamp(currentIp, 0, maxIp);
        atk = Mathf.Max(0, atk);
    }

    [ContextMenu("Initialize From Tutorial State")]
    public void InitializeFromTutorialState()
    {
        unitTemplateKey = NormalizeKey(unitTemplateKey);
        level = Mathf.Max(1, level);

        if (string.IsNullOrWhiteSpace(unitTemplateKey))
            return;

        TutorialProgressRepository repository = useTutorialProgressRepository
            ? TutorialProgressRepository.EnsureInstance()
            : null;

        if (repository != null &&
            repository.TryGetUnitState(unitTemplateKey, out TutorialUnitProgressState progressState) &&
            progressState != null &&
            HasStoredStats(progressState))
        {
            ApplyProgressState(progressState);
            return;
        }

        ApplyTemplateDefaults();
        SaveStatsToRepository();
    }

    public void SetStats(int nextCurrentHp, int nextMaxHp, int nextCurrentIp, int nextMaxIp, int nextAtk)
    {
        maxHp = Mathf.Max(0, nextMaxHp);
        currentHp = Mathf.Clamp(nextCurrentHp, 0, maxHp);
        maxIp = Mathf.Max(0, nextMaxIp);
        currentIp = Mathf.Clamp(nextCurrentIp, 0, maxIp);
        atk = Mathf.Max(0, nextAtk);
        SaveStatsToRepository();
    }

    public void SetCurrentHp(int value)
    {
        currentHp = Mathf.Clamp(value, 0, maxHp);
        SaveStatsToRepository();
    }

    public void SetCurrentIp(int value)
    {
        currentIp = Mathf.Clamp(value, 0, maxIp);
        SaveStatsToRepository();
    }

    public void SaveStatsToRepository()
    {
        if (!useTutorialProgressRepository || string.IsNullOrWhiteSpace(unitTemplateKey))
            return;

        TutorialUnitProgressState state = TutorialProgressRepository.EnsureInstance()?.GetOrCreateUnitState(unitTemplateKey);
        state?.SetStats(currentHp, maxHp, currentIp, maxIp, atk);
    }

    private void ApplyTemplateDefaults()
    {
        TutorialCatalog catalog = TutorialCatalog.Instance != null
            ? TutorialCatalog.Instance
            : FindFirstObjectByType<TutorialCatalog>();
        if (catalog == null ||
            !catalog.TryGetPlayerUnitTemplate(unitTemplateKey, out DHPlayerUnitTemplate template) ||
            template == null)
        {
            return;
        }

        StatBlock baseStats = template.BaseStats;
        maxHp = Mathf.Max(0, Mathf.RoundToInt(baseStats.HP));
        currentHp = maxHp;
        maxIp = Mathf.Max(0, Mathf.RoundToInt(baseStats.Influence));
        currentIp = Mathf.Clamp(Mathf.RoundToInt(baseStats.Influence), 0, maxIp);
        atk = Mathf.Max(0, Mathf.RoundToInt(baseStats.Atk));
    }

    private void ApplyProgressState(TutorialUnitProgressState state)
    {
        currentHp = Mathf.Max(0, state.CurrentHp);
        maxHp = Mathf.Max(currentHp, state.MaxHp);
        currentIp = Mathf.Max(0, state.CurrentIp);
        maxIp = Mathf.Max(currentIp, state.MaxIp);
        atk = Mathf.Max(0, state.Atk);
    }

    private static bool HasStoredStats(TutorialUnitProgressState state)
    {
        return state.MaxHp > 0 || state.MaxIp > 0 || state.Atk > 0;
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}
