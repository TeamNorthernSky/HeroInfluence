using System.Collections.Generic;
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private DHAudioClipCatalog clipCatalog;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly Dictionary<string, float> sfxBlockedUntil = new Dictionary<string, float>();
    private string currentBgmKey;
    private float currentBgmDefaultVolume = 1f;

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

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyBgmSourceVolume();
    }

    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        ApplyBgmSourceVolume();
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    public bool TryGetBgmEntry(string key, out DHAudioClipEntry entry)
    {
        return TryGetEntryFromList(clipCatalog != null ? clipCatalog.BgmClips : null, key, out entry);
    }

    public bool TryGetSfxEntry(string key, out DHAudioClipEntry entry)
    {
        return TryGetEntryFromList(clipCatalog != null ? clipCatalog.SfxClips : null, key, out entry);
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

    private void ResolveAudioSources()
    {
        if (bgmSource == null)
            bgmSource = gameObject.AddComponent<AudioSource>();

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        bgmSource.playOnAwake = false;
        sfxSource.playOnAwake = false;
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
            bgmSource.volume = masterVolume * bgmVolume * currentBgmDefaultVolume;
    }

    private float GetSfxPlaybackVolume(DHAudioClipEntry entry)
    {
        return masterVolume * sfxVolume * entry.DefaultVolume;
    }
}
