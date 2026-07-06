public static class Game
{
    public static EconomyManager Economy => GameManager.Instance != null ? GameManager.Instance.Economy : null;
    public static GridManager Grid => GameManager.Instance != null ? GameManager.Instance.Grid : null;

    public static int CurrentDay => GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
}
