using UnityEngine;

public static class GameSettings
{
    private const string KeyResolution  = "Settings_Resolution";
    private const string KeyFullScreen  = "Settings_FullScreen";
    private const string KeyMaster      = "Settings_MasterVolume";
    private const string KeyBGM         = "Settings_BGMVolume";
    private const string KeyEffect      = "Settings_EffectVolume";

    public static float MasterVolume  => PlayerPrefs.GetFloat(KeyMaster, 1f);
    public static float BGMVolume     => PlayerPrefs.GetFloat(KeyBGM,    1f);
    public static float EffectVolume  => PlayerPrefs.GetFloat(KeyEffect,  1f);
    public static int   ResolutionIndex => PlayerPrefs.GetInt(KeyResolution, 2);
    public static bool  IsFullScreen  => PlayerPrefs.GetInt(KeyFullScreen, 1) == 1;

    public static void ApplyResolution(int index, (int width, int height)[] resolutions)
    {
        if (index < 0 || index >= resolutions.Length) return;
        var r = resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
        PlayerPrefs.SetInt(KeyResolution, index);
        PlayerPrefs.Save();
    }

    public static void ApplyFullScreen(bool fullScreen)
    {
        Screen.fullScreen = fullScreen;
        PlayerPrefs.SetInt(KeyFullScreen, fullScreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void ApplyMasterVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(KeyMaster, value);
        PlayerPrefs.Save();
    }

    public static void ApplyBGMVolume(float value)
    {
        PlayerPrefs.SetFloat(KeyBGM, value);
        PlayerPrefs.Save();
    }

    public static void ApplyEffectVolume(float value)
    {
        PlayerPrefs.SetFloat(KeyEffect, value);
        PlayerPrefs.Save();
    }

    public static void LoadAndApplyAll()
    {
        AudioListener.volume = MasterVolume;
    }
}
