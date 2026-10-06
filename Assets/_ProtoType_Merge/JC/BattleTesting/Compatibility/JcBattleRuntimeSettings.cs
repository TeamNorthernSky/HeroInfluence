// 원본 대응: ASB/Scripts/Battle/Core/BattleRuntimeSettings.cs. 함수는 동일하되 상태는 JC 테스트에만 보관하며 Play 시작 시 초기화합니다.
using UnityEngine;

public static class JcBattleRuntimeSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlaySession() => Reset();

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
