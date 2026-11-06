using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSettings
{
    private const string KeyResolution  = "Settings_Resolution";
    private const string KeyFullScreen  = "Settings_FullScreen";
    private const string KeyMaster      = "Settings_MasterVolume";
    private const string KeyBGM         = "Settings_BGMVolume";
    private const string KeyEffect      = "Settings_EffectVolume";
    private const string KeyAutoTurnEnd = "Settings_AutoTurnEnd";
    private const string KeyMasterMuted = "Settings_MasterMuted";
    private const string KeyBgmMuted = "Settings_BgmMuted";
    private const string KeyEffectMuted = "Settings_EffectMuted";

    public static float MasterVolume  => PlayerPrefs.GetFloat(KeyMaster, 1f);
    public static float BGMVolume     => PlayerPrefs.GetFloat(KeyBGM,    1f);
    public static float EffectVolume  => PlayerPrefs.GetFloat(KeyEffect,  1f);
    public static int   ResolutionIndex => PlayerPrefs.GetInt(KeyResolution, 2);
    public static bool  IsFullScreen  => PlayerPrefs.GetInt(KeyFullScreen, 1) == 1;
    public static bool AutoTurnEnd => PlayerPrefs.GetInt(KeyAutoTurnEnd, 1) == 1;

    public static SoundSettingsData SavedSound => new SoundSettingsData(MasterVolume, BGMVolume, EffectVolume,
        PlayerPrefs.GetInt(KeyMasterMuted, 0) != 0, PlayerPrefs.GetInt(KeyBgmMuted, 0) != 0,
        PlayerPrefs.GetInt(KeyEffectMuted, 0) != 0);
    private static SoundSettingsData appliedSound;
    private static bool soundInitialized;
    public static SoundSettingsData AppliedSound
    {
        get
        {
            if (!soundInitialized) { appliedSound = SavedSound; soundInitialized = true; }
            return appliedSound;
        }
    }

    // 미리 듣기는 실제 음량만 바꾸고 저장값은 유지한다.
    public static void PreviewSound(SoundSettingsData value)
    {
        appliedSound = value;
        soundInitialized = true;
        ApplyToAudioManager();
    }

    public static void SaveSound(SoundSettingsData value)
    {
        PlayerPrefs.SetFloat(KeyMaster, value.Master);
        PlayerPrefs.SetFloat(KeyBGM, value.Bgm);
        PlayerPrefs.SetFloat(KeyEffect, value.Effect);
        PlayerPrefs.SetInt(KeyMasterMuted, value.MasterMuted ? 1 : 0);
        PlayerPrefs.SetInt(KeyBgmMuted, value.BgmMuted ? 1 : 0);
        PlayerPrefs.SetInt(KeyEffectMuted, value.EffectMuted ? 1 : 0);
        PlayerPrefs.Save();
        PreviewSound(value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneSettings()
    {
        SceneManager.sceneLoaded -= ApplyAutoTurnEndToScene;
        SceneManager.sceneLoaded += ApplyAutoTurnEndToScene;
    }

    private static void ApplyAutoTurnEndToScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "DHScene_3") return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var manager in root.GetComponentsInChildren<TurnManager>(true))
                manager.AutoTurnEnd = AutoTurnEnd;
    }

    public static void ApplyAutoTurnEnd(bool enabled)
    {
        PlayerPrefs.SetInt(KeyAutoTurnEnd, enabled ? 1 : 0);
        PlayerPrefs.Save();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            ApplyAutoTurnEndToScene(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

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
        SaveSound(SavedSound.WithMaster(value));
    }

    public static void ApplyBGMVolume(float value)
    {
        SaveSound(SavedSound.WithBgm(value));
    }

    public static void ApplyEffectVolume(float value)
    {
        SaveSound(SavedSound.WithEffect(value));
    }

    // [KJ 260708] Settings 모달이 지연 초기화(self-deactivate)로 바뀌어도
    // 저장된 볼륨은 게임 시작 시 항상 적용되도록 GameSettings 자체에 부팅 훅을 둠.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnBoot()
    {
        appliedSound = SavedSound;
        soundInitialized = true;
        LoadAndApplyAll();
        SceneManager.sceneLoaded -= ReapplySoundToScene;
        SceneManager.sceneLoaded += ReapplySoundToScene;
    }

    private static void ReapplySoundToScene(Scene scene, LoadSceneMode mode) => ApplyToAudioManager();

    public static void LoadAndApplyAll()
    {
        // 전체 음량은 AudioManager에서 적용한다. 이전 출력 감쇠는 해제한다.
        ApplyToAudioManager();
    }

    private static void ApplyToAudioManager()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null) return;
        var sound = AppliedSound;
        audio.SetMasterVolume(sound.Master);
        audio.SetBgmVolume(sound.Bgm);
        audio.SetSfxVolume(sound.Effect);
        audio.SetMasterMuted(sound.MasterMuted);
        audio.SetBgmMuted(sound.BgmMuted);
        audio.SetSfxMuted(sound.EffectMuted);
    }
}
