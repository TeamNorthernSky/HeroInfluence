using System;
using UnityEngine;

/// <summary>Path A Timeline 효과음을 DH AudioManager에 전달한다.</summary>
public static class PresentationSfxPlayer
{
    // EditMode 테스트에서 실제 AudioSource 대신 호출을 기록한다.
    internal static Action<string> SinkOverride;
    private static bool s_WarnedMissingManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        s_WarnedMissingManager = false;
        SinkOverride = null;
    }

    public static void Play(string key)
    {
        if (SinkOverride != null)
        {
            SinkOverride(key);
            return;
        }

        AudioManager manager = AudioManager.Instance;
        if (manager != null)
        {
            manager.PlaySfx(key);
            return;
        }

        if (s_WarnedMissingManager) return;
        s_WarnedMissingManager = true;
        Debug.LogWarning("[PathA-SFX] AudioManager가 없어 Timeline 효과음을 재생하지 못했습니다. BootScene부터 실행하세요.");
    }
}
