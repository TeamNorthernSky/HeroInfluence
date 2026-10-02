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
        if (AudioManager.Instance != null) AudioManager.Instance.SetBgmVolume(value);
        PlayerPrefs.SetFloat(KeyBGM, value);
        PlayerPrefs.Save();
    }

    public static void ApplyEffectVolume(float value)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(value);
        PlayerPrefs.SetFloat(KeyEffect, value);
        PlayerPrefs.Save();
    }

    // [KJ 260708] Settings 모달이 지연 초기화(self-deactivate)로 바뀌어도
    // 저장된 볼륨은 게임 시작 시 항상 적용되도록 GameSettings 자체에 부팅 훅을 둠.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnBoot()
    {
        LoadAndApplyAll();
        // AudioManager는 씬 오브젝트라 부팅 시점엔 없다. 씬 로드마다 저장값을 다시 넣어 준다.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => ApplyToAudioManager();
    }

    public static void LoadAndApplyAll()
    {
        // 마스터는 AudioListener로 전체에 적용 — AudioManager 마스터는 건드리지 않는다(이중 감쇠 방지).
        AudioListener.volume = MasterVolume;
        ApplyToAudioManager();
    }

    private static void ApplyToAudioManager()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;
        audio.SetBgmVolume(BGMVolume);
        audio.SetSfxVolume(EffectVolume);
    }
}
