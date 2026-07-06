using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EffectRegistry", menuName = "Battle/Effect Registry")]
public class EffectRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public int Id;
        public GameObject Prefab;
    }

    [SerializeField] private Entry[] _entries;

    private Dictionary<int, GameObject> _cache;

    public GameObject Get(int id)
    {
        if (_cache == null)
        {
            BuildCache();
        }

        _cache.TryGetValue(id, out GameObject result);
        return result;
    }

    private void BuildCache()
    {
        _cache = new Dictionary<int, GameObject>();
        if (_entries == null)
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (entry != null && entry.Prefab != null)
            {
                _cache[entry.Id] = entry.Prefab;
            }
        }
    }

    private void OnValidate() => _cache = null;
}
