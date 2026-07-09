using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SoundRegistry", menuName = "Battle/Sound Registry")]
public class SoundRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public int Id;
        public AudioClip Clip;
    }

    [SerializeField] private Entry[] _entries;

    private Dictionary<int, AudioClip> _cache;

    public AudioClip Get(int id)
    {
        if (_cache == null)
        {
            BuildCache();
        }

        _cache.TryGetValue(id, out AudioClip result);
        return result;
    }

    private void BuildCache()
    {
        _cache = new Dictionary<int, AudioClip>();
        if (_entries == null)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (entry != null && entry.Clip != null)
            {
                _cache[entry.Id] = entry.Clip;
            }
        }
    }

    private void OnValidate() => _cache = null;
}
