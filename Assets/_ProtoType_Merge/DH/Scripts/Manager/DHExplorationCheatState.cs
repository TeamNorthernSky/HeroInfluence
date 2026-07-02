public static class DHExplorationCheatState
{
    public static bool UnlimitedMovePoints { get; private set; }

    public static bool ToggleUnlimitedMovePoints()
    {
        UnlimitedMovePoints = !UnlimitedMovePoints;
        return UnlimitedMovePoints;
    }

    public static void SetUnlimitedMovePoints(bool enabled)
    {
        UnlimitedMovePoints = enabled;
    }
}
