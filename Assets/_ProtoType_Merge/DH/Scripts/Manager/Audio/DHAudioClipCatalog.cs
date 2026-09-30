using System;
using System.Collections.Generic;
using UnityEngine;

public enum DHAudioClipCategory
{
    Bgm,
    Sfx,
    Ui,
    Battle,
    BattleEnd,
    Explore
}

[CreateAssetMenu(
    fileName = "DHAudioClipCatalog",
    menuName = "DH Work/Audio/Audio Clip Catalog")]
public sealed class DHAudioClipCatalog : ScriptableObject
{
    [SerializeField] private List<DHAudioClipEntry> clips = new List<DHAudioClipEntry>();

    private Dictionary<string, DHAudioClipEntry> lookup;

    public IReadOnlyList<DHAudioClipEntry> Clips => clips;

    public bool TryGetClip(string key, out AudioClip clip)
    {
        if (TryGetEntry(key, out DHAudioClipEntry entry))
        {
            clip = entry.Clip;
            return clip != null;
        }

        clip = null;
        return false;
    }

    public bool TryGetEntry(string key, out DHAudioClipEntry entry)
    {
        EnsureLookup();

        string normalizedKey = NormalizeKey(key);
        if (!string.IsNullOrWhiteSpace(normalizedKey) &&
            lookup.TryGetValue(normalizedKey, out entry))
        {
            return true;
        }

        entry = default;
        return false;
    }

    public static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private void OnValidate()
    {
        lookup = null;
    }

    private void EnsureLookup()
    {
        if (lookup != null)
            return;

        lookup = new Dictionary<string, DHAudioClipEntry>(StringComparer.OrdinalIgnoreCase);
        if (clips == null)
            return;

        for (int i = 0; i < clips.Count; i++)
        {
            DHAudioClipEntry entry = clips[i];
            string key = NormalizeKey(entry.Key);
            if (string.IsNullOrWhiteSpace(key) || lookup.ContainsKey(key))
                continue;

            lookup.Add(key, entry);
        }
    }
}

[Serializable]
public struct DHAudioClipEntry
{
    [SerializeField] private string key;
    [SerializeField] private DHAudioClipCategory category;
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float defaultVolume;
    [SerializeField] private bool loop;
    [SerializeField] private bool allowOverlap;

    public string Key => DHAudioClipCatalog.NormalizeKey(key);
    public DHAudioClipCategory Category => category;
    public AudioClip Clip => clip;
    public float DefaultVolume => defaultVolume <= 0f ? 1f : defaultVolume;
    public bool Loop => loop;
    public bool AllowOverlap => allowOverlap;

    public DHAudioClipEntry(
        string key,
        DHAudioClipCategory category,
        AudioClip clip,
        float defaultVolume,
        bool loop,
        bool allowOverlap)
    {
        this.key = DHAudioClipCatalog.NormalizeKey(key);
        this.category = category;
        this.clip = clip;
        this.defaultVolume = Mathf.Clamp01(defaultVolume);
        this.loop = loop;
        this.allowOverlap = allowOverlap;
    }
}
