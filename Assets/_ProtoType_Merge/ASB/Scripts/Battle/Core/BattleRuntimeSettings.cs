using UnityEngine;

public static class BattleRuntimeSettings
{
    public const float NormalSpeed = 1f;

    public static bool IsAutoBattle { get; private set; }
    public static float BattleSpeed { get; private set; } = NormalSpeed;

    public static void SetAutoBattle(bool value)
    {
        IsAutoBattle = value;
    }

    public static void SetBattleSpeed(float value)
    {
        BattleSpeed = Mathf.Max(0.01f, value);
    }

    public static void Reset()
    {
        IsAutoBattle = false;
        BattleSpeed = NormalSpeed;
    }
}
