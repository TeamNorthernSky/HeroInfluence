public sealed class TurnResolutionContext
{
    public BattleCharactor Actor { get; }
    public bool WasSkipped { get; }
    public bool WasPlayer => Actor != null && Actor.IsPlayer;
    public int RoundIndex { get; }

    public TurnResolutionContext(
        BattleCharactor actor,
        bool wasSkipped,
        int roundIndex)
    {
        Actor = actor;
        WasSkipped = wasSkipped;
        RoundIndex = roundIndex;
    }
}
