using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Catalog")]
    [SerializeField] private DHAudioClipCatalog clipCatalog;

    [Header("Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Min(1)] private int timedSfxInitialPoolSize = 2;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("Scene BGM")]
    [SerializeField] private bool playBgmOnSceneLoaded = true;
    [SerializeField] private bool stopBgmWhenSceneHasNoBinding;
    [SerializeField] private List<DHSceneBgmBinding> sceneBgmBindings = new List<DHSceneBgmBinding>();

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly Dictionary<string, float> sfxBlockedUntil = new Dictionary<string, float>();
    private readonly Dictionary<string, AudioSource> loopingSfxSources = new Dictionary<string, AudioSource>();
    private readonly Dictionary<string, Coroutine> loopingSfxStopCoroutines = new Dictionary<string, Coroutine>();
    private readonly List<AudioSource> timedSfxSources = new List<AudioSource>();
    private string currentBgmKey;
    private float currentBgmDefaultVolume = 1f;
    private float sceneBgmFadeVolume = 1f;
    private bool sceneBgmTransitionActive;

    public DHAudioClipCatalog ClipCatalog => clipCatalog;
    public string CurrentBgmKey => currentBgmKey;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        ResolveAudioSources();
        ApplyVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        if (Instance == this && playBgmOnSceneLoaded)
            PlayBgmForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void PlayBgm(string key)
    {
        if (bgmSource == null || clipCatalog == null)
            return;

        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (string.Equals(currentBgmKey, normalizedKey, System.StringComparison.OrdinalIgnoreCase) &&
            bgmSource.isPlaying)
        {
            return;
        }

        if (!TryGetBgmEntry(normalizedKey, out DHAudioClipEntry entry) || entry.Clip == null)
            return;

        currentBgmKey = entry.Key;
        currentBgmDefaultVolume = entry.DefaultVolume;
        bgmSource.clip = entry.Clip;
        bgmSource.loop = entry.Loop;
        ApplyBgmSourceVolume();
        bgmSource.Play();
    }

    public void StopBgm()
    {
        currentBgmKey = null;

        if (bgmSource != null)
            bgmSource.Stop();
    }

    public void PlaySfx(string key)
    {
        if (sfxSource == null || clipCatalog == null)
            return;

        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (!TryGetSfxEntry(normalizedKey, out DHAudioClipEntry entry) || entry.Clip == null)
            return;

        if (!entry.AllowOverlap)
        {
            if (sfxBlockedUntil.TryGetValue(entry.Key, out float blockedUntil) &&
                Time.unscaledTime < blockedUntil)
            {
                return;
            }

            sfxBlockedUntil[entry.Key] = Time.unscaledTime + entry.Clip.length;
        }

        sfxSource.PlayOneShot(entry.Clip, GetSfxPlaybackVolume(entry));
    }

    public void PlaySfxForDuration(string key, float durationSeconds)
    {
        if (clipCatalog == null)
            return;

        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (!TryGetSfxEntry(normalizedKey, out DHAudioClipEntry entry) || entry.Clip == null)
            return;

        float clampedDuration = Mathf.Max(0f, durationSeconds);
        if (clampedDuration <= 0f)
            return;

        float playbackDuration = Mathf.Min(clampedDuration, entry.Clip.length);
        if (!entry.AllowOverlap)
        {
            if (sfxBlockedUntil.TryGetValue(entry.Key, out float blockedUntil) &&
                Time.unscaledTime < blockedUntil)
            {
                return;
            }

            sfxBlockedUntil[entry.Key] = Time.unscaledTime + playbackDuration;
        }

        AudioSource source = GetAvailableTimedSfxSource();
        if (source == null)
            return;

        source.Stop();
        source.clip = entry.Clip;
        source.loop = false;
        source.volume = GetSfxPlaybackVolume(entry);
        source.Play();

        StartCoroutine(StopTimedSfxAfter(source, playbackDuration));
    }

    public void PlayLoopingSfx(string key)
    {
        if (clipCatalog == null)
            return;

        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (!TryGetSfxEntry(normalizedKey, out DHAudioClipEntry entry) || entry.Clip == null)
            return;

        if (loopingSfxSources.TryGetValue(entry.Key, out AudioSource existingSource) &&
            existingSource != null &&
            existingSource.isPlaying)
        {
            CancelScheduledLoopingSfxStop(entry.Key);
            existingSource.loop = true;
            existingSource.volume = GetSfxPlaybackVolume(entry);
            return;
        }

        AudioSource source = existingSource != null ? existingSource : CreateLoopingSfxSource(entry.Key);
        CancelScheduledLoopingSfxStop(entry.Key);
        source.Stop();
        source.clip = entry.Clip;
        source.loop = true;
        source.volume = GetSfxPlaybackVolume(entry);
        source.Play();
    }

    public void StopLoopingSfx(string key)
    {
        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (!loopingSfxSources.TryGetValue(normalizedKey, out AudioSource source) || source == null)
            return;

        CancelScheduledLoopingSfxStop(normalizedKey);
        if (!source.isPlaying || source.clip == null)
        {
            StopLoopingSfxImmediately(source);
            return;
        }

        float remainingSeconds = Mathf.Max(0f, source.clip.length - source.time);
        loopingSfxStopCoroutines[normalizedKey] = StartCoroutine(StopLoopingSfxAfterCurrentCycle(normalizedKey, source, remainingSeconds));
    }

    public void PlayBgmForScene(string sceneName)
    {
        if (TryGetSceneBgmKey(sceneName, out string bgmKey))
        {
            PlayBgm(bgmKey);
            return;
        }

        if (stopBgmWhenSceneHasNoBinding)
            StopBgm();
    }

    public void PrepareSceneBgmTransition(string sceneName)
    {
        sceneBgmTransitionActive = false;
        sceneBgmFadeVolume = 1f;
        if (playBgmOnSceneLoaded)
        {
            if (TryGetSceneBgmKey(sceneName, out string key))
            {
                // Invalid bindings keep the current track, just like PlayBgmForScene.
                if (TryGetBgmEntry(key, out DHAudioClipEntry entry) && entry.Clip != null)
                    sceneBgmTransitionActive = !string.Equals(currentBgmKey,
                        DHAudioClipCatalog.NormalizeKey(key), System.StringComparison.OrdinalIgnoreCase)
                        || bgmSource == null || !bgmSource.isPlaying;
            }
            else
                sceneBgmTransitionActive = stopBgmWhenSceneHasNoBinding;
        }
        ApplyBgmSourceVolume();
    }

    public void UpdateSceneBgmFade(float visibility, bool completed = false)
    {
        if (!sceneBgmTransitionActive)
            return;

        sceneBgmFadeVolume = Mathf.Clamp01(visibility);
        if (completed)
        {
            sceneBgmTransitionActive = false;
            sceneBgmFadeVolume = 1f;
        }
        ApplyBgmSourceVolume();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyBgmSourceVolume();
        RefreshLoopingSfxVolumes();
    }

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        ApplyBgmSourceVolume();
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        RefreshLoopingSfxVolumes();
    }

    public bool TryGetBgmEntry(string key, out DHAudioClipEntry entry)
    {
        return TryGetEntryFromList(clipCatalog != null ? clipCatalog.BgmClips : null, key, out entry);
    }

    public bool TryGetSfxEntry(string key, out DHAudioClipEntry entry)
    {
        return TryGetEntryFromList(clipCatalog != null ? clipCatalog.SfxClips : null, key, out entry);
    }

    public bool TryGetSceneBgmKey(string sceneName, out string bgmKey)
    {
        string normalizedSceneName = NormalizeSceneName(sceneName);
        if (!string.IsNullOrWhiteSpace(normalizedSceneName) && sceneBgmBindings != null)
        {
            for (int i = 0; i < sceneBgmBindings.Count; i++)
            {
                DHSceneBgmBinding binding = sceneBgmBindings[i];
                if (string.Equals(binding.SceneName, normalizedSceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    bgmKey = binding.BgmKey;
                    return !string.IsNullOrWhiteSpace(bgmKey);
                }
            }
        }

        bgmKey = string.Empty;
        return false;
    }

    private static bool TryGetEntryFromList(
        IReadOnlyList<DHAudioClipEntry> entries,
        string key,
        out DHAudioClipEntry entry)
    {
        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (entries != null && !string.IsNullOrWhiteSpace(normalizedKey))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                DHAudioClipEntry candidate = entries[i];
                if (string.Equals(candidate.Key, normalizedKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = default;
        return false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != this || !playBgmOnSceneLoaded)
            return;

        PlayBgmForScene(scene.name);
    }

    private void ResolveAudioSources()
    {
        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        bgmSource.playOnAwake = false;
        sfxSource.playOnAwake = false;

        EnsureTimedSfxPool();
    }

    private void ApplyVolumes()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        bgmVolume = Mathf.Clamp01(bgmVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);

        ApplyBgmSourceVolume();
    }

    private void ApplyBgmSourceVolume()
    {
        if (bgmSource != null)
            bgmSource.volume = masterVolume * bgmVolume * currentBgmDefaultVolume * sceneBgmFadeVolume;
    }

    private float GetSfxPlaybackVolume(DHAudioClipEntry entry)
    {
        return masterVolume * sfxVolume * entry.DefaultVolume;
    }

    private void EnsureTimedSfxPool()
    {
        int targetCount = Mathf.Max(1, timedSfxInitialPoolSize);
        while (timedSfxSources.Count < targetCount)
            timedSfxSources.Add(CreateTimedSfxSource());
    }

    private AudioSource GetAvailableTimedSfxSource()
    {
        for (int i = 0; i < timedSfxSources.Count; i++)
        {
            AudioSource source = timedSfxSources[i];
            if (source != null && !source.isPlaying)
                return source;
        }

        AudioSource newSource = CreateTimedSfxSource();
        timedSfxSources.Add(newSource);
        return newSource;
    }

    private AudioSource CreateTimedSfxSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        return source;
    }

    private AudioSource CreateLoopingSfxSource(string key)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        loopingSfxSources[key] = source;
        return source;
    }

    private void RefreshLoopingSfxVolumes()
    {
        foreach (KeyValuePair<string, AudioSource> pair in loopingSfxSources)
        {
            AudioSource source = pair.Value;
            if (source == null)
                continue;

            if (TryGetSfxEntry(pair.Key, out DHAudioClipEntry entry))
                source.volume = GetSfxPlaybackVolume(entry);
        }
    }

    private void CancelScheduledLoopingSfxStop(string key)
    {
        if (!loopingSfxStopCoroutines.TryGetValue(key, out Coroutine coroutine) || coroutine == null)
            return;

        StopCoroutine(coroutine);
        loopingSfxStopCoroutines.Remove(key);
    }

    private System.Collections.IEnumerator StopLoopingSfxAfterCurrentCycle(string key, AudioSource source, float remainingSeconds)
    {
        if (remainingSeconds > 0f)
            yield return new WaitForSecondsRealtime(remainingSeconds);

        if (source != null)
            StopLoopingSfxImmediately(source);

        loopingSfxStopCoroutines.Remove(key);
    }

    private static void StopLoopingSfxImmediately(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
    }

    private System.Collections.IEnumerator StopTimedSfxAfter(AudioSource source, float durationSeconds)
    {
        yield return new WaitForSecondsRealtime(durationSeconds);

        if (source == null)
            yield break;

        source.Stop();
        source.clip = null;
    }

    private static string NormalizeSceneName(string sceneName)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
    }
}

[System.Serializable]
public struct DHSceneBgmBinding
{
    [SerializeField] private string sceneName;
    [SerializeField] private string bgmKey;

    public string SceneName => string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
    public string BgmKey => DHAudioClipCatalog.NormalizeKey(bgmKey);
}
