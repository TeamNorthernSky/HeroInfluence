public static class Game
{
    public static EconomyManager Economy => GameManager.Instance != null ? GameManager.Instance.Economy : null;
    public static GridManager Grid => GameManager.Instance != null ? GameManager.Instance.Grid : null;
    public static TurnManager Turn => GameManager.Instance != null ? GameManager.Instance.Turn : null;
    public static CombatEncounterManager Combat => GameManager.Instance != null ? GameManager.Instance.Combat : null;
    public static FogGridManager FogGrid => GameManager.Instance != null ? GameManager.Instance.FogGrid : null;
    public static FogRenderManager FogRender => GameManager.Instance != null ? GameManager.Instance.FogRender : null;
    public static LevelLoader Level => GameManager.Instance != null ? GameManager.Instance.Level : null;
}
