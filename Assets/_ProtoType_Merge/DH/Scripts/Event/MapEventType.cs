public enum MapEventType
{
    TrainingHp,
    TrainingAtk,
    Heal
}

public static class MapEventTypeUtility
{
    public static string ToEventKey(MapEventType eventType)
    {
        return eventType switch
        {
            MapEventType.TrainingHp => "EVT_Training_001",
            MapEventType.TrainingAtk => "EVT_Training_002",
            MapEventType.Heal => "EVT_Heal_001",
            _ => eventType.ToString()
        };
    }

    public static int GetDefaultCostAmount(MapEventType eventType)
    {
        return 100;
    }

    public static int GetDefaultEffectAmount(MapEventType eventType)
    {
        return eventType switch
        {
            MapEventType.TrainingHp => 3,
            MapEventType.TrainingAtk => 1,
            _ => 0
        };
    }
}
