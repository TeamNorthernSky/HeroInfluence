public static class DHGameEndState
{
    public static bool IsEnding { get; private set; }

    public static void BeginEnding()
    {
        IsEnding = true;
    }

    public static void Reset()
    {
        IsEnding = false;
    }
}
