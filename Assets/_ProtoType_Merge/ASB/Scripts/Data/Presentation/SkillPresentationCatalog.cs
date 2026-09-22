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

    [Tooltip("긴급 롤백용. 켜면 에셋의 AnimationRail 값을 바꾸지 않고 모든 Timeline Rail을 Animator Rail로 우회합니다.")]
    [SerializeField] private bool _forceAnimatorRail;
    [SerializeField] private Binding[] _bindings;

    public bool ForceAnimatorRail => _forceAnimatorRail;

    private Dictionary<int, SkillPresentationData> _cache;

    public SkillPresentationData Get(int skillIndex)
    {
        if (_cache == null)
        {
            BuildCache();
        }

        if (!_cache.TryGetValue(skillIndex, out SkillPresentationData result))
            _cache.TryGetValue(HeroSkillRules.FamilyId(skillIndex), out result);
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
