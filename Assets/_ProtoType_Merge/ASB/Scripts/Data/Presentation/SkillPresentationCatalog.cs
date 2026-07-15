using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// skillIndex → SkillPresentationData 매핑 에셋.
/// Runtime SkillData에 SO 참조를 직접 붙이지 않고 별도 카탈로그에서 관리합니다.
/// </summary>
[CreateAssetMenu(fileName = "SkillPresentationCatalog", menuName = "Battle/Skill Presentation Catalog")]
public class SkillPresentationCatalog : ScriptableObject
{
    [Serializable]
    public class Binding
    {
        public int SkillIndex;
        public SkillPresentationData Presentation;
    }

    [SerializeField] private Binding[] _bindings;

    private Dictionary<int, SkillPresentationData> _cache;

    public SkillPresentationData Get(int skillIndex)
    {
        if (_cache == null)
        {
            BuildCache();
        }

        _cache.TryGetValue(skillIndex, out SkillPresentationData result);
        return result;
    }

    private void BuildCache()
    {
        _cache = new Dictionary<int, SkillPresentationData>();
        if (_bindings == null)
        {
            return;
        }

        foreach (Binding b in _bindings)
        {
            if (b != null && b.Presentation != null)
            {
                _cache[b.SkillIndex] = b.Presentation;
            }
        }
    }

    private void OnValidate() => _cache = null;
}
