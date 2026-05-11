public static class Game
{
    public static EconomyManager Economy => GameManager.Instance != null ? GameManager.Instance.Economy : null;
    public static GridManager Grid => GameManager.Instance != null ? GameManager.Instance.Grid : null;
    public static TurnManager Turn => GameManager.Instance != null ? GameManager.Instance.Turn : null;
    public static CombatEncounterManager Combat => GameManager.Instance != null ? GameManager.Instance.Combat : null;
    public static FogGridManager FogGrid => GameManager.Instance != null ? GameManager.Instance.FogGrid : null;
    public static FogRenderManager FogRender => GameManager.Instance != null ? GameManager.Instance.FogRender : null;
    public static LevelLoader Level => GameManager.Instance != null ? GameManager.Instance.Level : null;

    // [JC 추가 260511] 영속 매니저·상태 접근자
    public static PersistentUnitRepository UnitRepo => GameManager.Instance != null ? GameManager.Instance.UnitRepo : null;
    public static PersistentEnemyRepository EnemyRepo => GameManager.Instance != null ? GameManager.Instance.EnemyRepo : null;
    public static HQVisitState HQVisit => GameManager.Instance != null ? GameManager.Instance.HQVisit : null;

    public static int CurrentDay
    {
        get => GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
        set { if (GameManager.Instance != null) GameManager.Instance.CurrentDay = value; }
    }
}
