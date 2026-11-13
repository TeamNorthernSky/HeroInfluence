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

    [Header("BGM Ducking")]
    [Tooltip("덕킹 요청 중 적용할 BGM 배율. 0.75는 원래 볼륨의 75%입니다.")]
    [SerializeField, Range(0f, 1f)] private float bgmDuckingMultiplier = 0.75f;

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
    private readonly HashSet<UnityEngine.Object> bgmDuckRequests = new HashSet<UnityEngine.Object>();
    private string deferredBgmScene;
    private string currentBgmKey;
    private float currentBgmDefaultVolume = 1f;
    public bool MasterMuted { get; private set; }
    public bool BgmMuted { get; private set; }
    public bool SfxMuted { get; private set; }
    private float sceneBgmFadeVolume = 1f;
    private bool sceneBgmTransitionActive;
    private AudioSource bgmLoopSource;
    private Coroutine pendingBgmPreparation;
    private double scheduledBgmStart;
    private string overrideBgmScene;
    private string overrideBgmKey;

    public DHAudioClipCatalog ClipCatalog => clipCatalog;
    public string CurrentBgmKey => currentBgmKey;
    public bool IsReady => isActiveAndEnabled && clipCatalog != null && bgmSource != null && sfxSource != null;

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
        PreloadBgmClips();
        ApplyVolumes();
        // Apply saved settings before sceneLoaded/Start can play the first sound.
        GameSettings.LoadAndApplyAll();
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
        bgmDuckRequests.Clear();
        deferredBgmScene = null;
        if (Instance == this) StopBgm();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayBgm(string key)
    {
        if (bgmSource == null || clipCatalog == null)
            return;

        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (string.Equals(currentBgmKey, normalizedKey, System.StringComparison.OrdinalIgnoreCase) &&
            HasBgmPlayback())
        {
            return;
        }

        if (!TryGetBgmEntry(normalizedKey, out DHAudioClipEntry entry) || entry.Clip == null)
            return;

        StopBgm();
        currentBgmKey = entry.Key;
        currentBgmDefaultVolume = entry.DefaultVolume;
        ApplyBgmSourceVolume();
        RequestAudioData(entry.IntroClip);
        RequestAudioData(entry.Clip);
        if (IsBgmDataReady(entry))
            ScheduleBgm(entry);
        else
            pendingBgmPreparation = StartCoroutine(PrepareBgm(entry));
    }

    public void StopBgm()
    {
        if (pendingBgmPreparation != null)
            StopCoroutine(pendingBgmPreparation);
        pendingBgmPreparation = null;
        currentBgmKey = null;
        scheduledBgmStart = 0;

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
        if (bgmLoopSource != null)
        {
            bgmLoopSource.Stop();
            bgmLoopSource.clip = null;
        }
    }

    private bool HasBgmPlayback()
    {
        return pendingBgmPreparation != null || scheduledBgmStart > AudioSettings.dspTime ||
            (bgmSource != null && bgmSource.isPlaying) ||
            (bgmLoopSource != null && bgmLoopSource.isPlaying);
    }

    private void PreloadBgmClips()
    {
        if (clipCatalog == null) return;
        foreach (var entry in clipCatalog.BgmClips)
        {
            RequestAudioData(entry.IntroClip);
            RequestAudioData(entry.Clip);
        }
    }

    private static void RequestAudioData(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    private static bool IsBgmDataReady(DHAudioClipEntry entry)
    {
        return entry.Clip.loadState == AudioDataLoadState.Loaded &&
            (entry.IntroClip == null || entry.IntroClip.loadState == AudioDataLoadState.Loaded);
    }

    private System.Collections.IEnumerator PrepareBgm(DHAudioClipEntry entry)
    {
        // Keep the preparation handle valid even if loading fails immediately.
        yield return null;
        while (!IsBgmDataReady(entry))
        {
            if (entry.Clip.loadState == AudioDataLoadState.Failed ||
                (entry.IntroClip != null && entry.IntroClip.loadState == AudioDataLoadState.Failed))
            {
                Debug.LogWarning($"[AudioManager] Failed to load BGM '{entry.Key}'.", this);
                pendingBgmPreparation = null;
                currentBgmKey = null;
                yield break;
            }
            yield return null;
        }
        pendingBgmPreparation = null;
        ScheduleBgm(entry);
    }

    private void ScheduleBgm(DHAudioClipEntry entry)
    {
        AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
        double bufferSeconds = (double)bufferLength * bufferCount / Mathf.Max(1, AudioSettings.outputSampleRate);
        scheduledBgmStart = AudioSettings.dspTime + System.Math.Max(0.1, bufferSeconds * 2);
        bgmSource.pitch = bgmLoopSource.pitch = 1f;
        bgmSource.spatialBlend = bgmLoopSource.spatialBlend = 0f;
        bgmLoopSource.outputAudioMixerGroup = bgmSource.outputAudioMixerGroup;
        bgmLoopSource.clip = entry.Clip;
        bgmLoopSource.loop = true;
        if (entry.IntroClip != null)
        {
            // Schedule both now; no frame callback is needed at the intro/loop seam.
            double loopStart = scheduledBgmStart + (double)entry.IntroClip.samples / entry.IntroClip.frequency;
            bgmSource.clip = entry.IntroClip;
            bgmSource.loop = false;
            bgmSource.PlayScheduled(scheduledBgmStart);
            bgmSource.SetScheduledEndTime(loopStart);
            bgmLoopSource.PlayScheduled(loopStart);
        }
        else
            bgmLoopSource.PlayScheduled(scheduledBgmStart);
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
        if (string.Equals(normalizedKey, "Button_Click_Fail", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedKey, "Explore_MoveFail", System.StringComparison.OrdinalIgnoreCase))
            MouseClickSfx.NotifyClickFailPlayed();
    }

    public void PlaySfxForDuration(string key, float durationSeconds)
    {
        PlaySfxForDuration(key, durationSeconds, null);
    }

    public bool PlaySfxForDuration(string key, float durationSeconds, System.Action onCompleted)
    {
        if (clipCatalog == null)
            return false;

        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        if (!TryGetSfxEntry(normalizedKey, out DHAudioClipEntry entry) || entry.Clip == null)
            return false;

        float clampedDuration = Mathf.Max(0f, durationSeconds);
        if (clampedDuration <= 0f)
            return false;

        float playbackDuration = Mathf.Min(clampedDuration, entry.Clip.length);
        if (!entry.AllowOverlap)
        {
            if (sfxBlockedUntil.TryGetValue(entry.Key, out float blockedUntil) &&
                Time.unscaledTime < blockedUntil)
            {
                return false;
            }

            sfxBlockedUntil[entry.Key] = Time.unscaledTime + playbackDuration;
        }

        AudioSource source = GetAvailableTimedSfxSource();
        if (source == null)
            return false;

        source.Stop();
        source.clip = entry.Clip;
        source.loop = false;
        source.volume = GetSfxPlaybackVolume(entry);
        source.Play();

        StartCoroutine(StopTimedSfxAfter(source, playbackDuration, onCompleted));
        return true;
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

    // JC | 2026-11-04 | 결과 수치 카운팅은 표시가 끝날 때 즉시 멈추고 기존 호출자는 현재 루프를 끝까지 재생합니다.
    public void StopLoopingSfx(string key, bool finishCurrentCycle = true)
    {
        string normalizedKey = DHAudioClipCatalog.NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        if (!loopingSfxSources.TryGetValue(normalizedKey, out AudioSource source) || source == null)
            return;

        CancelScheduledLoopingSfxStop(normalizedKey);
        if (!finishCurrentCycle || !source.isPlaying || source.clip == null)
        {
            StopLoopingSfxImmediately(source);
            return;
        }

        float remainingSeconds = Mathf.Max(0f, source.clip.length - source.time);
        loopingSfxStopCoroutines[normalizedKey] = StartCoroutine(StopLoopingSfxAfterCurrentCycle(normalizedKey, source, remainingSeconds));
    }

    public void PlayBgmForScene(string sceneName)
    {
        if (string.Equals(deferredBgmScene, NormalizeSceneName(sceneName), System.StringComparison.OrdinalIgnoreCase))
            return;

        if (TryGetSceneBgmKey(sceneName, out string bgmKey))
        {
            PlayBgm(bgmKey);
            return;
        }

        if (stopBgmWhenSceneHasNoBinding)
            StopBgm();
    }

    public void DeferSceneBgm(string sceneName)
    {
        deferredBgmScene = NormalizeSceneName(sceneName);
        StopBgm();
    }

    public void ResumeSceneBgm(string sceneName, bool play = true)
    {
        if (!string.Equals(deferredBgmScene, NormalizeSceneName(sceneName), System.StringComparison.OrdinalIgnoreCase))
            return;

        deferredBgmScene = null;
        if (play && playBgmOnSceneLoaded &&
            string.Equals(SceneManager.GetActiveScene().name, NormalizeSceneName(sceneName), System.StringComparison.OrdinalIgnoreCase))
            PlayBgmForScene(sceneName);
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
                        || !HasBgmPlayback();
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

    public void SetMasterMuted(bool muted) { MasterMuted = muted; ApplyMuteState(); }
    public void SetBgmMuted(bool muted) { BgmMuted = muted; ApplyMuteState(); }
    public void SetSfxMuted(bool muted) { SfxMuted = muted; ApplyMuteState(); }

    private void ApplyMuteState()
    {
        ApplyBgmSourceVolume();
        if (sfxSource != null) sfxSource.mute = MasterMuted || SfxMuted;
        foreach (var source in timedSfxSources)
            if (source != null) source.mute = MasterMuted || SfxMuted;
        foreach (var source in loopingSfxSources.Values)
            if (source != null) source.mute = MasterMuted || SfxMuted;
        RefreshLoopingSfxVolumes();
    }

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        ApplyBgmSourceVolume();
    }

    public void SetBgmDucking(UnityEngine.Object requester, bool active)
    {
        if (ReferenceEquals(requester, null)) return;
        bool changed = active && requester != null
            ? bgmDuckRequests.Add(requester)
            : bgmDuckRequests.Remove(requester);
        if (changed) ApplyBgmSourceVolume();
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
        if (!string.IsNullOrWhiteSpace(overrideBgmKey) &&
            string.Equals(overrideBgmScene, normalizedSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            bgmKey = overrideBgmKey;
            return true;
        }
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

    public void SetSceneBgmOverride(string sceneName, string key)
    {
        overrideBgmScene = NormalizeSceneName(sceneName);
        overrideBgmKey = DHAudioClipCatalog.NormalizeKey(key);
    }

    public void ClearSceneBgmOverride(string sceneName)
    {
        if (!string.Equals(overrideBgmScene, NormalizeSceneName(sceneName), System.StringComparison.OrdinalIgnoreCase))
            return;
        overrideBgmScene = null;
        overrideBgmKey = null;
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
        if (Instance != this)
            return;

        if (bgmDuckRequests.RemoveWhere(requester => requester == null ||
                (requester is Behaviour behaviour && !behaviour.isActiveAndEnabled)) > 0)
            ApplyBgmSourceVolume();

        if (mode == LoadSceneMode.Single && !string.Equals(overrideBgmScene, scene.name, System.StringComparison.OrdinalIgnoreCase))
            ClearSceneBgmOverride(overrideBgmScene);

        if (!playBgmOnSceneLoaded)
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
        bgmLoopSource = gameObject.AddComponent<AudioSource>();
        bgmLoopSource.playOnAwake = false;

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
        float volume = (MasterMuted || BgmMuted ? 0f : masterVolume * bgmVolume) * currentBgmDefaultVolume * sceneBgmFadeVolume *
            (bgmDuckRequests.Count > 0 ? Mathf.Clamp01(bgmDuckingMultiplier) : 1f);
        if (bgmSource != null)
            bgmSource.volume = volume;
        if (bgmLoopSource != null)
            bgmLoopSource.volume = volume;
    }

    private float GetSfxPlaybackVolume(DHAudioClipEntry entry)
    {
        // 개별 재생 음량은 보관하고 AudioSource.mute로 출력을 차단한다.
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
        source.mute = MasterMuted || SfxMuted;
        return source;
    }

    private AudioSource CreateLoopingSfxSource(string key)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.mute = MasterMuted || SfxMuted;
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

    private System.Collections.IEnumerator StopTimedSfxAfter(AudioSource source, float durationSeconds, System.Action onCompleted)
    {
        yield return new WaitForSecondsRealtime(durationSeconds);

        if (source != null)
        {
            source.Stop();
            source.clip = null;
        }
        onCompleted?.Invoke();
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
